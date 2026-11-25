"""Reproduce the five-metre side-view WideCloudburst cast and its matched controls."""
import argparse
import csv
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys
import tempfile
import xml.etree.ElementTree as ET

from compose_water import compose
from fluid_mixer import MANIFEST, install
from fluid_replay_checks import read_state
from replay_fluids import CLI, EDITOR_VERSION, HERE, PACKAGES, digest, native_parent, run_command
from water_limits import BRIEFS

NAME = "WideCloudburst"
CASE = "QinFluidReplay.WideCloudburstProbe"
TEMPLATE = "Assets/QinFluidMixer"
PROBE = HERE / "cloudburst-replay/QinWideCloudburstProbe.cs"
MODES = ("normal", "no-waves", "legacy")
FRAME_COUNT = 114


def stage(work):
    project = work / "project"
    for folder in ("Assets", "Packages", "ProjectSettings"):
        (project / folder).mkdir(parents=True)
    (project / ".qin-fluid-replay").write_text("Disposable side-view cloudburst receiver.\n", encoding="utf-8")
    (project / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: " + EDITOR_VERSION + "\n", encoding="utf-8")
    (project / "Packages/manifest.json").write_text(json.dumps({"dependencies": PACKAGES}, indent=2), encoding="utf-8")
    install(project, TEMPLATE)
    profile, parameters = BRIEFS[NAME]
    compose(project, TEMPLATE, "Assets/GameWater", NAME, profile, parameters)
    fixture = project / "Assets/Editor/FluidReplay"
    shutil.copytree(HERE / "fluid-replay", fixture)
    shutil.copyfile(PROBE, fixture / PROBE.name)
    (work / "captures").mkdir()
    for directory in ("home/Library/Preferences", "home/Library/Caches", "upm"):
        (work / directory).mkdir(parents=True)


def run_unity(work, editor, launcher, *, configure=True):
    project = work / "project"
    common = ["--editor-path", str(editor), "--timeout", "1200", "--non-interactive", "--no-pager"]
    with tempfile.TemporaryDirectory(prefix="temp-f-", dir=native_parent(work)) as native:
        env = dict(os.environ, QIN_FLUID_OUTPUT=str(work / "captures"), QIN_FLUID_SEED="29411",
                   QIN_FLUID_ASSET=TEMPLATE, CFFIXED_USER_HOME=str(work / "home"), UPM_CACHE_ROOT=str(work / "upm"),
                   TMPDIR=native, TEMP=native, TMP=native, DOTNET_PROCESSOR_COUNT="3")
        if configure:
            run_command([sys.executable, "-B", str(launcher), "run", str(project), *common,
                         "--log-file", str(work / "import.log"), "--no-tail", "--", "-job-worker-count", "3",
                         "-executeMethod", "QinFluidReplay.Configure"], project, env, work / "import-cli.log")
            if "QIN_FLUID_RECEIVER_READY" not in (work / "import.log").read_text(errors="replace"):
                raise RuntimeError(f"URP receiver setup did not complete. See {work / 'import.log'}")
        run_command([sys.executable, "-B", str(launcher), "test", str(project), *common, "--mode", "EditMode",
                     "--filter", CASE, "--output", str(work / "tests.xml"), "--", "-job-worker-count", "3",
                     "-logFile", str(work / "play.log")], project, env, work / "test-cli.log")
    cases = ET.parse(work / "tests.xml").findall(".//test-case")
    if len(cases) != 1 or cases[0].get("fullname") != CASE or cases[0].get("result") != "Passed":
        raise RuntimeError(f"Expected one passing {CASE} test. See {work / 'tests.xml'}")


def verify(work):
    caps = work / "captures" / NAME
    dry = (caps / "dry.png").read_bytes()
    clock = [f"{i:04d}.png" for i in range(FRAME_COUNT)]
    film_hashes = {}
    for mode in MODES:
        images = sorted((caps / mode).glob("*.png"))
        if [p.name for p in images] != clock or any(p.read_bytes() != dry for p in (images[0], images[-1])):
            raise ValueError(f"Incomplete {mode} take or uncleared cast")
        sequence = hashlib.sha256()
        for image in images:
            sequence.update(bytes.fromhex(digest(image)))
        film_hashes[mode] = sequence.hexdigest()
    if len(set(film_hashes.values())) != len(MODES):
        raise ValueError("All three takes must be visibly distinct")

    samples = sorted((caps / "normal").glob("state-*.bin"))
    coordinates = {}
    contact_frames = {}
    max_height = 0.0
    first_wave = None
    first_wave_correction = None
    compared = 0
    for path in samples:
        frame = int(path.stem.split("-")[-1])
        driven, waves = read_state(path)
        still, quiet = read_state(caps / "no-waves" / path.name)
        original, original_waves = read_state(caps / "legacy" / path.name)
        if first_wave_correction is None and waves != original_waves:
            first_wave_correction = frame
        if any(quiet):
            raise ValueError(f"The no-wave control generated waves at frame {frame}")
        height = max(map(abs, waves))
        if height > 0 and first_wave is None:
            first_wave = frame
        max_height = max(max_height, height)
        for index, (parcel, control) in enumerate(zip(driven, still)):
            if parcel[3] < 0 or index in contact_frames:
                continue
            if parcel[1] < .14:
                contact_frames[index] = frame
            else:
                if parcel != control or parcel != original[index]:
                    raise ValueError(f"A surface option changed free flight at frame {frame}, drop {index}")
                compared += 1
                coordinates.setdefault(index, parcel[0])
    if len(coordinates) != 160 or len(contact_frames) != 160 or first_wave is None:
        raise ValueError("The full shower did not reach the receiving water")
    width = max(coordinates.values()) - min(coordinates.values())
    if width < 4.0:
        raise ValueError("The five-metre inlet collapsed to a localized emitter")

    with (caps / "normal/contacts.csv").open(newline="", encoding="utf-8-sig") as handle:
        impacts = list(csv.DictReader(handle))
    with (caps / "no-waves/contacts.csv").open(newline="", encoding="utf-8-sig") as handle:
        if list(csv.DictReader(handle)):
            raise ValueError("The no-wave take contains impact splashes")
    if {int(hit["parcel"]) for hit in impacts} != set(range(160)) or len(impacts) != 160:
        raise ValueError("Each emitted drop must register its physical water contact")
    times = [float(hit["impactSeconds"]) for hit in impacts]
    if abs(min(times) - first_wave / 30) > 1 / 30:
        raise ValueError("The first splash did not coincide with the first surface response")
    if first_wave_correction is None or first_wave_correction <= first_wave:
        raise ValueError("Expected the missing mid-shower contact to change the original wave field")
    first_camera_response = next((frame for frame, name in enumerate(clock)
                                  if (caps / "normal" / name).read_bytes() != (caps / "no-waves" / name).read_bytes()), None)
    if first_camera_response != first_wave:
        raise ValueError("The camera response must begin on the frame of the first measured water impact")

    project = work / "project"
    asset = project / "Assets/GameWater" / NAME / f"{NAME}.asset"
    device = (caps.parent / "device.txt").read_text().splitlines()
    expected_files = json.loads(MANIFEST.read_text(encoding="utf-8"))["files"]
    if any(digest(project / TEMPLATE / name) != checksum for name, checksum in expected_files.items()):
        raise ValueError("The captured template differs from the distributed fluid package")
    fixture = project / "Assets/Editor/FluidReplay" / PROBE.name
    if digest(fixture) != digest(PROBE):
        raise ValueError("The captured test differs from the distributed probe")
    return {"case": CASE, "device": device, "seed": 29411, "framesPerMode": FRAME_COUNT, "fps": 30,
            "camera": {"dimensions": [960, 540], "orthographicSizeM": 1.64, "worldCentreXY": [0, 1.10]},
            "modeSha256OfOrderedPngDigests": film_hashes, "recipeSha256": digest(asset),
            "fixtureSha256": digest(PROBE), "count": len(coordinates), "sourceWidthM": 5,
            "templateManifestSha256": digest(MANIFEST), "recordedImpacts": len(impacts),
            "firstImpactSeconds": min(times), "lastImpactSeconds": max(times),
            "firstObservedBirthSpanM": width, "firstNonzeroWaveFrame": first_wave,
            "identicalPreImpactCameraFrames": first_camera_response,
            "matchedPrecontactParcelSamples": compared, "peakAbsoluteWaveHeightM": max_height,
            "activeStateSamplesPerMode": len(samples), "firstCorrectedWaveFieldFrame": first_wave_correction,
            "allNoWaveHeightsZero": True, "allModesClear": True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", type=Path, required=True, help="New directory beneath the allocated Cache/temp-*")
    parser.add_argument("--editor", type=Path, required=True, help="Unity 6000.6.7f1 Editor")
    parser.add_argument("--launcher", type=Path, default=CLI, help="Registered Unity CLI")
    args = parser.parse_args()
    work, editor, launcher = args.work.resolve(), args.editor.resolve(), args.launcher.resolve()
    if not any(p.parent.name == "Cache" and p.name.startswith("temp-") for p in work.parents):
        parser.error("--work must be a new child of the allocated task cache")
    if not editor.exists() or not launcher.is_file():
        parser.error("Select the installed Editor and registered Unity launcher")
    work.mkdir(parents=True, exist_ok=False)
    stage(work)
    run_unity(work, editor, launcher)
    output = work / "CloudburstVerification.json"
    output.write_text(json.dumps(verify(work), indent=2) + "\n", encoding="utf-8")
    print(output)


if __name__ == "__main__":
    main()
