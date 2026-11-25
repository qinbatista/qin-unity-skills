"""Build and render five native VFX Graph briefs in one new, isolated Unity receiver."""
import argparse
import json
import os
from pathlib import Path
import shutil
import sys
import tempfile
import xml.etree.ElementTree as ET

from replay_fluids import CLI, EDITOR_VERSION, PACKAGES, native_parent, run_command

HERE = Path(__file__).resolve().parent
FORMS = ("Rain", "Embers", "Fountain", "Leaves", "Motes")


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
    common = [str(project), "--editor-path", str(editor), "--timeout", "1470", "--non-interactive", "--no-pager"]
    cli = [sys.executable, "-B", str(launcher)]
    with tempfile.TemporaryDirectory(prefix="v-", dir=native_parent(work)) as temp:
        env.update(TMPDIR=temp, TMP=temp, TEMP=temp)
        for label, method in (("init", "GraphTrialReplay.Configure"), ("author", "QinParticleGraphAuthor.CreateAll")):
            log = work / (label + ".log")
            run_command([*cli, "run", *common, "--log-file", str(log), "--no-tail", "--", "-job-worker-count", "3",
                         "-executeMethod", method], project, env, work / (label + "-cli.log"))
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
        if len(result["normal"]) != 40 or len(result["cohort"]) != 20:
            raise RuntimeError("Incomplete native captures for " + name)
    print(f"Trial {trial}: five graphs imported and passed in {work}", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", type=Path, required=True, help="New receiver directory under the task Cache.")
    parser.add_argument("--trial", type=int, required=True, choices=(1, 2, 3))
    parser.add_argument("--editor", type=Path, required=True)
    parser.add_argument("--launcher", type=Path, default=CLI)
    args = parser.parse_args()
    create_receiver(args.work.resolve(), args.trial, args.editor.resolve(), args.launcher.resolve())


if __name__ == "__main__":
    main()
