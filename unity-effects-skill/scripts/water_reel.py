"""Encode the four matrix chapters at their recorded speed and frame the six camera views independently."""
from pathlib import Path
import subprocess
import shutil

from PIL import Image, ImageDraw, ImageOps
from fluid_preview import font, hidden_process_options
from water_gallery import GROUPS, title


def write_reel(work, families):
    cols = 3
    tile = (384, 216)
    row = tile[1] + 30
    size = (cols * tile[0], 50 + 2 * row)
    output = work / "WaterMatrix.mp4"
    args = [shutil.which("ffmpeg"), "-nostdin", "-loglevel", "error", "-f", "rawvideo",
            "-pix_fmt", "rgb24", "-s", f"{size[0]}x{size[1]}", "-r", "30", "-i", "pipe:0",
            "-an", "-c:v", "libx264", "-threads", "3", "-crf", "22", "-maxrate", "650k", "-bufsize", "900k", "-pix_fmt", "yuv420p",
            "-movflags", "+faststart", str(output)]
    with subprocess.Popen(args, stdin=subprocess.PIPE, **hidden_process_options()) as encoder:
        for group, label in GROUPS.items():
            cases = families[group]
            sequences = [sorted((work / "A/captures" / name / "normal").glob("[0-9][0-9][0-9][0-9].png"))
                         for name in cases]
            if not sequences or any(len(frames) != len(sequences[0]) for frames in sequences):
                raise ValueError(f"Inconsistent native frame sequences in {group}")
            board = Image.new("RGB", size, "#0b1820")
            drawing = ImageDraw.Draw(board)
            drawing.text((10, 9), label, font=font(26), fill="#e6f6fb")
            drawing.text((size[0] - 204, 16), "Unity capture  /  30 fps", font=font(15), fill="#a9c3cd")
            y_origin = 50 + (row // 2 if len(cases) <= cols else 0)
            for i, name in enumerate(cases):
                x, y = (i % cols) * tile[0], y_origin + (i // cols) * row
                drawing.text((x + 8, y + tile[1] + 4), title(name), font=font(18), fill="#e6f6fb")
            for frames in zip(*sequences):
                canvas = board.copy()
                for i, source in enumerate(frames):
                    with Image.open(source) as image:
                        view = ImageOps.contain(image.convert("RGB"), tile, Image.Resampling.LANCZOS)
                        x = (i % cols) * tile[0] + (tile[0] - view.width) // 2
                        y = y_origin + (i // cols) * row + (tile[1] - view.height) // 2
                        canvas.paste(view, (x, y))
                encoder.stdin.write(canvas.tobytes())
        encoder.stdin.close()
        if encoder.wait(timeout=180):
            raise RuntimeError(f"Encoding failed at {output}")
    return output
