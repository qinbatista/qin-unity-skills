"""Check the optional bowl projection against a real DrivenTank field in an isolated Unity receiver."""
import argparse
import csv
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

from fluid_mixer import install
from replay_fluids import CLI, EDITOR_VERSION, HERE, PACKAGES, hidden_process_options, native_parent, run_command

OPTION = HERE.parent / "assets/bowl-projection"
CASE = "QinFluidReplay.BowlProjectionProbe"
COUNT = 75


def signatures(directory):
    return {p.relative_to(directory).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted(directory.rglob("*")) if p.is_file() and not p.name.startswith(".")}


def clone_warm(source, project):
    if not (source / ".qin-fluid-replay").is_file() or not (source / "Library/ArtifactDB").is_file():
        raise ValueError("--warm-receiver must be a completed disposable fluid replay project")
    version = (source / "ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8")
    if EDITOR_VERSION not in version or (source / "Temp/UnityLockfile").exists():
        raise ValueError("The warm receiver needs the matching Editor and a closed project")
    if sys.platform != "darwin":
        raise ValueError("APFS clone warming is available on macOS. Omit --warm-receiver for a fresh receiver.")
    for directory in ("Assets", "Packages", "ProjectSettings", "Library"):
        subprocess.run(["cp", "-cR", str(source / directory), str(project / directory)], check=True,
                       stdin=subprocess.DEVNULL, timeout=300, **hidden_process_options())


