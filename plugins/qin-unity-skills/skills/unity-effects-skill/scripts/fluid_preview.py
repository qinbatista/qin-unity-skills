"""Create a neutral landscape preview from one receiver's real Unity frames (optional Pillow dependency)."""
from contextlib import ExitStack
from pathlib import Path
import os
import shutil
import subprocess
from PIL import Image, ImageChops, ImageDraw, ImageFont

from fluid_replay_checks import FORMS

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
