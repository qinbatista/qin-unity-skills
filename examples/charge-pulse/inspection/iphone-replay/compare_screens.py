"""Render device and screen comparisons from a verified set of Unity Player screenshots."""
import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps

MODELS = (("17e", "iPhone 17e"), ("17", "iPhone 17"), ("Air", "iPhone Air"),
          ("18-Pro", "iPhone 18 Pro"), ("18-Pro-Max", "iPhone 18 Pro Max"), ("Duo", "iPhone Duo, outer"))
PAPER, INK, MUTED, RULE = "#f6f4ed", "#192f2e", "#435c55", "#a5b9ad"


def caption_font(font, size):
    return ImageFont.truetype(str(font), size) if font else ImageFont.load_default(size=size)


def place(canvas, image_file, x, y, w, h):
    with Image.open(image_file) as picture:
        native = ImageOps.contain(picture.convert("RGB"), (w, h), Image.Resampling.LANCZOS)
    canvas.paste(native, (x + (w - native.width)//2, y + (h - native.height)//2))


def screenshot(frames, stem):
    metadata = json.loads((frames/(stem+".json")).read_text())
    return frames/(stem+".png"), f'{metadata["width"]} × {metadata["height"]}'


def folds(source, output, font, prefix):
    frames = source / (prefix + "Duo") / "frames"
    canvas = Image.new("RGB", (1660, 663), PAPER)
    draw = ImageDraw.Draw(canvas)
    draw.text((30,21), "iPhone Duo  /  One briefing through a fold", fill=INK, font=caption_font(font,42))
    draw.text((30,83), "The same German briefing stays open. All three Unity frames use the same pixel scale.", fill=MUTED, font=caption_font(font,24))
    for x, width, label, stem in [(30,409,"Closed","retained-fold-before"), (463,572,"Partly open","retained-half-ready"),
                                   (1058,572,"Fully open","retained-inner-ready")]:
        path, pixels = screenshot(frames, stem)
        draw.line((x,141,x+width,141),fill=RULE,width=2)
        draw.text((x,150),label,fill=INK,font=caption_font(font,31))
        draw.text((x,198),pixels,fill=MUTED,font=caption_font(font,22))
        place(canvas,path,x,242,width,404)
    canvas.save(output,quality=94,optimize=True,subsampling=0)


def phones(source, output, font, prefix):
    canvas = Image.new("RGB", (1618, 1480), PAPER)
    draw = ImageDraw.Draw(canvas)
    draw.text((32,17), "Expedition menu  /  Six iPhone simulators", fill=INK, font=caption_font(font,42))
    draw.text((32,78), "Upright and landscape. Each pair shows the same German menu in the running Unity Player.", fill=MUTED, font=caption_font(font,23))
    for i,(folder,title) in enumerate(MODELS):
        x = 32+(i%2)*790
        y = 137+(i//2)*446
        display = "outer" if folder=="Duo" else "phone"
        frames = source / (prefix + folder) / "frames"
        draw.line((x,y,x+756,y),fill=RULE,width=2)
        draw.text((x,y+9),title,fill=INK,font=caption_font(font,30))
        for offset,w,orient in [(0,218,"Portrait"),(236,520,"LandscapeLeft")]:
            path, pixels = screenshot(frames,display+"-"+orient+"-Expedition")
            draw.text((x+offset,y+62),pixels,fill=MUTED,font=caption_font(font,20))
            place(canvas,path,x+offset,y+95,w,330)
    canvas.save(output,quality=94,optimize=True,subsampling=0)


def duo_screens(source, output, font, prefix):
    frames = source / (prefix + "Duo") / "frames"
    screens = (("Expedition", "Expedition menu"), ("ControllerSurface", "Touch controller"),
               ("Hud", "Gameplay HUD"), ("Waypoint", "Waypoint map"),
               ("Inventory", "Inventory"), ("Atlas", "Trail atlas"),
               ("Settings", "Settings overview"), ("Preferences", "Preferences"),
               ("Dialog", "Confirmation"), ("Quests", "Trail journal"))
    canvas = Image.new("RGB", (1660, 2196), PAPER)
    draw = ImageDraw.Draw(canvas)
    draw.text((30, 17), "iPhone Duo  /  Ten Unity UI Toolkit screens", fill=INK, font=caption_font(font, 42))
    draw.text((30, 80), "Each pair shows the closed outer screen in portrait and the open inner screen in landscape.",
              fill=MUTED, font=caption_font(font, 23))
    for index, (screen, title) in enumerate(screens):
        x = 30 + index % 2 * 813
        y = 144 + index // 2 * 406
        draw.line((x, y, x + 777, y), fill=RULE, width=2)
        draw.text((x, y + 9), title, fill=INK, font=caption_font(font, 28))
        place(canvas, frames / ("outer-Portrait-" + screen + ".png"), x, y + 62, 228, 321)
        place(canvas, frames / ("inner-LandscapeLeft-" + screen + ".png"), x + 243, y + 62, 530, 321)
    canvas.save(output, quality=93, optimize=True, subsampling=0)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("devices", type=Path)
    parser.add_argument("output", type=Path, help="Existing destination for the three JPEGs")
    parser.add_argument("--font", type=Path)
    parser.add_argument("--prefix", default="", help="Common result label prefix, for example final-")
    args = parser.parse_args()
    folds(args.devices, args.output/"duo-fold.jpg", args.font, args.prefix)
    phones(args.devices, args.output/"iphone-family.jpg", args.font, args.prefix)
    duo_screens(args.devices, args.output / "duo-screens.jpg", args.font, args.prefix)
