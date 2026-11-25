"""Recreate the water gallery and its 2D applications in two fresh Unity projects."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
from importlib import import_module
import json
import os
import re
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

from fluid_application_checks import APPLICATIONS, review_application
from fluid_mixer import MANIFEST, install
from compose_water import compose
from channel_replay_checks import CHANNELS, COVE, review_channels
from inlet_replay_checks import INLETS, review_inlets
from fluid_replay_checks import FORMS, review

HERE = Path(__file__).resolve().parent
CLI = HERE.parents[1] / "unity-code-skill/scripts/unity_cli_daily.py"
sys.path.insert(0, str(CLI.parent))
from hidden_process import hidden_process_options

EDITOR_VERSION = "6000.6.7f1"
PACKAGES = {
    "com.unity.render-pipelines.universal": "17.6.0",
    "com.unity.shadergraph": "17.6.0",
    "com.unity.visualeffectgraph": "17.6.0",
    "com.unity.test-framework": "1.8.0",
    **{"com.unity.modules." + name: "1.0.0" for name in ("imageconversion", "video", "audio", "particlesystem", "physics2d")}
}


SUITES = {
    "channel-matrix": ("channel_matrix", "ChannelMatrix"),
    "topdown": ("topdown_matrix", "TopdownMatrix"),
    "spells": ("spell_matrix", "SpellMatrix"),
    "limits": ("water_limits", "ParameterLimits"),
}


def selected_suites(case):
    return {group: import_module(module) for group, (module, _) in SUITES.items()
            if case == "all" or case == group}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run_command(command, project, env, log, *, timeout=1530):
    with log.open("w", encoding="utf-8") as stream:
        completed = subprocess.run(command, cwd=project, env=env, stdin=subprocess.DEVNULL,
                                   stdout=stream, stderr=subprocess.STDOUT, timeout=timeout, **hidden_process_options())
    if completed.returncode:
        raise RuntimeError(f"Unity exited with {completed.returncode}. See {log} and its adjacent Unity log.")


def native_parent(work):
    if sys.platform != "darwin" or len(os.fsencode(work / "temp-f-12345678")) <= 70:
        return work
    # APFS exposes a short name for the same directory, including on long task paths.
    stat = work.stat()
    short = Path("/.vol") / str(stat.st_dev) / str(stat.st_ino)
    if not short.is_dir() or not short.samefile(work):
        raise ValueError("Use a shorter task-owned path on this volume for native Unity IPC.")
    with tempfile.TemporaryDirectory(prefix="ipc-", dir=short) as test:
        probe = Path(test) / "socket"
        with socket.socket(socket.AF_UNIX) as connection:
            connection.bind(str(probe))
            if not (work / probe.parent.name / probe.name).is_socket():
                raise OSError("The native socket must resolve inside the task-owned replay directory.")
    return short


def named_receiver(project, relative, case, edition, prefix):
    from water_templates import CASES, compose_bundle, recipe

    authored = {}
    paths = []
    for subject, (suite, _) in CASES.items():
        if case not in ("all", suite):
            continue
        subject_edition = edition if subject == "WideCloudburst" else "original"
        _, actors = recipe(subject, subject_edition)
        name = prefix + subject
        prefabs, _ = compose_bundle(project, relative, "Assets/GameWater", name, actors,
                                    original_surface=subject_edition == "original")
        authored[subject] = {p.name: digest(p) for p in sorted(prefabs[0].parent.iterdir()) if p.is_file()}
        paths.extend(p.relative_to(project).as_posix() for p in prefabs)
    (project / ".qin-fluid-template-prefix").write_text(prefix, encoding="utf-8")
    return authored, paths


def lane(label, work, editor, seed, case, launcher, temp_parent, edition="refined", asset_prefix=None):
    base = work / label
    project = base / "project"
    for folder in ("Assets", "Packages", "ProjectSettings"):
        (project / folder).mkdir(parents=True)
    (project / ".qin-fluid-replay").write_text("Disposable native fluid receiver.\n")
    (project / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: " + EDITOR_VERSION + "\n")
    (project / "Packages/manifest.json").write_text(json.dumps({"dependencies": PACKAGES},indent=2))
    relative = "Assets/QinFluidMixer" if label == "A" else "Assets/Effects/Liquid"
    installed, _ = install(project, relative)
    suites = selected_suites(case)
    named_paths = []
    if asset_prefix:
        authored, named_paths = named_receiver(project, relative, case, edition, asset_prefix)
    else:
        authored = {}
        briefs = {}
        if case in ("all", "channels"):
            briefs["CoveWash"] = ("shore", COVE)
        if case in ("all", "inlets"):
            briefs.update({name: (profile, {}) for name, profile in INLETS.items()})
        for name, (profile, overrides) in briefs.items():
            path, result = compose(project, relative, "Assets/GameWater", name, profile, overrides)
            if result != "composed":
                raise RuntimeError("Expected independent authoring in this receiver")
            authored[name] = {p.name: digest(p) for p in sorted(path.parent.iterdir()) if p.is_file()}
        for group, suite in suites.items():
            options = {"edition": edition} if group == "limits" else {}
            authored.update(suite.author(project, relative, **options))
    if authored:
        (base / "authored.json").write_text(json.dumps(authored, sort_keys=True))
    shutil.copytree(HERE / "fluid-replay", project / "Assets/Editor/FluidReplay")
    captures = base / "captures"
    captures.mkdir()
    (base / "home/Library/Preferences").mkdir(parents=True)
    (base / "home/Library/Caches").mkdir()
    env = dict(os.environ, QIN_FLUID_OUTPUT=str(captures), QIN_FLUID_ASSET=relative,
               QIN_FLUID_SEED=str(seed), CFFIXED_USER_HOME=str(base / "home"),
               UPM_CACHE_ROOT=str(work / "upm"), DOTNET_PROCESSOR_COUNT="3")
    common = ["--editor-path",str(editor),"--timeout","1470","--non-interactive","--no-pager"]
    filters = {"reuse": "QinFluidReplay.ReuseRockImpact", "gallery": "QinFluidReplay.Replay", "all": "QinFluidReplay", "channels": "QinFluidReplay.FlowChannel", "inlets": "QinFluidReplay.CastWaterVariant"}
    filters.update({group: "QinFluidReplay." + method for group, (_, method) in SUITES.items()})
    selected = filters[case] if case in filters else "QinFluidReplay." + APPLICATIONS[case][0]
    with tempfile.TemporaryDirectory(prefix="temp-f-", dir=temp_parent) as scratch:
        env.update(TMPDIR=scratch,TEMP=scratch,TMP=scratch)
        run_command([sys.executable,"-B",str(launcher),"run",str(project),*common,"--log-file",str(base/"import.log"),
                     "--no-tail","--","-job-worker-count","3","-executeMethod","QinFluidReplay.Configure"],
                    project,env,base/"import-cli.log")
        if "QIN_FLUID_RECEIVER_READY" not in (base/"import.log").read_text(errors="replace"):
            raise RuntimeError(f"Editor setup did not finish. See {base/'import.log'}")
        run_command([sys.executable,"-B",str(launcher),"test",str(project),*common,"--mode","EditMode",
                     "--filter",selected,"--output",str(base/"tests.xml"),"--",
                     "-job-worker-count","3","-perfTestResults",str(base/"performance.json"),"-logFile",str(base/"play.log")],project,env,base/"play-cli.log")
    tests = list(ET.parse(base/"tests.xml").iter("test-case"))
    expected_cases = {"gallery": len(FORMS), "all": len(FORMS) + len(APPLICATIONS) + len(CHANNELS) + len(INLETS) + 1,
                      "channels": len(CHANNELS), "inlets": len(INLETS)}
    expected_cases.update({group: len(suite.CASES) for group, suite in suites.items()})
    expected_cases["all"] += sum(len(suite.CASES) for suite in suites.values())
    if len(tests) != expected_cases.get(case, 1) or any(t.get("result") != "Passed" for t in tests):
        raise RuntimeError(f"A native case failed. See {base/'tests.xml'}")
    if case in ("all", "reuse") and sum(t.get("fullname") == "QinFluidReplay.ReuseRockImpact" for t in tests) != 1:
        raise RuntimeError(f"The native water reuse test was missing or duplicated in receiver {label}")
    if named_paths:
        lines = (base / "play.log").read_text(errors="replace").splitlines()
        loaded = {line.removeprefix("QIN_WATER_TEMPLATE_ASSET ") for line in lines
                  if line.startswith("QIN_WATER_TEMPLATE_ASSET ")}
        missing = set(named_paths) - loaded
        if missing:
            raise RuntimeError(f"Native fixture did not load authored prefabs: {sorted(missing)}")
        (base / "named-prefabs.json").write_text(json.dumps(sorted(named_paths), indent=2), encoding="utf-8")
    actual = json.loads((project/"Packages/packages-lock.json").read_text())["dependencies"]
    if any(actual[key]["version"] != value for key,value in PACKAGES.items()):
        raise RuntimeError(f"Unexpected Unity package versions in receiver {label}")
    generated = {p.relative_to(installed).as_posix(): digest(p) for p in sorted(installed.rglob("*")) if p.is_file()}
    print(f"{label}: {len(tests)} native Play cases passed.", flush=True)
    return generated


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--editor",type=Path,required=True,help="Unity 6000.6.7f1 editor (.app on macOS)")
    parser.add_argument("--launcher", type=Path, default=CLI, help="Receiving project's registered Unity CLI Python launcher")
    parser.add_argument("--work",type=Path,required=True,help="New output directory inside the active task Cache")
    parser.add_argument("--seed",type=int,default=29411)
    parser.add_argument("--case", choices=("gallery", *APPLICATIONS, "channels", "inlets", *SUITES, "reuse", "all"), default="gallery",
                        help="Select a water family, the reuse comparison, or the full physical scenario matrix")
    parser.add_argument("--edition", choices=("original", "refined"), default="refined",
                        help="Select the original filmed rain or its later exposed streaks and contact response")
    parser.add_argument("--preview",action="store_true",help="Also write FluidMixer.gif; requires Pillow")
    parser.add_argument("--video", action="store_true", help="Also write the gallery and individual native case movies at 30 fps. Requires Pillow and ffmpeg")
    parser.add_argument("--library-only", action="store_true",
                        help="For --case all, export the full-rate subject movies, selector and reel with all physical and visual checks. Skip supplementary family previews.")
    parser.add_argument("--asset-prefix", help="Replay independently authored game-named variants, including every actor of the hit sequence.")
    args=parser.parse_args()
    if args.library_only:
        if args.case != "all":
            parser.error("--library-only requires --case all.")
        args.video = True
    if args.edition == "original" and args.case != "reuse" and not args.asset_prefix:
        args.asset_prefix = "Recorded"
    if args.asset_prefix is not None:
        if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]*", args.asset_prefix) or args.case == "reuse":
            parser.error("Choose an identifier prefix and one or more recorded visual cases.")
        from water_templates import CASES
        if any(len(args.asset_prefix + subject) > 60 for subject, (suite, _) in CASES.items()
               if args.case in ("all", suite)):
            parser.error("Prefix plus each selected case identifier must fit within 60 characters.")
    if args.video and not shutil.which("ffmpeg"):
        parser.error("Install ffmpeg on PATH to export the optional MP4.")
    media = args.preview or args.video
    if not (0 <= args.seed <= 0xffffffff):
        parser.error("Provide a 32-bit unsigned seed.")
    launcher = args.launcher.resolve()
    if not launcher.is_file():
        parser.error(f"Missing Unity CLI launcher at {launcher}. Select the registered launcher or install the full pack.")
    work = args.work.resolve()
    if work.exists():
        parser.error(f"Choose a new work directory. Existing data at {work} is preserved.")
    if args.case == "reuse" and media:
        parser.error("The reuse case records GPU state and resource counts. Select a visual case for previews or videos.")
    work.mkdir(parents=True)
    temp_parent = native_parent(work)
    with ThreadPoolExecutor(max_workers=2) as workers:
        futures = [workers.submit(lane,label,work,args.editor.resolve(),args.seed,args.case,launcher,temp_parent,args.edition,args.asset_prefix) for label in ("A","B")]
        first,second = [f.result() for f in futures]
    expected = json.loads(MANIFEST.read_text(encoding="utf-8"))["files"]
    if first != second or first != expected:
        raise RuntimeError("The imported asset bytes differ from the release manifest or the second receiver.")
    verify_and_export(args, work, first)


def verify_and_export(args, work, installed_files):
    """Check the recorded GPU state and camera captures, then write the selected media."""
    media = args.preview or args.video
    family_previews = media and not args.library_only
    if media:
        import fluid_preview
    captures = work / "A/captures"
    gallery = args.case in ("gallery", "all")
    application_cases = tuple(key for key in APPLICATIONS if args.case in ("all", key))
    device_record = "FreeJet/device.txt" if gallery else "device.txt"
    device = (captures / device_record).read_text().splitlines()
    report = {"editor": EDITOR_VERSION, "packages": PACKAGES, "seed": args.seed, "edition": args.edition, "files": installed_files,
              "receiverPaths": ["Assets/QinFluidMixer", "Assets/Effects/Liquid"], "graphics": device}
    if args.asset_prefix:
        named = [json.loads((work / label / "named-prefabs.json").read_text()) for label in ("A", "B")]
        if named[0] != named[1]:
            raise RuntimeError("The native receivers did not load the same game-named actors.")
        report.update(namedAssetPrefix=args.asset_prefix, loadedNamedPrefabsPerReceiver=named[0])
    if args.case in ("all", "reuse"):
        costs = [json.loads((work / lane / "captures/RockImpactReuse/costs.json").read_text())
                 for lane in ("A", "B")]
        if costs[0] != costs[1]:
            raise RuntimeError("Water reuse costs or matched GPU state differ between receivers")
        report["reuse"] = costs[0]
    if gallery:
        report["cases"] = review(work)
        if media:
            report["sideWaterline"] = fluid_preview.verify_side_waterline(captures)
            if args.seed == 29411:
                report["referenceSilhouetteIoU"] = fluid_preview.compare_silhouettes(captures)
        if family_previews:
            fluid_preview.export(captures, work / "FluidMixer.gif", work / "FluidMixer.mp4" if args.video else None)
    if application_cases:
        report["applications"] = {}
        for case in application_cases:
            app = review_application(work, case)
            if media:
                name = APPLICATIONS[case][1]
                app["visual"] = fluid_preview.review_application_images(captures / name, case, app["physics"])
            if family_previews:
                fluid_preview.export_application(captures / name, work / name, args.video)
                if case == "waterfall" and args.video:
                    valve_movie = work / "WaterfallValveControl.mp4"
                    fluid_preview.export_valve_comparison(captures / name, valve_movie)
                    app["valveVideo"] = valve_movie.name
            report["applications"][case] = app
    suites = selected_suites(args.case)
    if suites or args.case in ("channels", "inlets"):
        authored = [(work / label / "authored.json").read_bytes() for label in ("A", "B")]
        if authored[0] != authored[1]:
            raise RuntimeError("Independently authored prefabs have different identities or parameters")
        report["authoredVariants"] = json.loads(authored[0])
    if args.case in ("all", "channels"):
        report["channels"] = review_channels(work)
        if media:
            from channel_preview import export_channels, visible
            for name in CHANNELS:
                report["channels"][name]["visual"] = visible(captures, name)
        if family_previews:
            export_channels(captures, work, args.video)
    if args.case in ("all", "inlets"):
        report["inlets"] = review_inlets(work)
        if family_previews:
            from channel_preview import export_inlets
            export_inlets(captures, work, args.video)
    if suites:
        completed = {}
        report["families"] = {}
        for group, suite in suites.items():
            cases = suite.verify(work)
            if set(cases) != set(suite.CASES) or completed.keys() & cases.keys():
                raise RuntimeError(f"Unexpected or repeated native subjects in {group}.")
            report["families"][group] = list(suite.CASES)
            completed.update(cases)
        report["scenarios"] = completed
    if args.video:
        report["subjectVideos"] = fluid_preview.export_subject_videos(work, report)
        from water_gallery import write_gallery
        report["gallery"] = write_gallery(work, report).name
        if args.case == "all":
            from water_reel import write_reel
            report["matrixReel"] = write_reel(work, report["families"]).name
    (work / "FluidVerification.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n")
    print(f"Verified {args.case} in both receivers. Evidence: {work / 'FluidVerification.json'}")



if __name__ == "__main__":
    main()
