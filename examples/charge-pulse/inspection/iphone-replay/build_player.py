"""Stage the Charge Pulse UI fixture in a disposable Unity project and build its iOS simulator Player."""
import argparse
import hashlib
import json
import os
import plistlib
from pathlib import Path
import shutil
import socket
import subprocess
import sys

from duo_hinge import owned_work

HERE = Path(__file__).resolve().parent
SAMPLE = HERE.parent.parent
FIXTURE = SAMPLE / "Assets/Editor/Tests/ResponsiveLayouts"
HELPERS = ["LayoutText", "LayoutFeedback", "ScreenFamilyViews", "ControllerSurface", "InventoryReorder"]

def command(args, work, env, log):
    with log.open("w") as output:
        subprocess.run(args, cwd=work, env=env, stdout=output, stderr=subprocess.STDOUT, check=True)

def stage(work):
    receiver = work / "receiver"
    assets = receiver / "Assets"
    resources = assets / "Resources/UiCases"
    folders = [resources, assets / "Editor", assets / "Plugins/iOS", receiver / "Packages", receiver / "ProjectSettings"]
    for path in folders:
        path.mkdir(parents=True, exist_ok=True)
    source = SAMPLE / "Packages/manifest.json"
    manifest = json.loads(source.read_text())
    manifest["dependencies"]["com.unity.modules.screencapture"] = "1.0.0"
    (receiver / "Packages/manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    shutil.copy2(SAMPLE / "ProjectSettings/ProjectVersion.txt", receiver / "ProjectSettings")
    shutil.copy2(SAMPLE / "Assets/Prefabs/ChargePulse/PulseTheme.tss", assets / "Resources")
    sources = {}
    def copy(path, directory):
        shutil.copy2(path, directory)
        sources[path.name] = hashlib.sha256((directory / path.name).read_bytes()).hexdigest()
    for suffix in ("*.uss", "*.uxml", "LayoutStrings.json"):
        for path in FIXTURE.glob(suffix):
            copy(path, resources)
    for helper in HELPERS:
        copy(FIXTURE / (helper + ".cs"), assets)
    for name, directory in [("IPhoneReplayPlayer.cs", assets), ("DeviceScreenBinder.cs", assets),
                            ("ReplayBuilder.cs", assets / "Editor"), ("ReplayDisplay.mm", assets / "Plugins/iOS")]:
        copy(HERE / name, directory)
    # These two fixtures keep their runtime views in the last nested class of their Editor test.
    # Stage the original view bodies in a partial binder without importing NUnit or UnityEditor.
    for fixture_name, view_name in (("AtlasCases", "AtlasDirectory"), ("WaypointCases", "WaypointMap")):
        fixture = FIXTURE / (fixture_name + ".cs")
        contents = fixture.read_text(encoding="utf-8")
        declaration = "        sealed class " + view_name
        ending = "    }\n}\n"
        if contents.count(declaration) != 1 or not contents.endswith(ending):
            raise ValueError(f"Cannot locate the terminal {view_name} view in {fixture}")
        view = contents[contents.index(declaration):-len(ending)]
        generated = assets / (view_name + ".cs")
        generated.write_text(
            "using System;\nusing System.Collections.Generic;\nusing UnityEngine;\nusing UnityEngine.UIElements;\n"
            "namespace Qin.UnitySamples.Tests\n{\n    sealed partial class DeviceScreenBinder\n    {\n"
            + view + ending, encoding="utf-8")
        sources[fixture.name] = hashlib.sha256(fixture.read_bytes()).hexdigest()
        sources[generated.name] = hashlib.sha256(generated.read_bytes()).hexdigest()
    (work / "sources.json").write_text(json.dumps(sources, indent=2, sort_keys=True) + "\n")
    return receiver

def file_hash(path):
    with path.open("rb") as stream:
        digest = hashlib.sha256()
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
        return digest.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", type=Path, required=True)
    parser.add_argument("--unity-cli", type=Path, required=True, help="Project-selected Unity CLI Python launcher")
    parser.add_argument("--export-only", action="store_true")
    parser.add_argument("--link-only", action="store_true")
    args = parser.parse_args()
    work = owned_work(args.work)
    tmp = work / "tmp"
    tmp.mkdir(exist_ok=True)
    st = tmp.stat()
    short = Path("/.vol") / str(st.st_dev) / str(st.st_ino)
    if not short.samefile(tmp):
        raise RuntimeError("The short native temporary path must resolve to the owned directory.")
    probe = short / "iphone-replay.sock"
    with socket.socket(socket.AF_UNIX) as test:
        test.bind(str(probe))
        if not (tmp / probe.name).exists():
            raise RuntimeError("Native compiler socket did not reach the owned directory.")
    probe.unlink()
    export = work / "xcode"
    env = dict(os.environ, TMPDIR=str(short)+"/", TMP=str(short), TEMP=str(short), UPM_CACHE_ROOT=str(work / "upm"),
               UI_REPLAY_EXPORT=str(export))
    if not args.link_only:
        receiver = stage(work)
        command([sys.executable, "-B", str(args.unity_cli.resolve()), "run", str(receiver), "--non-interactive",
                 "--no-pager", "--no-tail", "--timeout", "1800", "--log-file", str(work / "unity-export.log"),
                 "--", "-buildTarget", "iOS", "-executeMethod", "ReplayBuilder.Export"], work, env, work / "unity-cli.log")
    if not args.export_only:
        command(["xcodebuild", "-project", str(export / "Unity-iPhone.xcodeproj"), "-scheme", "Unity-iPhone",
                 "-configuration", "Release", "-sdk", "iphonesimulator", "-destination", "generic/platform=iOS Simulator",
                 "-derivedDataPath", str(work / "build"), "-jobs", "5", "ARCHS=arm64", "ONLY_ACTIVE_ARCH=YES",
                 "CODE_SIGNING_ALLOWED=NO", "build"], work, env, work / "xcodebuild.log")
        product = work / "build/Build/Products/Release-iphonesimulator/receiver.app"
        info = plistlib.loads((product / "Info.plist").read_bytes())
        binaries = (product / info["CFBundleExecutable"], product / "Frameworks/UnityFramework.framework/UnityFramework")
        manifest = {"sources": json.loads((work / "sources.json").read_text()),
                    "bundle": info["CFBundleIdentifier"], "sdk": info.get("DTSDKName"),
                    "binaries": {path.name: file_hash(path) for path in binaries}}
        (work / "build-manifest.json").write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n")

if __name__ == "__main__":
    main()
