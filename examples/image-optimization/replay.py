"""Run the illustrated image optimization examples in an isolated Unity project."""

import argparse
import hashlib
import json
import os
import shutil
import sys
import tarfile
from pathlib import Path
from xml.etree import ElementTree

EXAMPLE = Path(__file__).resolve().parent
ROOT = EXAMPLE.parents[1]
PACK = ROOT / "plugins/qin-unity-skills/skills"
sys.path.insert(0, str(PACK / "unity-performance-skill/scripts"))
from replay_cases import EDITOR, make_workspace, native_temp, run_unity


def prepare(project):
    assets = project / "Assets"
    scripts = assets / "Editor"
    scripts.mkdir(parents=True, exist_ok=True)
    sample = ROOT / "examples/charge-pulse"
    inputs = {
        sample / "Packages/manifest.json": project / "Packages/manifest.json",
        sample / "Packages/packages-lock.json": project / "Packages/packages-lock.json",
        sample / "ProjectSettings/ProjectVersion.txt": project / "ProjectSettings/ProjectVersion.txt",
        sample / "Assets/Prefabs/ChargePulse/PulseTheme.tss": assets / "PulseTheme.tss",
        PACK / "unity-assets-skill/assets/Editor/WorldRepeatSprite.cs": scripts / "WorldRepeatSprite.cs",
        PACK / "unity-ui-skill/assets/ResizableBacking.uss": assets / "ResizableBacking.uss",
    }
    for source in (EXAMPLE / "Art").rglob("*.png"):
        inputs[source] = assets / "ImageExamples" / source.relative_to(EXAMPLE / "Art")
    for source in (EXAMPLE / "Editor").glob("*.cs"):
        inputs[source] = scripts / source.name
    for source, destination in inputs.items():
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
    assembly = {"name": "ImageExample.Tests", "references": ["UnityEngine.UI"], "includePlatforms": ["Editor"],
                "optionalUnityReferences": ["TestAssemblies"]}
    (scripts / "ImageExample.Tests.asmdef").write_text(json.dumps(assembly), encoding="utf-8")
    return {source.relative_to(ROOT).as_posix(): hashlib.sha256(destination.read_bytes()).hexdigest()
            for source, destination in inputs.items()}


def verify_prefab_package(package, project):
    assets = {f"Assets/ImageExamples/World/{source.name}" for source in (EXAMPLE / "Art/World").glob("*.png")}
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


def capture(work, project, inputs):
    output = work / "captures"
    output.mkdir()
    temporary = str(native_temp(work))
    environment = dict(os.environ, TMPDIR=temporary, TMP=temporary, TEMP=temporary, UPM_CACHE_ROOT=str(work / "upm"), PYTHONDONTWRITEBYTECODE="1", IMAGE_EXAMPLE_OUTPUT=str(output))
    xml_path = work / "results.xml"
    arguments = ["--mode", "EditMode", "--output", str(xml_path), "--", "-job-worker-count", "2", "-logFile", str(work / "test.log")]
    run_unity("test", arguments, project, environment, work, None)
    result = ElementTree.parse(xml_path).getroot()
    tests = list(result.iter("test-case"))
    classes = {test.get("classname", "").split(".")[-1] for test in tests}
    if classes != {"WorldArtExample", "ControlArtExample", "ImportAtlasExample"} or result.get("result") != "Passed" or any(test.get("result") != "Passed" for test in tests):
        raise ValueError(f"Expected all three example classes to pass. See {xml_path}")
    captures = {}
    for name in ("World.png", "UI.png", "UI-Canvas.png", "Import-Before.png", "Import-After.png", "Atlas.png", "ImportAtlas.json", "Woodland.unitypackage"):
        content = (output / name).read_bytes()
        if name.endswith(".png") and not content.startswith(b"\x89PNG\r\n\x1a\n"):
            raise ValueError(f"Invalid Unity capture: {name}")
        captures[name] = hashlib.sha256(content).hexdigest()
    measurements = json.loads((output / "ImportAtlas.json").read_text(encoding="utf-8"))
    if measurements.get("editorVersion") != EDITOR:
        raise ValueError(f"Expected Unity {EDITOR} in the native measurements.")
    if not (project / "Assets/ImageExamples/Woodland.prefab").is_file():
        raise ValueError("The editable woodland prefab is missing.")
    verify_prefab_package(output / "Woodland.unitypackage", project)
    receipt = {"unity": measurements["editorVersion"], "activeTarget": measurements["activeTarget"], "cases": [test.get("fullname") for test in tests], "sourceSha256": inputs, "captureSha256": captures}
    (output / "Replay.json").write_text(json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"{len(tests)} native examples passed. Captures and measurements: {output}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", required=True, type=Path, help="New directory inside the project's allocated Cache/temp-* run")
    work = make_workspace(parser.parse_args().work)
    project = work / "receiver"
    capture(work, project, prepare(project))
