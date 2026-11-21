"""Check the installed Unity CLI once per local day and forward a command."""

from contextlib import closing
from datetime import date
import json
import os
from pathlib import Path
import shutil
import sqlite3
import subprocess
import sys

from hidden_process import hidden_process_options


def self_update(executable, arguments, environment, timeout):
    result = subprocess.run(
        [executable, "self-update", *arguments, "--non-interactive", "--format", "json"],
        env=environment, stdin=subprocess.DEVNULL, capture_output=True,
        encoding="utf-8", errors="replace", timeout=timeout, **hidden_process_options(),
    )
    if result.returncode:
        detail = result.stderr.strip() or result.stdout.strip()
        raise RuntimeError(f"Unity CLI self-update exited {result.returncode}. {detail}".strip())
    response = json.loads(result.stdout)
    if not isinstance(response, dict) or response.get("success") is not True:
        raise RuntimeError(f"Unity CLI self-update did not succeed. {result.stdout.strip()}")
    return response


def check_update(executable, environment, state_path):
    """Serialize callers for the same installation and record successful checks."""
    key = str(Path(executable).resolve())
    state_path.parent.mkdir(parents=True, exist_ok=True)
    with closing(sqlite3.connect(state_path, timeout=300)) as database, database:
        # Keep the receipt readable and writable by the existing YoFaAssets launcher.
        database.execute("CREATE TABLE IF NOT EXISTS checks (executable TEXT PRIMARY KEY, day TEXT NOT NULL)")
        database.execute("BEGIN IMMEDIATE")
        today = date.today().isoformat()
        previous = database.execute("SELECT day FROM checks WHERE executable = ?", (key,)).fetchone()
        if previous == (today,):
            return

        versions = self_update(executable, ["--check"], environment, timeout=45).get("data")
        if not isinstance(versions, dict) or not versions.get("currentVersion") or not versions.get("latestVersion"):
            raise ValueError("Unity CLI returned no current or latest version")
        if versions["currentVersion"] != versions["latestVersion"]:
            self_update(executable, ["--yes"], environment, timeout=180)
            verified = self_update(executable, ["--check"], environment, timeout=45).get("data")
            if not isinstance(verified, dict) or not verified.get("currentVersion") or verified["currentVersion"] != verified.get("latestVersion"):
                raise ValueError("Unity CLI update could not be verified")

        database.execute("INSERT OR REPLACE INTO checks (executable, day) VALUES (?, ?)", (key, today))


def main(arguments):
    executable = shutil.which("unity")
    if not executable:
        print("Unity CLI was not found on PATH", file=sys.stderr)
        return 127

    environment = dict(os.environ, UNITY_NO_UPDATE_CHECK="1", UNITY_NON_INTERACTIVE="1", UNITY_NO_CONSENT_PROMPT="1", UNITY_NO_PAGER="1")
    state_path = Path.home() / ".unity" / "yofa-cli-update" / "checks.sqlite3"
    try:
        check_update(executable, environment, state_path)
    except (OSError, RuntimeError, ValueError, sqlite3.Error, subprocess.SubprocessError) as error:
        print(f"Unity CLI update check failed. {error}", file=sys.stderr)

    return subprocess.run([executable, *arguments], env=environment, **hidden_process_options()).returncode


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
