"""Place the original artwork below a native capture for the illustrated guide."""

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps

ART = Path(__file__).resolve().parents[2] / "unity-performance-skill/assets/image-optimization/Art"


def draw_source_strip(captures):
    world = Image.open(captures / "World.png").convert("RGB")
    sheet = Image.new("RGB", (world.width, world.height + 176), "#f0f1e7")
    sheet.paste(world)
    pen = ImageDraw.Draw(sheet)
    title = ImageFont.load_default(size=18)
    normal = ImageFont.load_default(size=14)
    pieces = [("World/Ground.png", "Ground period"), ("World/TreeBase.png", "Base"),
              ("World/TreeTrunk.png", "Trunk period"), ("World/TreeCrown.png", "Crown"),
              ("UI/WoodlandFrame.png", "UI frame")]
    column = sheet.width // len(pieces)
    for i, (relative, label) in enumerate(pieces):
        with Image.open(ART / relative) as raw:
            raw = raw.convert("RGBA")
            inset = ImageOps.contain(raw, (column - 30, 86), method=Image.Resampling.NEAREST)
            center = column * i + column // 2
            sheet.paste(inset, (center - inset.width // 2, world.height + 12 + (86 - inset.height) // 2), inset)
            pen.text((center, world.height + 115), label, font=title, anchor="mm", fill="#1f3734")
            pen.text((center, world.height + 143), f"{raw.width} x {raw.height} px", font=normal, anchor="mm", fill="#4e6158")
    sheet.save(captures / "World-and-pieces.png", optimize=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("captures", type=Path, help="Directory containing the Unity replay outputs")
    draw_source_strip(parser.parse_args().captures)
