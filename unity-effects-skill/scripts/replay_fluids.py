"""Recreate the water gallery and its 2D applications in two fresh Unity projects."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

from fluid_application_checks import APPLICATIONS, review_application
from fluid_mixer import MANIFEST, install
from fluid_replay_checks import FORMS, review

HERE = Path(__file__).resolve().parent
CLI = HERE.parents[1] / "unity-code-skill/scripts/unity_cli_daily.py"
EDITOR_VERSION = "6000.6.7f1"
PACKAGES = {
    "com.unity.render-pipelines.universal": "17.6.0",
    "com.unity.shadergraph": "17.6.0",
    "com.unity.visualeffectgraph": "17.6.0",
    "com.unity.test-framework": "1.8.0",
    **{"com.unity.modules." + name: "1.0.0" for name in ("imageconversion", "video", "audio", "particlesystem", "physics2d")}
}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run_command(command, project, env, log):
    options = {"creationflags": subprocess.CREATE_NO_WINDOW} if os.name == "nt" else {}
    with log.open("w", encoding="utf-8") as stream:
        completed = subprocess.run(command, cwd=project, env=env, stdin=subprocess.DEVNULL,
                                   stdout=stream, stderr=subprocess.STDOUT, timeout=1530, **options)
    if completed.returncode:
        raise RuntimeError(f"Unity exited with {completed.returncode}. See {log} and its adjacent Unity log.")


def native_parent(work):
    if sys.platform != "darwin" or len(os.fsencode(work / "temp-f-12345678")) <= 70:
        return work
    # The macOS UPM socket has a short path limit. Keep it in the same task workspace.
    parent = next((p for p in work.parents if p.name == "Cache" and len(os.fsencode(p / "temp-f-12345678")) <= 70), None)
    if parent is None:
        raise ValueError("Use a shorter work path, or put it in a task run below a short project Cache.")
    return parent


def lane(label, work, editor, seed, case):
    base = work / label
    project = base / "project"
    for folder in ("Assets", "Packages", "ProjectSettings"):
        (project / folder).mkdir(parents=True)
    (project / ".qin-fluid-replay").write_text("Disposable native fluid receiver.\n")
    (project / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: " + EDITOR_VERSION + "\n")
    (project / "Packages/manifest.json").write_text(json.dumps({"dependencies": PACKAGES},indent=2))
    relative = "Assets/QinFluidMixer" if label == "A" else "Assets/Effects/Liquid"
    installed, _ = install(project, relative)
    shutil.copytree(HERE / "fluid-replay", project / "Assets/Editor/FluidReplay")
    captures = base / "captures"
    captures.mkdir()
    (base / "home/Library/Preferences").mkdir(parents=True)
    (base / "home/Library/Caches").mkdir()
    env = dict(os.environ, QIN_FLUID_OUTPUT=str(captures), QIN_FLUID_ASSET=relative,
               QIN_FLUID_SEED=str(seed), CFFIXED_USER_HOME=str(base / "home"),
               UPM_CACHE_ROOT=str(work / "upm"), DOTNET_PROCESSOR_COUNT="3")
    common = ["--editor-path",str(editor),"--timeout","1470","--non-interactive","--no-pager"]
    filters = {"gallery": "QinFluidReplay.Replay", "all": "QinFluidReplay"}
    selected = filters[case] if case in filters else "QinFluidReplay." + APPLICATIONS[case][0]
    with tempfile.TemporaryDirectory(prefix="temp-f-", dir=native_parent(work)) as scratch:
        env.update(TMPDIR=scratch,TEMP=scratch,TMP=scratch)
        run_command([sys.executable,"-B",str(CLI),"run",str(project),*common,"--log-file",str(base/"import.log"),
                     "--no-tail","--","-job-worker-count","3","-executeMethod","QinFluidReplay.Configure"],
                    project,env,base/"import-cli.log")
        if "QIN_FLUID_RECEIVER_READY" not in (base/"import.log").read_text(errors="replace"):
            raise RuntimeError(f"Editor setup did not finish. See {base/'import.log'}")
        run_command([sys.executable,"-B",str(CLI),"test",str(project),*common,"--mode","EditMode",
                     "--filter",selected,"--output",str(base/"tests.xml"),"--",
                     "-job-worker-count","3","-logFile",str(base/"play.log")],project,env,base/"play-cli.log")
    tests = list(ET.parse(base/"tests.xml").iter("test-case"))
    expected_cases = {"gallery": len(FORMS), "all": len(FORMS) + len(APPLICATIONS)}
    if len(tests) != expected_cases.get(case, 1) or any(t.get("result") != "Passed" for t in tests):
        raise RuntimeError(f"A native case failed. See {base/'tests.xml'}")
    actual = json.loads((project/"Packages/packages-lock.json").read_text())["dependencies"]
    if any(actual[key]["version"] != value for key,value in PACKAGES.items()):
        raise RuntimeError(f"Unexpected Unity package versions in receiver {label}")
    generated = {p.relative_to(installed).as_posix(): digest(p) for p in sorted(installed.rglob("*")) if p.is_file()}
    print(f"{label}: {len(tests)} native Play cases passed.", flush=True)
    return generated


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--editor",type=Path,required=True,help="Unity 6000.6.7f1 editor (.app on macOS)")
    parser.add_argument("--work",type=Path,required=True,help="New output directory inside the active task Cache")
    parser.add_argument("--seed",type=int,default=29411)
    parser.add_argument("--case", choices=("gallery", *APPLICATIONS, "all"), default="gallery",
                        help="Select the seven-form gallery, an individual 2D application, or both")
    parser.add_argument("--preview",action="store_true",help="Also write FluidMixer.gif; requires Pillow")
    parser.add_argument("--video", action="store_true", help="Also write a full-rate H.264 reference. Requires Pillow and ffmpeg")
    args=parser.parse_args()
    if args.video and not shutil.which("ffmpeg"):
        parser.error("Install ffmpeg on PATH to export the optional MP4.")
    media = args.preview or args.video
    if not (0 <= args.seed <= 0xffffffff):
        parser.error("Provide a 32-bit unsigned seed.")
    if not CLI.is_file():
        parser.error("Install the full qin-unity-skills pack; the native launcher belongs to unity-code-skill.")
    work = args.work.resolve()
    if work.exists():
        parser.error(f"Choose a new work directory. Existing data at {work} is preserved.")
    if media:
        import fluid_preview
    work.mkdir(parents=True)
    native_parent(work)
    with ThreadPoolExecutor(max_workers=2) as workers:
        futures = [workers.submit(lane,label,work,args.editor.resolve(),args.seed,args.case) for label in ("A","B")]
        first,second = [f.result() for f in futures]
    expected = json.loads(MANIFEST.read_text(encoding="utf-8"))["files"]
    if first != second or first != expected:
        raise RuntimeError("The imported asset bytes differ from the release manifest or the second receiver.")
    captures = work / "A/captures"
    gallery = args.case in ("gallery", "all")
    application_cases = {"gallery": (), "all": tuple(APPLICATIONS)}.get(args.case, (args.case,))
    device_record = "FreeJet/device.txt" if gallery else "device.txt"
    device = (captures / device_record).read_text().splitlines()
    report = {"editor": EDITOR_VERSION, "packages": PACKAGES, "seed": args.seed, "files": first,
              "receiverPaths": ["Assets/QinFluidMixer", "Assets/Effects/Liquid"], "graphics": device}
    if gallery:
        report["cases"] = review(work)
        if media:
            report["sideWaterline"] = fluid_preview.verify_side_waterline(captures)
            if args.seed == 29411:
                report["referenceSilhouetteIoU"] = fluid_preview.compare_silhouettes(captures)
            fluid_preview.export(captures, work / "FluidMixer.gif", work / "FluidMixer.mp4" if args.video else None)
    if application_cases:
        report["applications"] = {}
        for case in application_cases:
            app = review_application(work, case)
            if media:
                name = APPLICATIONS[case][1]
                app["visual"] = fluid_preview.review_application_images(captures / name, case, app["physics"])
                fluid_preview.export_application(captures / name, work / name, args.video)
            report["applications"][case] = app
    (work / "FluidVerification.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n")
    print(f"Verified {args.case} in both receivers. Evidence: {work / 'FluidVerification.json'}")



if __name__ == "__main__":
    main()
