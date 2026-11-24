"""Show four synchronized, side-on native channel captures and check their visible water."""
from contextlib import nullcontext
import os
import shutil
import subprocess

from PIL import Image, ImageChops, ImageDraw

from fluid_preview import font, frame_mask, mask_area
from channel_replay_checks import CHANNELS

TITLES = ("River current", "Shore wash", "Closed-basin slosh", "Cove wash")
DIMENSIONS = (1280, 720)


def visible(captures, name):
    folder = captures / name
    def read(path):
        with Image.open(path) as frame:
            return frame.convert("RGB")
    normal = read(folder / "normal/0066.png")
    dry = read(folder / "dry.png")
    varied = read(folder / "normal/0060.png")
    movement = [mask_area(frame_mask(read(folder / "normal" / f"{i:04d}.png"),
                                     read(folder / "normal" / f"{i + 3:04d}.png"), minimum=3))
                for i in (54, 83, 120, 150)]
    red, green, blue = ImageChops.difference(normal, read(folder / "no-chroma-reversal.png")).split()
    chroma = ImageChops.lighter(red, ImageChops.lighter(green, blue))
    data = {"wetPixels": mask_area(frame_mask(normal, dry)),
            "chromaReversalPixels": sum(chroma.histogram()[3:]),
            "transmissionPixels": mask_area(frame_mask(normal, read(folder / "no-optics.png"), minimum=2)),
            "peakMovingPixelsOverThreeFrames": max(movement),
            "seedVariationPixels": mask_area(frame_mask(varied, read(folder / "alternate.png"), minimum=3))}
    if min(data.values()) < 20 or data["wetPixels"] < 1250 or data["peakMovingPixelsOverThreeFrames"] < 200:
        raise ValueError(f"{name} failed visible water, transport, variation or optics: {data}")
    return data


def export_channels(captures, work, video):
    export_gallery(captures, work, video, "SideChannels", CHANNELS, TITLES, 256)


def export_inlets(captures, work, video):
    from inlet_replay_checks import INLETS
    export_gallery(captures, work, video, "WaterInlets", tuple(INLETS),
                   ("Rain reaching a pool", "Water dripping from a ledge", "Fountain arc"), 114)


def export_gallery(captures, work, video, output, cases, titles, count):
    labels = font(26)
    frames = []
    flags = {"creationflags": subprocess.CREATE_NO_WINDOW} if os.name == "nt" else {}
    command = [shutil.which("ffmpeg"), "-nostdin", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24",
               "-s", "1280x720", "-framerate", "30", "-i", "pipe:0", "-an", "-c:v", "libx264", "-threads", "3",
               "-crf", "16", "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(work / (output + ".mp4"))]
    stream = subprocess.Popen(command, stdin=subprocess.PIPE, **flags) if video else None
    try:
        with stream.stdin if stream else nullcontext():
            for tick in range(0, count, 1 if video else 2):
                screen = Image.new("RGB", DIMENSIONS, "#11232a")
                draw = ImageDraw.Draw(screen)
                for i, name in enumerate(cases):
                    x, y = 16 + 635 * (i % 2), 8 + 354 * (i // 2)
                    if i == 2 and len(cases) == 3: x = 335
                    draw.text((x, y), titles[i], font=labels, fill="#e1efed")
                    with Image.open(captures / name / "normal" / f"{tick:04d}.png") as native:
                        # Each panel keeps the source camera and the same registered elevation.
                        panel = native.crop((0, 45 if len(cases) == 3 else 98, 960, 487 if len(cases) == 3 else 540)).resize((608, 280), Image.Resampling.LANCZOS)
                        screen.paste(panel, (x, y + 42))
                if stream: stream.stdin.write(screen.tobytes())
                if tick % 2 == 0: frames.append(screen.resize((768, 432), Image.Resampling.LANCZOS))
    finally:
        if stream and stream.wait(timeout=120):
            raise RuntimeError("The channel video encoder failed")
    swatch = Image.new("RGB", (192, 108 * len(frames[::8])))
    for i, frame in enumerate(frames[::8]):
        swatch.paste(frame.resize((192, 108), Image.Resampling.BOX), (0, 108 * i))
    palette = swatch.quantize(colors=256)
    frames = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    frames[0].save(work / (output + ".gif"), save_all=True, append_images=frames[1:],
                   duration=[(70, 60, 70)[i % 3] for i in range(len(frames))], loop=0, disposal=1, optimize=False)
