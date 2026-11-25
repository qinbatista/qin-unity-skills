"""Create a neutral landscape preview from one receiver's real Unity frames (optional Pillow dependency)."""
from contextlib import ExitStack
from pathlib import Path
import os
import shutil
import subprocess
import sys
import tempfile
from PIL import Image, ImageChops, ImageDraw, ImageFont

from fluid_application_checks import APPLICATIONS
from fluid_replay_checks import FORMS

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "unity-code-skill/scripts"))
from hidden_process import hidden_process_options

LANDMARK_FRAMES = (12, 24, 38, 47, 57)
MASK_SIZE = (96, 54)
REFERENCE = Path(__file__).resolve().parents[1] / "assets/fluid-silhouettes.png"

GROUPS = (
    ("Surface and falling water",(("PlanarRipples","Planar ripples"),("FallingMass","Falling mass"))),
    ("The same jet at a boundary",(("FreeJet","Free jet"),("RockImpact","Sphere contact"))),
    ("Contact and continuous flow",(("OpposedJets","Opposing jets"),("Waterfall","Waterfall"))),
    ("Rotating water",(("Vortex","Converging spiral"),)),
)


def font(size):
    for family in ("DejaVuSans.ttf","Arial.ttf"):
        try:
            return ImageFont.truetype(family,size)
        except OSError:
            continue
    return ImageFont.load_default()


def bounds(folder):
    with Image.open(folder/"dry.png") as dry:
        boxes=[]
        for p in sorted((folder/"frames").glob("*.png"))[::2]:
            with Image.open(p) as image:
                diff=ImageChops.difference(dry.convert("RGB"),image.convert("RGB")).convert("L")
                box=diff.point(lambda x: 255 if x>3 else 0).getbbox()
                if box: boxes.append(box)
    return (min(b[0] for b in boxes)-12,min(b[1] for b in boxes)-12,
            max(b[2] for b in boxes)+12,max(b[3] for b in boxes)+12)


def silhouette(folder, frame):
    with Image.open(folder / "dry.png") as dry, Image.open(folder / "frames" / f"{frame:04d}.png") as wet:
        difference = ImageChops.difference(dry.convert("RGB"), wet.convert("RGB")).convert("L")
    mask = difference.point(lambda intensity: 255 if intensity >= 10 else 0)
    return mask.resize(MASK_SIZE, Image.Resampling.BOX)


def compare_silhouettes(captures):
    """Check motion against a frozen independent reference without including the backdrop in the score."""
    width, height = MASK_SIZE
    scores = {}
    with Image.open(REFERENCE) as atlas:
        if atlas.mode != "L" or atlas.size != (width * len(LANDMARK_FRAMES), height * len(FORMS)):
            raise ValueError("Incomplete fluid reference sheet")
        for row, form in enumerate(FORMS):
            scores[form] = {}
            for column, frame in enumerate(LANDMARK_FRAMES):
                x, y = column * width, row * height
                expected = atlas.crop((x, y, x + width, y + height)).tobytes()
                observed = silhouette(captures / form, frame).tobytes()
                pairs = list(zip(expected, observed))
                union = sum(max(a, b) for a, b in pairs)
                score = sum(min(a, b) for a, b in pairs) / max(union, 1)
                if score < 0.85:
                    raise ValueError(f"{form} frame {frame} silhouette overlap {score:.3f} falls below 0.85")
                scores[form][str(frame)] = round(score, 5)
    return scores


def pages(captures, stride):
    title, small = font(30), font(20)
    for label, items in GROUPS:
        boxes = [bounds(captures / name) for name, _ in items]
        if items[0][0] == "FreeJet":
            boxes = [(min(b[0] for b in boxes), min(b[1] for b in boxes),
                      max(b[2] for b in boxes), max(b[3] for b in boxes))] * 2
        for tick in range(0, 84, stride):
            picture = Image.new("RGB", (832, 468), "#0b181f")
            draw = ImageDraw.Draw(picture)
            draw.text((24, 19), label, font=title, fill="#e7eff1")
            for panel, ((name, caption), box) in enumerate(zip(items, boxes)):
                width = 386 if len(items) == 2 else 786
                left = 24 + panel * 400
                cx, cy = (box[0] + box[2]) / 2, (box[1] + box[3]) / 2
                scale = min(width / (box[2] - box[0]), 322 / (box[3] - box[1]), 2.6)
                w, h = width / scale, 322 / scale
                crop = (round(cx - w / 2), round(cy - h / 2), round(cx + w / 2), round(cy + h / 2))
                with Image.open(captures / name / "frames" / f"{tick:04d}.png") as native:
                    panel_image = native.crop(crop).resize((width, 322), Image.Resampling.LANCZOS)
                picture.paste(panel_image, (left, 77))
                draw.text((left, 417), caption, font=small, fill="#bdd1d8")
            yield tick, picture


