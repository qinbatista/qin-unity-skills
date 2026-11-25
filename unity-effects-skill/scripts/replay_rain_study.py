"""Capture paired native rainfall with the installed RainPond solver and a fixed camera."""
import argparse
import csv
import hashlib
import json
import math
import os
from pathlib import Path
import shutil
import sys
import tempfile

from replay_fluids import CLI, EDITOR_VERSION, native_parent, run_command

HERE = Path(__file__).resolve().parent
ASSETS = HERE.parent / "assets"
STAMP = "Disposable focused native rain receiver.\n"
WIDTH, HEIGHT, FPS, FRAMES = 960, 540, 30, 270


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def stage(project):
    for folder in ("Assets/Editor", "Assets/Pond", "Assets/RainLook", "Packages", "ProjectSettings"):
        (project / folder).mkdir(parents=True, exist_ok=True)
    (project / ".qin-rain-study").write_text(STAMP, encoding="utf-8")
    (project / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: " + EDITOR_VERSION + "\n", encoding="utf-8")
    (project / "Packages/manifest.json").write_text(json.dumps({"dependencies": {"com.unity.modules.imageconversion": "1.0.0"}}, indent=2), encoding="utf-8")
    groups = [
        (ASSETS / "fluid-mixer/TopDown", project / "Assets/Pond", ("TopDownWater2D.cs", "TopDownWater.compute", "WaterDomain.hlsl")),
        (ASSETS / "rain-study", project / "Assets/RainLook",
         ("RainStudy.shader", "RainStudy.shader.meta", "StillPool.jpg", "StillPool.jpg.meta", "ShoreMask.png", "ShoreMask.png.meta")),
        (HERE / "rain-study", project / "Assets/Editor", ("RainDropTimeline.cs", "NativeRainStudy.cs")),
    ]
    hashes = {}
    for source, target, names in groups:
        for name in names:
            original, staged = source / name, target / name
            if not staged.is_file() or sha(staged) != sha(original):
                shutil.copyfile(original, staged)
            hashes[staged.relative_to(project).as_posix()] = sha(staged)
    return hashes


def read_csv(path):
    with path.open(encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def inspect_images(output, first, first_frame):
    import numpy as np
    from PIL import Image, ImageFilter

    def rgb(path):
        with Image.open(path) as image:
            if image.size != (WIDTH, HEIGHT):
                raise ValueError(f"Unexpected capture dimensions at {path}")
            return np.array(image.convert("RGB"))

    with Image.open(ASSETS / "rain-study/ShoreMask.png") as matte:
        mask = matte.convert("L").resize((WIDTH, HEIGHT), Image.Resampling.BILINEAR)
    # Omit the one-pixel interpolation boundary between Unity and Pillow.
    dry = np.asarray(mask.filter(ImageFilter.MaxFilter(3))) == 0
    if not np.any(dry):
        raise ValueError("The water matte has no testable shoreline")
    rows, columns = np.ogrid[:HEIGHT, :WIDTH]
    radius = np.hypot(((columns + .5) / WIDTH - float(first["groundU"])) * 6.08,
                      (1 - (rows + .5) / HEIGHT - float(first["groundV"])) * 4.5)
    # The first, strongest drop uses a 5 cm wavelength. The packet envelope
    # follows the group velocity of the capillary–gravity dispersion relation.
    k = 2 * math.pi / .050
    omega = math.sqrt(9.81 * k + .000073 * k**3)
    group_speed = (9.81 + 3 * .000073 * k**2) / (2 * omega)
    probes = []
    for frame in range(FRAMES):
        name = f"{frame:04d}.png"
        normal = rgb(output / "normal" / name)
        control = rgb(output / "control" / name)
        if frame < first_frame and not np.array_equal(normal, control):
            raise ValueError(f"Render diverged before the first landing at frame {frame}")
        if not np.array_equal(normal[dry], control[dry]):
            raise ValueError(f"Water moved the solid shoreline at frame {frame}")
        if frame not in (36, 38, 42, 45, 49, FRAMES - 1):
            continue
        coarse = rgb(output / "coarseOnly" / name)
        if frame == FRAMES - 1:
            if not np.array_equal(coarse, normal):
                raise ValueError("Short waves persisted after the final packet retired")
            continue
        delta = np.max(np.abs(normal.astype(np.int16) - coarse.astype(np.int16)), axis=2)
        age = (frame - first_frame) / FPS
        visible = int(np.count_nonzero(delta >= 8))
        if visible < 45:
            raise ValueError(f"The first droplet has no readable post-crown ripples at frame {frame}")
        measured = float(np.sum(delta * radius) / np.sum(delta))
        expected = .022 + group_speed * age
        if abs(measured - expected) > .045 or (probes and measured <= probes[-1]["contrastRadiusM"]):
            raise ValueError(f"Unexpected capillary packet travel at frame {frame}")
        probes.append({"frame": frame, "ageS": age, "contrastRadiusM": measured,
                       "predictedEnvelopeRadiusM": expected, "pixelsAtLeast8of255": visible})
    return {"preImpactIdenticalFrames": first_frame, "checkedShorePixelsPerFrame": int(dry.sum()),
            "shoreDifferences": 0, "firstPacketGroupSpeedMps": group_speed, "firstPacket": probes,
            "finalPacketDifferencePixels": 0}


def verify(output, inputs):
    contacts = read_csv(output / "contacts.csv")
    field = read_csv(output / "field.csv")
    if len(field) != FRAMES or not contacts:
        raise ValueError("Incomplete native rain data")
    if any(not all(math.isfinite(float(value)) for value in row.values()) for row in field + contacts):
        raise ValueError("Non-finite native rain data")
    for name in ("normal", "control"):
        files = sorted((output / name).glob("*.png"))
        if [p.stem for p in files] != [f"{i:04d}" for i in range(FRAMES)]:
            raise ValueError(f"Incomplete native {name} sequence")
    first = contacts[0]
    first_tick = int(first["impactTick"])
    if first_tick != 108 or int(field[-1]["landed"]) != len(contacts):
        raise ValueError("Unexpected landing schedule")
    for drop in contacts:
        flight = float(drop["impactS"]) - float(drop["birthS"])
        expected = float(drop["initialDownMps"]) + 9.81 * flight
        if abs(expected - float(drop["impactDownMps"])) > .0001:
            raise ValueError("Invalid ballistic trajectory")
    if any(float(row["controlMaxM"]) != 0 for row in field):
        raise ValueError("Disabled impacts displaced the control surface")
    first_frame = first_tick * FPS // 120
    if any(float(row["normalMaxM"]) != 0 for row in field[:first_frame]):
        raise ValueError("Unexpected water disturbance before first contact")
    radii = [float(field[f]["firstDropEnergyRadiusM"]) for f in (first_frame, first_frame + 3, first_frame + 8, first_frame + 14)]
    if not all(b > a for a, b in zip(radii, radii[1:])) or float(field[first_frame]["firstDropCenterM"]) >= 0:
        raise ValueError(f"First landing produced no expanding cavity response: {radii}")
    last = max(int(d["impactTick"]) for d in contacts)
    start = (last + 3) // 4
    tail = float(field[start]["sumHeightSquaredM2"])
    residual = float(field[-1]["sumHeightSquaredM2"])
    if any(int(row["airborne"]) for row in field[start:]) or int(field[-1]["recent"]) or not 0 < residual < tail * .35:
        raise ValueError("Airborne or surface retirement did not match the clock")
    peak_air = max(int(row["airborne"]) for row in field)
    peak_recent = max(int(row["recent"]) for row in field)
    if not 0 < max(peak_air, peak_recent) <= 64:
        raise ValueError("Rain shader buffer capacity exceeded")
    receipt = {"editorDevice": (output / "device.txt").read_text().splitlines(), "dimensions": [WIDTH, HEIGHT],
               "fps": FPS, "framesPerTake": FRAMES, "solverHz": 120, "seed": 29411,
               "nativeInputSha256": inputs, "contacts": len(contacts), "firstImpactTick": first_tick,
               "sourceCutoffSeconds": 6.15, "lastImpactTick": last,
               "firstCavityDepthM": float(field[first_frame]["firstDropCenterM"]),
               "firstHeightEnergyRadiusM": dict(zip((str(i) for i in (0, 3, 8, 14)), radii)),
               "peakAirborne": peak_air, "peakRetainedContacts": peak_recent,
               "tailFirstFrame": start, "tailSumHeightSquaredM2": tail,
               "finalSumHeightSquaredM2": residual, "controlMaxM": 0,
               "nativeTableSha256": {name: sha(output / name) for name in ("contacts.csv", "field.csv")},
               "opticalReadback": inspect_images(output, first, first_frame)}
    (output / "RainVerification.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(output / "RainVerification.json")


def render(work, project, output, editor, launcher):
    inputs = stage(project)
    output.mkdir()
    for folder in ("Preferences", "Caches"):
        (work / "home/Library" / folder).mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="temp-f-", dir=native_parent(work)) as scratch:
        env = dict(os.environ, TMP=scratch, TEMP=scratch, TMPDIR=scratch, QIN_RAIN_OUTPUT=str(output),
                   CFFIXED_USER_HOME=str(work / "home"), UPM_CACHE_ROOT=str(work / "upm"), DOTNET_PROCESSOR_COUNT="3")
        run_command([sys.executable, "-B", str(launcher), "run", str(project), "--editor-path", str(editor),
                     "--timeout", "1000", "--non-interactive", "--no-pager", "--log-file", str(output / "editor.log"),
                     "--no-tail", "--", "-job-worker-count", "3", "-executeMethod", "NativeRainStudy.Render"],
                    project, env, output / "launcher.log")
    log = (output / "editor.log").read_text(errors="replace")
    if "QIN_NATIVE_RAIN_CAPTURED" not in log or "Assertion failed" in log:
        raise RuntimeError(f"Native rendering did not finish cleanly. See {output / 'editor.log'}")
    verify(output, inputs)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", type=Path, required=True, help="A new isolated directory under your allocated Cache/temp-*")
    parser.add_argument("--editor", type=Path, required=True, help="Unity 6000.6.7f1 Editor")
    parser.add_argument("--launcher", type=Path, default=CLI, help="Receiving project's registered Unity CLI")
    parser.add_argument("--take", default="frames", help="New result folder in the same disposable receiver when tuning optics")
    args = parser.parse_args()
    work, editor, launcher = args.work.resolve(), args.editor.resolve(), args.launcher.resolve()
    if not any(p.name.startswith("temp-") and p.parent.name == "Cache" for p in work.parents):
        parser.error("--work must be a child of the allocated project Cache/temp-* directory")
    if not editor.exists() or not launcher.is_file():
        parser.error("Select the installed editor and registered launcher")
    if not args.take.isidentifier() or args.take.casefold() in {"project", "home", "upm"}:
        parser.error("--take must name a new results directory")
    project, output = work / "project", work / args.take
    if output.exists() or output.is_symlink():
        parser.error("The take already exists")
    stamp = project / ".qin-rain-study"
    try:
        work.mkdir(parents=True)
    except FileExistsError:
        if (project.is_symlink() or stamp.is_symlink() or not stamp.is_file() or
                stamp.read_text(encoding="utf-8") != STAMP or (project / "Temp/UnityLockfile").exists()):
            parser.error("Use a new work directory or the closed marked rain receiver")
    lease = work / ".rendering"
    try:
        lease.touch(exist_ok=False)
    except FileExistsError:
        parser.error("This rain receiver already has a rendering lease")
    try:
        render(work, project, output, editor, launcher)
    finally:
        lease.unlink()


if __name__ == "__main__":
    main()
