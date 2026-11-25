"""Run the illustrated image optimization examples in an isolated Unity project."""

import argparse
import hashlib
import json
import os
import shutil
import struct
import subprocess
import tarfile
from pathlib import Path
from xml.etree import ElementTree

from replay_cases import EDITOR, make_workspace, native_temp, run_unity

SKILL = Path(__file__).resolve().parents[1]
PACK = SKILL.parent
FIXTURE = SKILL / "assets/image-optimization"
METHODS = (
    "ControlArtExample.OneFrameFitsQuestPanelAndActions",
    "ImportAtlasExample.CrownKeepsItsWorldSizeAndTreePartsShareOnePackedPage",
    "WorldArtExample.GroundAndOakResizeFromSharedArtwork",
)


def prepare(project):
    assets = project / "Assets"
    scripts = assets / "Editor"
    inputs = {
        FIXTURE / "PulseTheme.tss": assets / "PulseTheme.tss",
        FIXTURE / "ImageExample.Tests.asmdef": scripts / "ImageExample.Tests.asmdef",
        PACK / "unity-assets-skill/assets/Editor/WorldRepeatSprite.cs": scripts / "WorldRepeatSprite.cs",
        PACK / "unity-ui-skill/assets/ResizableBacking.uss": assets / "ResizableBacking.uss",
    }
    for name in ("Packages/manifest.json", "Packages/packages-lock.json", "ProjectSettings/ProjectVersion.txt"):
        inputs[FIXTURE / name] = project / name
    art = FIXTURE / "Art"
    for source in art.rglob("*.png"):
        inputs[source] = assets / "ImageExamples" / source.relative_to(art)
    for source in (FIXTURE / "Editor").glob("*.cs"):
        inputs[source] = scripts / source.name

    hashes = {}
    for source, destination in inputs.items():
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
        hashes[source.relative_to(PACK).as_posix()] = hashlib.sha256(destination.read_bytes()).hexdigest()
    return hashes


def verify_prefab_package(package, project):
    assets = {f"Assets/ImageExamples/World/{source.name}" for source in (FIXTURE / "Art/World").glob("*.png")}
    assets.add("Assets/ImageExamples/Woodland.prefab")
    seen = set()
    with tarfile.open(package, "r:gz") as archive:
        for entry in archive.getmembers():
            if not entry.name.endswith("/pathname"):
                continue
            name = archive.extractfile(entry).read().decode("utf-8").rstrip("\x00\r\n")
            if name in {"Assets/ImageExamples", "Assets/ImageExamples/World"}:
                continue
            if name not in assets or name in seen:
                raise ValueError(f"Unexpected asset in woodland package: {name}")
            folder = entry.name.rsplit("/", 1)[0]
            for member, asset in (("asset", name), ("asset.meta", name + ".meta")):
                if archive.extractfile(folder + "/" + member).read() != (project / asset).read_bytes():
                    raise ValueError(f"Exported bytes differ for {asset}")
            seen.add(name)
    if seen != assets:
        raise ValueError(f"Missing woodland assets: {sorted(assets - seen)}")


def verify_cases(xml_path):
    result = ElementTree.parse(xml_path).getroot()
    tests = list(result.iter("test-case"))
    names = sorted(test.get("fullname", "") for test in tests)
    if names != sorted(METHODS):
        raise ValueError(f"Expected {list(METHODS)}, got {names}. See {xml_path}")
    failures = [(test.get("fullname"), test.get("result"), test.findtext("failure/message"))
                for test in tests if test.get("result") != "Passed"]
    if failures or result.get("result") != "Passed":
        raise ValueError(f"Unity reported {result.get('result')}: {failures}. See {xml_path}")
    return names


def capture(work, project, inputs, editor=None):
    output = work / "captures"
    output.mkdir()
    temporary = str(native_temp(work))
    environment = dict(os.environ, TMPDIR=temporary, TMP=temporary, TEMP=temporary,
                       UPM_CACHE_ROOT=str(work / "upm"), PYTHONDONTWRITEBYTECODE="1",
                       IMAGE_EXAMPLE_OUTPUT=str(output))
    xml_path = work / "results.xml"
    selection = [] if editor else ["--editor-version", EDITOR]
    arguments = [*selection, "--mode", "EditMode", "--output", str(xml_path), "--",
                 "-job-worker-count", "2", "-perfTestResults", str(work / "performance.json"),
                 "-logFile", str(work / "test.log")]
    run_unity("test", arguments, project, environment, work, editor)
    names = verify_cases(xml_path)
    measurements = json.loads((output / "ImportAtlas.json").read_text(encoding="utf-8"))
    if measurements.get("editorVersion") != EDITOR:
        raise ValueError(f"Expected Unity {EDITOR} in the native measurements.")
    expected_sizes = {
        "World.png": (840, 448), "UI.png": (800, 370), "UI-Canvas.png": (560, 136),
        "Import-Before.png": (256, 192), "Import-After.png": (256, 192),
        "Atlas.png": (measurements["atlasWidth"], measurements["atlasHeight"]),
    }
    hashes = {}
    for name in (*expected_sizes, "ImportAtlas.json", "Woodland.unitypackage"):
        data = (output / name).read_bytes()
        if name in expected_sizes:
            if (len(data) < 33 or data[:8] != b"\x89PNG\r\n\x1a\n" or data[12:16] != b"IHDR"
                    or struct.unpack(">II", data[16:24]) != expected_sizes[name]):
                raise ValueError(f"Invalid native frame dimensions for {name}.")
        hashes[name] = hashlib.sha256(data).hexdigest()
    verify_prefab_package(output / "Woodland.unitypackage", project)
    versions = json.loads((FIXTURE / "Packages/manifest.json").read_text(encoding="utf-8"))["dependencies"]
    locked = json.loads((project / "Packages/packages-lock.json").read_text(encoding="utf-8"))["dependencies"]
    for package, version in versions.items():
        if locked.get(package, {}).get("version") != version:
            raise ValueError(f"The receiver did not resolve {package} {version}.")
    receipt = {"unity": EDITOR, "activeTarget": measurements["activeTarget"], "packageVersions": versions,
               "cases": names, "sourceSha256": inputs, "captureSha256": hashes}
    (output / "Replay.json").write_text(json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"{len(names)} native examples passed. Captures and measurements: {output}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", required=True, type=Path, help="New child of a project Cache/temp-* run")
    parser.add_argument("--editor", type=Path, help=f"Explicit path to the Unity {EDITOR} Editor")
    args = parser.parse_args()
    try:
        work = make_workspace(args.work)
        project = work / "receiver"
        capture(work, project, prepare(project), args.editor)
    except (OSError, ValueError, RuntimeError, ElementTree.ParseError, tarfile.TarError, subprocess.TimeoutExpired) as error:
        parser.exit(1, f"{error}\n")


if __name__ == "__main__":
    main()