def export(captures, output, video=None):
    frames = []
    stride = 1 if video else 2
    with ExitStack() as stack:
        if video:
            encoder = shutil.which("ffmpeg")
            if encoder is None:
                raise ValueError("Install ffmpeg on PATH to export the optional 30 fps MP4.")
            options = {"creationflags": subprocess.CREATE_NO_WINDOW} if os.name == "nt" else {}
            process = stack.enter_context(subprocess.Popen(
                [encoder, "-nostdin", "-loglevel", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24",
                 "-s", "832x468", "-framerate", "30", "-i", "pipe:0", "-an", "-c:v", "libx264",
                 "-threads", "3", "-crf", "16", "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(video)],
                stdin=subprocess.PIPE, **options))
            stream = stack.enter_context(process.stdin)
        for tick, picture in pages(Path(captures), stride):
            if video:
                stream.write(picture.tobytes())
            if tick % 2 == 0:
                frames.append(picture)
    if video and process.returncode:
        raise ValueError(f"ffmpeg exited with {process.returncode} while writing {video}")

    # A shared palette keeps the static studio from shimmering or inflating the animation.
    samples = frames[::6]
    swatches = Image.new("RGB", (208, len(samples) * 117))
    for index, frame in enumerate(samples):
        swatches.paste(frame.resize((208, 117), Image.Resampling.BOX), (0, index * 117))
    palette = swatches.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    encoded = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    # GIF durations use centiseconds. Repeat 70/60/70 ms for a true 15 fps mean.
    encoded[0].save(output, save_all=True, append_images=encoded[1:],
                    duration=[70, 60, 70] * (len(encoded) // 3), loop=0, disposal=1, optimize=False)


def frame_mask(first, second, minimum=10):
    delta = ImageChops.difference(first, second).convert("L")
    return delta.point(lambda value: 255 if value >= minimum else 0)


def application_frame(folder, tick, mode="normal"):
    with Image.open(folder / mode / f"{tick:04d}.png") as picture:
        return picture.convert("RGB")


def mask_area(mask):
    return mask.histogram()[255]


def verify_side_waterline(captures):
    folder = captures / "PlanarRipples"
    with Image.open(folder / "dry.png") as picture:
        dry = picture.convert("RGB")
    spans = []
    for tick in (12, 24, 38, 47, 57):
        with Image.open(folder / "frames" / f"{tick:04d}.png") as picture:
            mask = frame_mask(picture.convert("RGB"), dry, minimum=7)
        bounds = mask.getbbox()
        if bounds is None:
            raise ValueError(f"No visible waterline at frame {tick}")
        left, top, right, bottom = bounds
        height = bottom - top
        if height > 27 or not 332 <= top <= 362:
            raise ValueError(f"Ripple left the side-view waterline near frame {tick}")
        spans.append({"frame": tick, "widthPixels": right - left, "heightPixels": height})
    if spans[2]["widthPixels"] < 2.0 * spans[0]["widthPixels"]:
        raise ValueError("The ripple did not travel along the horizontal waterline")
    return spans


def review_application_images(folder, case, physics):
    with Image.open(folder / "dry.png") as reference:
        dry = reference.convert("RGB")
    optical_frame = application_frame(folder, physics["opticsFrame"])
    with Image.open(folder / "no-chroma-reversal.png") as reference:
        reversal = frame_mask(optical_frame, reference.convert("RGB"), minimum=3)
    with Image.open(folder / "no-optics.png") as reference:
        flat = reference.convert("RGB")
    transmission = frame_mask(optical_frame, flat, minimum=3)
    wet = ImageChops.lighter(frame_mask(optical_frame, dry, minimum=2), frame_mask(flat, dry, minimum=2))
    for optical in (reversal, transmission):
        changed = mask_area(optical)
        if changed < 80 or mask_area(ImageChops.multiply(wet, optical)) < .70 * changed:
            raise ValueError(f"{case} wet optics did not stay inside the active water")

    visual = {"chromaReversalPixels": mask_area(reversal), "transmissionPixels": mask_area(transmission)}
    if case == "curtain":
        impact = physics["impactFrame"]
        drawn = (420, 146, 780, 358)
        masks = [frame_mask(application_frame(folder, impact + i), dry).crop(drawn) for i in (4, 14, 26)]
        before, open_coverage, closed = map(mask_area, masks)
        if before > .19 * open_coverage or open_coverage < 9_000 or closed > .61 * open_coverage:
            raise ValueError("The shown curtain fails progressive formation or closure")
        miss = frame_mask(application_frame(folder, impact + 14, "miss"), dry).crop(drawn)
        if mask_area(miss) > .10 * open_coverage:
            raise ValueError("Visible curtain without contact")
        visual["upperWaterPixelsAfterImpact"] = [before, open_coverage, closed]
    else:
        inlet = (365, 128, 585, 237)
        source = [mask_area(frame_mask(application_frame(folder, k), dry).crop(inlet)) for k in range(66, 222)]
        if min(source) < 2_250 or max(source) > min(source) * 2.15:
            raise ValueError("The visible falling sheet has too little water or an interruption")
        draining = frame_mask(application_frame(folder, 253), dry).crop(inlet)
        if mask_area(draining) > 80:
            raise ValueError("The source kept drawing after the water reached the basin")
        movement = [mask_area(frame_mask(application_frame(folder, k), application_frame(folder, k + 1), minimum=6).crop(inlet))
                    for k in (77, 81, 96, 132, 162, 190, 215, 216, 217, 220)]
        if min(movement) < 200:
            raise ValueError("The steady waterfall has a frozen upper sheet")
        visual.update({"steadyInletPixels": [min(source), max(source)], "steadyMinimumChangingPixels": min(movement)})
    return visual


def encode_native_frames(source, output):
    encoder = shutil.which("ffmpeg")
    if encoder is None:
        raise ValueError("Install ffmpeg on PATH to export the native video.")
    command = [encoder, "-nostdin", "-loglevel", "error", "-framerate", "30", "-start_number", "0",
               "-i", str(source / "%04d.png"), "-an", "-c:v", "libx264", "-threads", "3",
               "-crf", "15", "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(output)]
    subprocess.run(command, check=True, timeout=120, **hidden_process_options())


def export_valve_comparison(folder, output):
    """Show both valve schedules at the same tick, with labels outside the camera frame."""
    with Image.open(folder / "normal/0000.png") as first:
        width = first.width
    with tempfile.TemporaryDirectory(prefix="temp-valves-", dir=output.parent) as temporary:
        labels = Path(temporary) / "labels.png"
        header = Image.new("RGB", (width * 2, 48), (15, 29, 35))
        draw = ImageDraw.Draw(header)
        for x, seconds in ((0, 7.4), (width, 2.4)):
            draw.text((x + 16, 7), f"Valve closes at {seconds:.1f} s", font=font(25), fill="white")
        header.save(labels)
        command = [shutil.which("ffmpeg"), "-nostdin", "-loglevel", "error",
                   "-framerate", "30", "-i", str(folder / "normal/%04d.png"),
                   "-framerate", "30", "-i", str(folder / "early-stop/%04d.png"),
                   "-loop", "1", "-framerate", "30", "-i", str(labels),
                   "-filter_complex_threads", "2", "-filter_complex",
                   "[0:v][1:v]hstack=shortest=1[water];[2:v][water]vstack=shortest=1[view]",
                   "-map", "[view]", "-an", "-c:v", "libx264", "-threads", "3", "-crf", "17",
                   "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(output)]
        subprocess.run(command, check=True, timeout=180, **hidden_process_options())


def export_subject_videos(work, report):
    """Publish one uncropped full-rate movie per case accepted by this replay."""
    destination = work / "Subjects"
    if destination.exists():
        raise FileExistsError(destination)
    captures = work / "A/captures"
    videos = {}
    with tempfile.TemporaryDirectory(prefix="temp-subjects-", dir=work) as temporary:
        staging = Path(temporary) / "Subjects"
        staging.mkdir()
        for group in ("cases", "applications", "channels", "inlets", "scenarios"):
            for case in report.get(group, {}):
                if group == "applications":
                    name = APPLICATIONS[case][1]
                    shutil.copyfile(work / (name + ".mp4"), staging / (name + ".mp4"))
                else:
                    name = case
                    sequence = "frames" if group == "cases" else "normal"
                    encode_native_frames(captures / name / sequence, staging / (name + ".mp4"))
                videos[name] = (Path("Subjects") / (name + ".mp4")).as_posix()
        staging.rename(destination)
    return videos


def export_application(folder, output, video):
    source = folder / "normal"
    if video:
        encode_native_frames(source, output.with_suffix(".mp4"))
    frames = []
    for path in sorted(source.glob("*.png"))[::2]:
        with Image.open(path) as frame:
            frames.append(frame.convert("RGB").resize((768, 432), Image.Resampling.LANCZOS))
    swatches = Image.new("RGB", (192, len(frames[::7]) * 108))
    for index, frame in enumerate(frames[::7]):
        swatches.paste(frame.resize((192, 108), Image.Resampling.BOX), (0, index * 108))
    palette = swatches.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    encoded = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    delays = (70, 60, 70)
    encoded[0].save(output.with_suffix(".gif"), save_all=True, append_images=encoded[1:],
                    duration=[delays[k % 3] for k in range(len(frames))], loop=0, disposal=1, optimize=False)
