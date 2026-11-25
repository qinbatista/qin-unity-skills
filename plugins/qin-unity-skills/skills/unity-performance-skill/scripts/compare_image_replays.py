"""Compare two fresh woodland replays made on the same Unity graphics target."""

import argparse
import hashlib
import json
import re
from dataclasses import dataclass
from pathlib import Path

from PIL import Image

from replay_image_optimization import METHODS, verify_cases, verify_prefab_package


FRAMES = ("World.png", "UI.png", "UI-Canvas.png", "Import-Before.png", "Import-After.png")
CAPTURES = {*FRAMES, "Atlas.png", "ImportAtlas.json", "Woodland.unitypackage"}


@dataclass
class Replay:
    receipt: dict
    prefab_guid: str
    measurements: dict
    frame_sizes: dict
    frame_pixels: dict
    atlas_pieces: dict


def pixel_hash(image):
    return hashlib.sha256(image.convert("RGBA").tobytes()).hexdigest()


def read_run(work):
    output = work / "captures"
    receipt = json.loads((output / "Replay.json").read_text(encoding="utf-8"))
    if verify_cases(work / "results.xml") != sorted(receipt["cases"]):
        raise ValueError(f"Case names differ between Unity and {output / 'Replay.json'}")
    if set(receipt["captureSha256"]) != CAPTURES:
        raise ValueError(f"Incomplete capture inventory in {output}")
    for name, expected in receipt["captureSha256"].items():
        if hashlib.sha256((output / name).read_bytes()).hexdigest() != expected:
            raise ValueError(f"Capture has changed since the replay: {output / name}")
    project = work / "receiver"
    verify_prefab_package(output / "Woodland.unitypackage", project)
    meta = (project / "Assets/ImageExamples/Woodland.prefab.meta").read_text(encoding="utf-8")
    guid = re.search(r"^guid: ([a-fA-F0-9]{32})$", meta, re.MULTILINE)
    if not guid:
        raise ValueError(f"Missing Unity prefab GUID in {project}")
    sizes = {}
    pixels = {}
    for name in FRAMES:
        with Image.open(output / name) as frame:
            sizes[name] = frame.size
            pixels[name] = pixel_hash(frame)

    measured = json.loads((output / "ImportAtlas.json").read_text(encoding="utf-8"))
    pieces = {}
    with Image.open(output / "Atlas.png") as page:
        for piece in measured["pieces"]:
            x, y, width, height = (piece[k] for k in ("x", "y", "width", "height"))
            top = page.height - y - height  # Unity records rectangles from the lower left.
            if not (0 <= x < x + width <= page.width and 0 <= top < top + height <= page.height):
                raise ValueError(f"Invalid atlas rectangle for {piece['name']} in {output}")
            crop = page.crop((x, top, x + width, top + height))
            pieces[piece["name"]] = {"size": crop.size, "rgbaSha256": pixel_hash(crop)}
    if set(pieces) != {"TreeBase", "TreeTrunk", "TreeCrown"} or measured["excluded"] != "Ground":
        raise ValueError(f"Unexpected tree atlas membership in {output}")
    return Replay(receipt, guid.group(1), measured, sizes, pixels, pieces)


def compare(first, second):
    left_run, right_run = read_run(first), read_run(second)
    if first.resolve() == second.resolve() or left_run.prefab_guid == right_run.prefab_guid:
        raise ValueError("Use two independently created receivers with different prefab GUIDs.")
    for key in ("unity", "activeTarget", "packageVersions", "sourceSha256"):
        if left_run.receipt[key] != right_run.receipt[key]:
            raise ValueError(f"The two replays differ in {key}.")
    for label, left, right in (
        ("frame dimensions", left_run.frame_sizes, right_run.frame_sizes),
        ("rendered pixels", left_run.frame_pixels, right_run.frame_pixels),
        ("packed sprite pixels", left_run.atlas_pieces, right_run.atlas_pieces),
    ):
        if left != right:
            changed = [key for key in left.keys() | right.keys() if left.get(key) != right.get(key)]
            raise ValueError(f"Different {label}: {', '.join(sorted(changed))}")
    for key in ("atlasWidth", "atlasHeight", "atlasFormat", "projectedWidth", "projectedHeight"):
        if left_run.measurements[key] != right_run.measurements[key]:
            raise ValueError(f"The two native measurements differ in {key}.")
    imports = []
    for run in (left_run, right_run):
        before, after = run.measurements["before"], run.measurements["after"]
        if not 0 < after["textureObjectBytes"] < before["textureObjectBytes"]:
            raise ValueError("Expected a smaller measured texture after import.")
        imports.append({"originalTextureBytes": before["textureObjectBytes"],
                        "optimizedTextureBytes": after["textureObjectBytes"]})
    return {"unity": left_run.receipt["unity"], "activeTarget": left_run.receipt["activeTarget"], "casesPerReceiver": sorted(METHODS),
            "receiverPrefabGuids": [left_run.prefab_guid, right_run.prefab_guid], "imports": imports, "matchingFrameRgbaSha256": left_run.frame_pixels,
            "matchingAtlasRegions": left_run.atlas_pieces,
            "replayReceiptSha256": [hashlib.sha256((work / "captures/Replay.json").read_bytes()).hexdigest()
                                    for work in (first, second)]}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("first", type=Path)
    parser.add_argument("second", type=Path)
    args = parser.parse_args()
    try:
        print(json.dumps(compare(args.first, args.second), indent=2, sort_keys=True))
    except (OSError, ValueError, KeyError) as error:
        parser.exit(1, f"{error}\n")