def collect(work, editor, launcher, warm):
    project = work / "project"
    project.mkdir()
    if warm:
        clone_warm(warm, project)
    else:
        for folder in ("Assets", "Packages", "ProjectSettings"):
            (project / folder).mkdir()
        (project / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: " + EDITOR_VERSION + "\n")
        (project / "Packages/manifest.json").write_text(json.dumps({"dependencies": PACKAGES}, indent=2))
    (project / ".qin-fluid-replay").write_text("Disposable bowl projection receiver.\n")
    relative = "Assets/Effects/Liquid" if (project / "Assets/Effects/Liquid/Channels/DrivenTank.prefab").exists() else "Assets/QinFluidMixer"
    install(project, relative)
    option = project / "Assets/QinBowlProjection"
    if option.exists():
        expected_meta = {p.name: p.read_bytes() for p in OPTION.glob("*.meta")}
        existing_meta = {p.name: p.read_bytes() for p in option.glob("*.meta")}
        if not warm or existing_meta != expected_meta or set(signatures(option)) != set(signatures(OPTION)):
            raise ValueError("The cloned receiver has an unrecognized optional bowl installation")
    shutil.copytree(OPTION, option, dirs_exist_ok=True)
    fixture = project / "Assets/Editor/FluidReplay"
    shutil.copytree(HERE / "fluid-replay", fixture, dirs_exist_ok=True)
    shutil.copyfile(HERE / "bowl-replay/QinBowlProjectionProbe.cs", fixture / "QinBowlProjectionProbe.cs")
    asmdef = fixture / "Qin.FluidReplay.Tests.asmdef"
    definition = json.loads(asmdef.read_text(encoding="utf-8"))
    definition["references"].append("Qin.UnityEffects.BowlProjection")
    asmdef.write_text(json.dumps(definition, indent=2) + "\n", encoding="utf-8")
    caps = work / "captures"
    caps.mkdir()
    for item in ("Preferences", "Caches"):
        (work / "home/Library" / item).mkdir(parents=True, exist_ok=True)
    common = ["--editor-path", str(editor), "--timeout", "1470", "--non-interactive", "--no-pager"]
    with tempfile.TemporaryDirectory(prefix="temp-f-", dir=native_parent(work)) as native:
        env = dict(os.environ, QIN_FLUID_OUTPUT=str(caps), QIN_FLUID_SEED="29411", QIN_FLUID_ASSET=relative,
                   CFFIXED_USER_HOME=str(work / "home"), UPM_CACHE_ROOT=str(work / "upm"),
                   DOTNET_PROCESSOR_COUNT="3", TMPDIR=native, TEMP=native, TMP=native)
        if not warm:
            run_command([sys.executable, "-B", str(launcher), "run", str(project), *common,
                         "--log-file", str(work / "import.log"), "--no-tail", "--", "-job-worker-count", "3",
                         "-executeMethod", "QinFluidReplay.Configure"], project, env, work / "import-cli.log")
        run_command([sys.executable, "-B", str(launcher), "test", str(project), *common, "--mode", "EditMode",
                     "--filter", CASE, "--output", str(work / "tests.xml"), "--", "-job-worker-count", "3",
                     "-logFile", str(work / "play.log")], project, env, work / "cli.log")
    cases = ET.parse(work / "tests.xml").findall(".//test-case")
    if len(cases) != 1 or cases[0].get("fullname") != CASE or cases[0].get("result") != "Passed":
        raise RuntimeError(f"The focused native test did not pass. See {work / 'tests.xml'}")
    expected = signatures(OPTION)
    if signatures(option) != expected:
        raise ValueError("Unity imported different optional asset bytes")
    return expected


def measure(caps):
    import numpy as np
    from PIL import Image

    def rgb(path):
        with Image.open(path) as image:
            return np.array(image.convert("RGB"), dtype=np.int16)

    with (caps / "fixture.csv").open(encoding="utf-8") as stream:
        fixture = next(csv.DictReader(stream))
    width, height = (int(fixture[k]) for k in ("width", "height"))
    cx, cy, rx, ry = (float(fixture[k]) for k in ("cx", "cy", "rx", "ry"))
    y, x = np.ogrid[:height, :width]
    sx = ((x + .5) / width - cx) / rx
    sy = (1 - (y + .5) / height - cy) / ry
    radius = np.sqrt(sx * sx + sy * sy)
    interior = radius < 1
    # Radial screen distance upper-bounds the nearest distance to the analytic ellipse.
    offset_px = np.hypot(sx * rx * width, sy * ry * height) * (1 - 1 / np.maximum(radius, 0.00001))
    outside = offset_px >= 1
    band = np.abs(sy) < .20
    dry = rgb(caps / "dry.png")
    states, wet, pngs = {}, {}, {}
    strict_exterior = exterior = 0
    largest_exterior_px = 0.0
    max_radius = 0.0
    for mode in ("zero", "driven", "opposite"):
        folder = caps / mode
        with (folder / "state.csv").open(encoding="utf-8") as stream:
            states[mode] = {int(r["frame"]): tuple(float(r[k]) for k in ("leftHeight", "rightHeight", "filmWeight"))
                            for r in csv.DictReader(stream)}
        names = sorted(folder.glob("*.png"))
        steps = list(range(COUNT)) if mode != "opposite" else list(range(0, COUNT, 3))
        if [f.stem for f in names] != [f"{i:04}" for i in steps]:
            raise ValueError("Incomplete camera frames for " + mode)
        wet[mode] = {}
        for frame, path in zip(steps, names):
            pixels = rgb(path)
            delta = np.max(abs(pixels - dry), axis=2)
            escaped = (delta > 0) & ~interior
            strict_exterior += int(np.count_nonzero(escaped))
            if np.any(escaped):
                largest_exterior_px = max(largest_exterior_px, float(offset_px[escaped].max()))
            exterior += int(np.count_nonzero((delta > 0) & outside))
            if frame % 3 != 0:
                continue
            mask = delta >= 3
            if np.any(mask):
                max_radius = max(max_radius, float(np.max(radius[mask])))
            hits = np.nonzero(mask & band)
            edge_balance = 0.
            if hits[0].size:
                left = ((int(hits[1].min()) + .5) / width - cx) / rx
                right = ((int(hits[1].max()) + .5) / width - cx) / rx
                edge_balance = left + right
            wet[mode][frame] = (int(mask.sum()), edge_balance)
            if frame in (9, 30, 48, 72):
                pngs[mode, frame] = pixels
    if largest_exterior_px > 1 / 256 or exterior or not np.array_equal(rgb(caps / "zero/0000.png"), dry):
        raise ValueError("Bowl registration changed the dry camera or pixels beyond the rim")
    if not np.array_equal(pngs["driven", 9], pngs["zero", 9]):
        raise ValueError("The same input must match at the same camera time")
    if not np.array_equal(pngs["zero", 30], pngs["zero", 72]):
        raise ValueError("The undriven, settled surface changed after its initial fade")
    if any((caps / "driven/field-0009.bin").read_bytes() != (caps / mode / "field-0009.bin").read_bytes()
           for mode in ("opposite", "zero")):
        raise ValueError("The pre-drive GPU presentation differs across identical inputs")

    def tilt(mode, tick):
        left, right, _ = states[mode][tick]
        return right - left

    raw_tilt = {}
    for mode in ("driven", "opposite", "zero"):
        field = np.fromfile(caps / mode / "field-0030.bin", dtype="<f4").reshape(4, 128, 4)
        elevation = field[2, :, 0]
        raw_tilt[mode] = float(elevation[-12:-4].mean() - elevation[4:12].mean())
        if abs(raw_tilt[mode] - tilt(mode, 30)) > 0.00001:
            raise ValueError(f"{mode} camera input disagrees with its GPU wall heights at frame 30")

    sample = max(states["driven"], key=lambda i: abs(tilt("driven", i) - tilt("opposite", i)))
    slope = tilt("driven", sample)
    reverse = tilt("opposite", sample)
    level = tilt("zero", sample)
    projected = wet["driven"][sample][1] - wet["opposite"][sample][1]
    if slope * reverse >= 0 or abs(slope) < .15 or abs(level) > .005 or (slope - reverse) * projected < .025:
        raise ValueError(f"Opposite solved wall heights must shift the measured wet edge: {(sample, slope, reverse, level, projected)}")
    zero = rgb(caps / "zero" / f"{sample:04}.png")
    driven = rgb(caps / "driven" / f"{sample:04}.png")
    different = int(np.count_nonzero(np.max(abs(zero - driven), axis=2) >= 5))
    if different < .035 * interior.sum() or wet["zero"][sample][0] < .20 * interior.sum():
        raise ValueError("The accepted tilt needs a visible water body and a distinct static-water control")

    # The direct and URP sorting-layer backdrops must place the two distinct specimens under the water.
    fallback = rgb(caps / "renderer-backdrop.png")
    fallback_delta = np.max(abs(fallback - dry), axis=2)
    if np.any(fallback_delta[outside]) or np.count_nonzero(fallback_delta >= 3) < .20 * interior.sum():
        raise ValueError("Unavailable or unregistered 2D Renderer sorting-layer backdrop")
    landmarks = {}
    for name, px, py, color_band in (("gold", -.36, .10, 0), ("turquoise", .33, -.17, 2)):
        roi = ((sx - px) ** 2 + (sy - py) ** 2) < .047 ** 2
        values = []
        for img in (dry, pngs["driven", 30], fallback):
            color = np.median(img[roi], axis=0)
            values.append(float(color[color_band] - color[2 - color_band]))
        if min(values) < 14:
            raise ValueError(f"The registered {name} specimen lost its color separation: {values}")
        landmarks[name] = values
    return {"nativeFrames": COUNT, "exteriorFramesChecked": 2 * COUNT + len(range(0, COUNT, 3)),
            "ellipse": [cx, cy, rx, ry], "outsideEllipseChangedPixelSamples": strict_exterior,
            "maximumChangedDistanceOutsideAnalyticEllipsePx": largest_exterior_px,
            "beyondOnePixelChangedSamples": exterior, "largestWetNormalizedRadius": max_radius, "comparisonFrame": sample,
            "clearedModesMatchDry": 3, "exteriorBackdropChallengeChangedPixelsAtFrame30": 0,
            "gpuFileRightMinusLeftHeightAtFrame30": raw_tilt,
            "rightMinusLeftHeight": {"driven": slope, "opposite": reverse, "zero": level},
            "signedWetEdgeDifferenceInRadii": projected, "interiorPixelsChangedFromZero": different,
            "stillWaterChangedPixelsFromDry": wet["zero"][sample][0], "sortingLayerChangedPixels": int(np.count_nonzero(fallback_delta >= 3)),
            "landmarkColorSeparationDryDrivenSortingLayer": landmarks}


def movie(work, caps):
    from PIL import Image, ImageDraw, ImageFont

    path = work / "BowlProjection.mp4"
    font = ImageFont.load_default(size=18)
    with Image.open(caps / "dry.png") as source:
        static = source.convert("RGB")
    with (caps / "fixture.csv").open(encoding="utf-8") as stream:
        record = next(csv.DictReader(stream))
    cx, cy, rx, ry = (float(record[k]) for k in ("cx", "cy", "rx", "ry"))
    w, h = static.size
    crop = tuple(round(q) for q in ((cx - 1.17 * rx)*w, (1-cy-1.17*ry)*h, (cx+1.17*rx)*w, (1-cy+1.17*ry)*h))
    size = (356, round(356 * (crop[3]-crop[1]) / (crop[2]-crop[0])))
    frame_size = (1100, (size[1] + 54 + 1) // 2 * 2)
    command = ["ffmpeg", "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{frame_size[0]}x{frame_size[1]}",
               "-r", "30", "-i", "pipe:0", "-an", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "21", "-preset", "fast",
               "-threads", "3", "-movflags", "+faststart", str(path)]
    with (work / "encode.log").open("wb") as log, subprocess.Popen(
            command, stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=log, **hidden_process_options()) as encoder:
        for frame in range(COUNT):
            plate = Image.new("RGB", frame_size, (24, 30, 31))
            labels = ImageDraw.Draw(plate)
            paths = (None, caps / "driven" / f"{frame:04}.png", caps / "zero" / f"{frame:04}.png")
            for column, (label, source) in enumerate(zip(("Dry pan", "Driven water", "Zero acceleration"), paths)):
                left = 8 + column * 366
                labels.text((left, 10), label, fill=(230, 238, 233), font=font)
                if source:
                    with Image.open(source) as image:
                        plate.paste(image.crop(crop).resize(size, Image.Resampling.LANCZOS), (left, 40))
                else:
                    plate.paste(static.crop(crop).resize(size, Image.Resampling.LANCZOS), (left, 40))
            encoder.stdin.write(plate.tobytes())
        encoder.stdin.close()
        if encoder.wait(timeout=120):
            raise RuntimeError(f"Could not encode {path}; see encode.log")
    subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-f", "null", "-"], check=True,
                   stdin=subprocess.DEVNULL, timeout=60, **hidden_process_options())
    raw = subprocess.check_output(["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=nb_frames,r_frame_rate",
                                   "-of", "json", str(path)], timeout=60, **hidden_process_options())
    stream = json.loads(raw)["streams"][0]
    if int(stream["nb_frames"]) != COUNT or stream["r_frame_rate"] != "30/1":
        raise ValueError("The control film must preserve the native 30 fps clock")
    return {"filename": path.name, "frames": COUNT, "fps": 30, "bytes": path.stat().st_size,
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}


def finish(work, imported, launch):
    caps = work / "captures/BowlProjection"
    project = work / "project"
    fixture = project / "Assets/Editor/FluidReplay/QinBowlProjectionProbe.cs"
    source = (HERE / "bowl-replay/QinBowlProjectionProbe.cs").read_bytes()
    if fixture.read_bytes() != source:
        raise ValueError("The receiver did not compile the delivered bowl fixture source")
    assembly = project / "Library/ScriptAssemblies/Qin.FluidReplay.Tests.dll"
    report = {"device": (caps.parent / "device.txt").read_text().splitlines(), "nativeCase": CASE,
              "launch": launch, "seed": 29411, "fixtureSha256": hashlib.sha256(source).hexdigest(),
              "compiledFixtureSha256": hashlib.sha256(assembly.read_bytes()).hexdigest(),
              "optionalSourceSha256": imported, "measurement": measure(caps), "film": movie(work, caps)}
    receipt = work / "BowlProjectionVerification.json"
    receipt.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", type=Path, required=True, help="New directory inside the active task Cache")
    parser.add_argument("--editor", type=Path, required=True, help="Unity 6000.6.7f1 Editor")
    parser.add_argument("--launcher", type=Path, default=CLI, help="Registered Unity CLI")
    parser.add_argument("--warm-receiver", type=Path, help="Optional closed, fully imported APFS core receiver to clone")
    args = parser.parse_args()
    work = args.work.resolve()
    if work.exists() or not any(p.name.startswith("temp-") and p.parent.name == "Cache" for p in work.parents):
        parser.error("Choose a new directory beneath an allocated Cache/temp-* run")
    for executable in ("ffmpeg", "ffprobe"):
        if not shutil.which(executable):
            parser.error(f"{executable} is required for film encoding and decoding")
    work.mkdir(parents=True)
    imported = collect(work, args.editor.resolve(), args.launcher.resolve(), args.warm_receiver.resolve() if args.warm_receiver else None)
    print(finish(work, imported, "Registered Unity CLI, EditMode test entering Play"))


if __name__ == "__main__":
    main()
