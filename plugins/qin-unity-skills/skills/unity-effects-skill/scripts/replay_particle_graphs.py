"""Build and render five native VFX Graph briefs in one new, isolated Unity receiver."""
import argparse
import json
import os
from pathlib import Path
import re
import shutil
import sys
import tempfile
import xml.etree.ElementTree as ET

from replay_fluids import CLI, EDITOR_VERSION, PACKAGES, native_parent, run_command

HERE = Path(__file__).resolve().parent
FORMS = ("Rain", "Embers", "Fountain", "Leaves", "Motes")


def configured_before_timeout(work, error):
    """Accept only the CLI timeout during shutdown after verified, persisted URP setup."""
    if not str(error).startswith("Unity exited with 14. "):
        return False
    timeout = "Error: Unity process timed out after 1470 seconds. Terminating."
    if timeout not in (work / "init-cli.log").read_text(errors="replace").splitlines():
        return False
    editor_log = (work / "init.log").read_text(errors="replace")
    ready = "\nQIN_GRAPH_TRIALS_READY\n"
    if ready not in editor_log or "Batchmode quit successfully invoked" not in editor_log.split(ready, 1)[1]:
        return False
    project = work / "Project"
    assets = project / "Assets"
    if (assets / "GraphTrials").exists():
        return False
    for asset_name, consumer, setting in (
        ("TrialPipeline.asset", project / "ProjectSettings/GraphicsSettings.asset", r"m_CustomRenderPipeline:\s*"),
        ("TrialRenderer.asset", assets / "TrialPipeline.asset", r"m_RendererDataList:\s*\n\s*-\s*"),
    ):
        asset = assets / asset_name
        meta = asset.with_name(asset.name + ".meta")
        if not (asset.is_file() and meta.is_file() and consumer.is_file()):
            return False
        match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(), re.MULTILINE)
        if not match or not re.search(setting + r"\{fileID: 11400000, guid: " + match[1] + r", type: 2\}", consumer.read_text()):
            return False
    return True


def create_receiver(work, trial, editor, launcher):
    if not work.parent.is_dir() or work.exists():
        raise ValueError("Use a new child directory of the allocated task Cache.")
    work.mkdir()
    project = work / "Project"
    for name in ("Assets", "ProjectSettings", "Packages"):
        (project / name).mkdir(parents=True)
    (project / ".qin-graph-trials").touch()
    (project / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: " + EDITOR_VERSION + "\n")
    (project / "Packages/manifest.json").write_text(json.dumps({"dependencies": PACKAGES}, indent=2))
    (work / "out").mkdir()
    for name in ("Caches", "Preferences"):
        (work / "home/Library" / name).mkdir(parents=True)
    shutil.copytree(HERE / "particle-graph-replay", project / "Assets/Editor/GraphTrials")
    shutil.copytree(HERE / "particle-graph-trials", project / "Assets/Editor/GraphAuthor")
    env = dict(os.environ, QIN_VFX_OUTPUT=str(work / "out"), QIN_VFX_TRIAL=str(trial),
               CFFIXED_USER_HOME=str(work / "home"), UPM_CACHE_ROOT=str(work / "upm"), DOTNET_PROCESSOR_COUNT="3")
    selection = ["--editor-path", str(editor)] if editor else ["--editor-version", EDITOR_VERSION]
    common = [str(project), *selection, "--timeout", "1470", "--non-interactive", "--no-pager"]
    cli = [sys.executable, "-B", str(launcher)]
    with tempfile.TemporaryDirectory(prefix="v-", dir=native_parent(work)) as temp:
        env.update(TMPDIR=temp, TMP=temp, TEMP=temp)
        for label, method in (("init", "GraphTrialReplay.Configure"), ("author", "QinParticleGraphAuthor.CreateAll")):
            log = work / (label + ".log")
            try:
                run_command([*cli, "run", *common, "--log-file", str(log), "--no-tail", "--", "-job-worker-count", "3",
                             "-executeMethod", method], project, env, work / (label + "-cli.log"))
            except RuntimeError as error:
                if label != "init" or not configured_before_timeout(work, error):
                    raise
                print("Unity timed out during setup shutdown. Verified saved URP settings. Continuing through the project CLI.", flush=True)
        run_command([*cli, "test", *common, "--mode", "EditMode", "--filter", "GraphTrialReplay.ShowTrials",
                     "--output", str(work / "tests.xml"), "--", "-job-worker-count", "3", "-perfTestResults",
                     str(work / "performance.json"), "-logFile", str(work / "play.log")], project, env, work / "test-cli.log")
    cases = list(ET.parse(work / "tests.xml").iter("test-case"))
    if len(cases) != len(FORMS) or any(case.get("result") != "Passed" for case in cases):
        raise RuntimeError("Expected five passing graph replays. See tests.xml and play.log in " + str(work))
    direct = json.loads((project / "Packages/packages-lock.json").read_text())["dependencies"]
    if any(direct[key]["version"] != version for key, version in PACKAGES.items()):
        raise RuntimeError("The imported package versions differ from this fixture.")
    for form in FORMS:
        name = f"{form}-{trial}"
        if not (project / "Assets/GraphTrials" / (name + ".vfx")).is_file():
            raise RuntimeError("Missing independently authored graph " + name)
        result = json.loads((work / "out" / name / "result.json").read_text())
        if result["unity"] != EDITOR_VERSION:
            raise RuntimeError(f"{name} ran on {result['unity']}, expected {EDITOR_VERSION}")
        if len(result["normal"]) != 40 or len(result["cohort"]) != 20:
            raise RuntimeError("Incomplete native captures for " + name)
    print(f"Trial {trial}: five graphs imported and passed in {work}", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", type=Path, required=True, help="New receiver directory under the task Cache.")
    parser.add_argument("--trial", type=int, required=True, choices=(1, 2, 3))
    parser.add_argument("--editor", type=Path, help="Explicit editor path. Omit to select the fixture's exact Unity version.")
    parser.add_argument("--launcher", type=Path, default=CLI)
    args = parser.parse_args()
    editor = args.editor.resolve() if args.editor else None
    create_receiver(args.work.resolve(), args.trial, editor, args.launcher.resolve())


if __name__ == "__main__":
    main()
