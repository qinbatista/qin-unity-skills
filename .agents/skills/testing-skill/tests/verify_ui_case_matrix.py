"""Verify one native ResponsiveLayoutCases run and optionally render its device comparison."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
from xml.etree import ElementTree

from PIL import Image, ImageDraw, ImageFont, ImageOps


PROJECT = Path(__file__).resolve().parents[4]
FIXTURES = PROJECT / "examples/charge-pulse/Assets/Editor/Tests/ResponsiveLayouts"
SCREENS = {
    "expedition": ("Expedition menu", "ExpeditionMenuReflowsAndNavigates"),
    "controllersurface": ("Gameplay controller", "ControllerSurfaceKeepsIndependentTouchAndNavigation"),
    "hud": ("Expedition HUD", "HudKeepsTheWingsAndMovesTheCenter"),
    "waypoint": ("Waypoint map", "WaypointTracksClampsAndDragsInTheActivePane"),
    "inventory": ("Inventory grid", "InventoryReflowsOneSetOfInstances"),
    "atlas": ("Searchable trail atlas", "AtlasSearchesAndRecyclesRealRows"),
    "settings": ("Settings overview", "SettingsKeepRowsContainedAndCardsAligned"),
    "preferences": ("Preferences form", "PreferencesPreserveFieldsAndAnchorOverlays"),
    "dialog": ("Confirmation dialog", "ConfirmationMatchesCanvas"),
    "quests": ("Scrolling journal", "QuestListScrollsInsideFixedHeaderAndFooter"),
}


def profiles():
    source = (FIXTURES / "ResponsiveLayoutCases.cs").read_text(encoding="utf-8")
    constructors = re.findall(r'new\s+LayoutProfile\s*\(', source)
    rows = re.findall(r'new\s+LayoutProfile\s*\(\s*"([^"]+)"\s*,\s*(\d+)\s*,\s*(\d+)', source)
    sizes = {name: (int(width), int(height)) for name, width, height in rows}
    if len(sizes) < 25 or len(sizes) != len(constructors):
        raise ValueError("Expected at least 25 uniquely named native viewport profiles with explicit dimensions.")
    return sizes


def required_frames(screen, profile, locale, last_cell):
    cell = f"{profile}-{locale}"
    yield f"{screen}-{cell}"
    additional = {
        "atlas": ["atlas-sort"],
        "dialog": ["canvas-dialog"],
        "expedition": ["expedition-briefing"],
        "preferences": ["preferences-map", "preferences-help", "preferences-confirm", "preferences-saved"],
    }
    for prefix in additional.get(screen, []):
        yield f"{prefix}-{cell}"
    if screen == "quests":
        yield f"quests-{cell}-last"
    if screen == "inventory" and profile == "compact-portrait" and locale == "de-DE":
        yield f"inventory-drag-{cell}"
    if screen == "atlas" and profile == "compact-portrait":
        yield f"atlas-{cell}-error"
    if screen == "controllersurface" and locale == "en-US" and profile in {
        "compact-portrait", "duo-outer-landscape-right", "separating-left-pane"
    }:
        yield f"controller-held-{cell}"
        yield f"controller-paused-{cell}"
    if screen == "hud" and last_cell:
        for state in ("focus", "held", "released", "disabled", "reduced"):
            yield f"feedback-hud-{cell}-{state}"


def verify(results, frames):
    sizes = profiles()
    locales = json.loads((FIXTURES / "LayoutStrings.json").read_text(encoding="utf-8"))["locales"]
    if len(set(locales)) != len(locales) or not {"en-US", "de-DE"}.issubset(locales):
        raise ValueError("The native fixture needs unique locales including English and German.")
    suite = ElementTree.parse(results)
    cases = {row.get("fullname"): row for row in suite.iter("test-case")}
    counts = Counter()
    last_profile = list(sizes)[-1]
    for screen, (_, method) in SCREENS.items():
        case = cases.get("Qin.UnitySamples.Tests.ResponsiveLayoutCases." + method)
        if case is None or case.get("result") != "Passed":
            detail = case.findtext("failure/message") if case is not None else "missing"
            raise ValueError(f"Native case {method} did not pass: {detail}")
        for profile, size in sizes.items():
            for locale in locales:
                last_cell = profile == last_profile and locale == locales[-1]
                for name in required_frames(screen, profile, locale, last_cell):
                    path = frames / (name + ".png")
                    with Image.open(path) as picture:
                        if picture.format != "PNG" or picture.size != size:
                            raise ValueError(f"{name} expected PNG {size}, got {picture.format} {picture.size}")
                        picture.load()
                        if all(lo == hi for lo, hi in picture.convert("RGB").getextrema()):
                            raise ValueError(f"{name} is a solid frame")
                    counts[screen] += 1
    return {"runtime": "Unity Editor Play Mode", "profiles": sizes, "locales": locales,
            "primaryScreenCells": len(sizes) * len(locales) * len(SCREENS),
            "verifiedFrameCount": sum(counts.values()), "framesByScreen": dict(counts)}


def gallery(frames, destination, font):
    def typeface(size):
        return ImageFont.truetype(str(font), size) if font else ImageFont.load_default(size=size)

    paper, ink, secondary = "#f5f4ed", "#192e2b", "#425c55"
    width, margin, column, gap, row = 1648, 36, 776, 24, 380
    image = Image.new("RGB", (width, 126 + 5 * row + 18), paper)
    draw = ImageDraw.Draw(image)
    draw.text((margin, 16), "Unity UI Toolkit  /  Device comparison", font=typeface(42), fill=ink)
    draw.text((margin, 76), "Native Editor renders. Each pair shows a 320 × 568 phone and a 2048 × 1536 tablet in English.",
              font=typeface(24), fill=secondary)
    for index, (screen, (title, _)) in enumerate(SCREENS.items()):
        x, y = margin + index % 2 * (column + gap), 126 + index // 2 * row
        draw.line((x, y, x + column, y), width=2, fill="#a8b9ad")
        draw.text((x, y + 11), title, font=typeface(30), fill=ink)
        for key, left, box_width in (("small-phone", x, 220), ("tablet-landscape", x + 228, column - 228)):
            with Image.open(frames / f"{screen}-{key}-en-US.png") as native:
                scaled = ImageOps.contain(native.convert("RGB"), (box_width, 305), Image.Resampling.LANCZOS)
            top = y + 58
            draw.rectangle((left, top, left + box_width - 1, top + 304), fill="#dbe4da")
            image.paste(scaled, (left + (box_width - scaled.width) // 2, top + (305 - scaled.height) // 2))
    image.save(destination, quality=93, optimize=True, subsampling=0)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results", required=True, type=Path, help="Unity NUnit XML for the same run")
    parser.add_argument("--frames", required=True, type=Path, help="Native PNG output of that run")
    parser.add_argument("--gallery", type=Path, help="Optional JPEG comparison output")
    parser.add_argument("--font", type=Path, help="Optional TrueType font for gallery titles")
    args = parser.parse_args()
    result = verify(args.results, args.frames)
    if args.gallery:
        gallery(args.frames, args.gallery, args.font)
    print(json.dumps(result, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
