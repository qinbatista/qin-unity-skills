"""Verify one Unity iPhone Player run using its active UIKit window, captures and input receipts."""
import argparse
import json
import math
from pathlib import Path

from PIL import Image

SCREENS = ("Expedition", "ControllerSurface", "Hud", "Inventory", "Settings",
           "Preferences", "Dialog", "Quests", "Atlas", "Waypoint")
# UIKit interface orientation values observed for Unity's requested device rotations.
ORIENTATIONS = {"Portrait": 1, "LandscapeLeft": 3, "LandscapeRight": 4}
# These two subjects are a read-only row and a scroll viewport.
STATIC_SUBJECTS = {"Settings", "Quests"}
FOLD_PHASES = {"half-ready": 90, "inner-ready": 180, "outer-return-ready": 0, "inner-again-ready": 180}
DATA_ERRORS = (OSError, ValueError, KeyError, TypeError, IndexError, ZeroDivisionError)


def rect(value):
    return [value[key] for key in ("x", "y", "width", "height")] if isinstance(value, dict) else value


def extent(box):
    x, y, width, height = box
    return x, y, x + width, y + height


def check_picture(path, size=None):
    with Image.open(path) as frame:
        if frame.format != "PNG" or (size and frame.size != size):
            raise ValueError(f"Unexpected native image type or dimensions {frame.format} {frame.size}")
        sample = frame.convert("RGB").resize((64, 64))
        if max(hi - lo for lo, hi in sample.getextrema()) <= 45 or sample.getcolors(15) is not None:
            raise ValueError("Capture lacks visible screen content")
        return list(frame.size)


def inspect(path, *, screen, pose, requested=None, retained=False, enforce_floor=True, both_zoom_buttons=False):
    observation = json.loads(path.read_text())
    native = json.loads(observation["nativeRegions"])
    errors = []

    def expect(ok, message):
        if not ok:
            errors.append(message)

    expect(observation["pose"] == pose and observation["screen"] == screen, "Receipt identity mismatch")
    expect(not observation.get("error"), observation.get("error"))
    expect(native.get("available") and native.get("regionsAvailable"), "Missing active UIKit view or region source")
    width, height = observation["width"], observation["height"]
    wx, wy, ww, wh = native["windowTopLeft"]
    sx, sy = width / ww, height / wh
    expect(abs(sx - sy) <= .01 and sx > 0, "Unity and UIKit window scales differ by axis")

    def in_frame(box):
        bx, by, bw, bh = box
        return [(bx - wx) * sx, (by - wy) * sy, bw * sx, bh * sy]

    if requested:
        expect(observation["requested"] == requested, "Recorded request differs from case")
        actual = observation["uiKitOrientation"]
        expect(actual in (1, 2, 3, 4), "Missing native interface orientation")
        if not pose.startswith(("inner-", "half-")):
            expect(actual == ORIENTATIONS[requested] and observation["unityOrientation"] == requested,
                   "Outer or standard phone did not realize the requested rotation")

    expect(observation["picked"], "The native panel did not pick the subject")
    check_picture(path.with_suffix(".png"), (width, height))
    scale = observation["pixelsPerPanelUnit"]
    x, y, x2, y2 = extent([v * scale for v in rect(observation["subject"])])
    pane = rect(observation["primaryPaneTopLeft"])
    native_pane = in_frame(native["primaryPaneTopLeft"])
    expect(all(abs(a - b) <= 1 for a, b in zip(pane, native_pane)), "Unity pane differs from native pane")
    left, top, right, bottom = extent(pane)
    expect(left - 1 <= x < x2 <= right + 1 and top - 1 <= y < y2 <= bottom + 1,
           "Subject exceeds the selected pane")
    for box in native["reservedTopLeft"] + native["divisionsTopLeft"]:
        l, t, r, b = extent(in_frame(box))
        expect(min(r, x2) - max(l, x) <= 1 or min(b, y2) - max(t, y) <= 1,
               f"Subject intersects UIKit region {box}")

    if not retained:
        expect(observation["edgesPicked"], "A subject edge failed native picking")
        point_scale = native["pointScale"]
        hit = observation["hitSizeInPoints"]
        point_width, point_height = (x2 - x) / (sx * point_scale), (y2 - y) / (sy * point_scale)
        expect(abs(hit["x"] - point_width) < .5 and abs(hit["y"] - point_height) < .5,
               "UIKit hit measurement mismatch")
        if enforce_floor and screen not in STATIC_SUBJECTS:
            expect(min(point_width, point_height) >= 43.5, "Primary control is smaller than 44 UIKit points")
    if screen == "Waypoint" and (both_zoom_buttons or "zoomOut" in observation):
        zoom = [v * scale for v in rect(observation["zoomOut"])]
        zl, zt, zr, zb = extent(zoom)
        expect(observation["zoomOutPicked"], "Zoom-out center or edge failed native picking")
        expect(left - 1 <= zl < zr <= right + 1 and top - 1 <= zt < zb <= bottom + 1,
               "Zoom-out exceeds the selected pane")
        expect(min(zoom[2] / sx, zoom[3] / sy) / native["pointScale"] >= 43.5,
               "Zoom-out is smaller than 44 UIKit points")
        for box in native["reservedTopLeft"] + native["divisionsTopLeft"]:
            l, t, r, b = extent(in_frame(box))
            expect(min(r, zr) - max(l, zl) <= 1 or min(b, zb) - max(t, zt) <= 1,
                   f"Zoom-out intersects UIKit region {box}")
    return observation, native, errors


def read_hinge(path, target):
    receipt = json.loads(path.read_text())
    angle = receipt["measuredDegrees"]
    tolerance = 3 if target in (0, 180) else 15
    if (receipt["passed"] is not True or receipt["requestedDegrees"] != target or
            not math.isfinite(angle) or not 0 <= angle <= 180 or abs(angle - target) > tolerance or
            receipt["samplesDegrees"][-1] != angle):
        raise ValueError(f"Sensor did not confirm the requested {target}° pose")
    return angle


def verify(directory, duo, waypoint_only=False):
    frames = directory / "frames"
    displays = ("outer", "half", "inner") if duo else ("phone",)
    failures, windows, retained_rows, baselines = {}, [], {}, []
    screen_names = ("Waypoint",) if waypoint_only else SCREENS
    selection = directory / "screens.txt"
    if waypoint_only and (not selection.is_file() or selection.read_text().strip() != "Waypoint"):
        failures["selection"] = ["Missing Waypoint-only run identity"]

    def read_case(stem, **fields):
        try:
            observation, native, errors = inspect(frames / (stem + ".json"), **fields)
        except DATA_ERRORS as exc:
            failures[stem] = [str(exc)]
            return None
        if errors:
            failures[stem] = errors
        return observation, native

    for display in displays:
        for requested, orientation in ORIENTATIONS.items():
            pose = display + "-" + requested
            for screen in screen_names:
                result = read_case(pose + "-" + screen, screen=screen, pose=pose, requested=requested,
                                   both_zoom_buttons=waypoint_only)
                if result and screen == screen_names[0]:
                    o, native = result
                    windows.append({
                        "pose": pose, "framebuffer": [o["width"], o["height"]], "requested": requested,
                        "unityOrientation": o["unityOrientation"], "uiKitOrientation": o["uiKitOrientation"],
                        "rotationRealized": orientation == o["uiKitOrientation"], "window": native["windowTopLeft"],
                        "safe": native["safeTopLeft"], "activeOcclusions": native["reservedTopLeft"],
                        "activeDivisions": native["divisionsTopLeft"], "pane": rect(o["primaryPaneTopLeft"]),
                        "actionPoints": o["hitSizeInPoints"],
                    })
        if waypoint_only:
            continue
        for screen in ("Expedition", "ControllerSurface"):
            pose = "before-" + display + "-Portrait"
            result = read_case(pose + "-" + screen, screen=screen, pose=pose, enforce_floor=False)
            if result:
                o, _ = result
                baselines.append({"pose": pose, "screen": screen, "framebuffer": [o["width"], o["height"]],
                                  "actionPoints": o["hitSizeInPoints"]})

    phases = ["rotation-before", "rotation-after"]
    if duo:
        phases += ["fold-before", *FOLD_PHASES]
    identities = set()
    layout_updates = {}
    for phase in phases:
        stem = "retained-" + phase
        result = read_case(stem, screen="Expedition", pose=phase, retained=True)
        if not result:
            continue
        o, native = result
        layout_updates[phase] = o.get("geometryUpdates", 0)
        retained_rows[phase] = {"framebuffer": [o["width"], o["height"]], "safe": native["safeTopLeft"],
                                "pane": rect(o["primaryPaneTopLeft"]), "activeDivisions": native["divisionsTopLeft"]}
        try:
            tokens = (frames / (stem + ".txt")).read_text().split()
            flags = dict(token.split("=", 1) for token in tokens)
            identities.add(flags["root"])
            if any(flags.get(key) != "True" for key in ("same", "briefing", "focus", "pick")):
                raise ValueError("Root, page, focus or hit failed during transfer")
        except DATA_ERRORS as exc:
            failures.setdefault(stem, []).append(str(exc))
    if len(identities) != 1:
        failures["retained-root"] = ["Missing root or different retained root across transitions"]

    hinge = {}
    if duo:
        sensors = [(phase, phase + "-hinge", angle) for phase, angle in FOLD_PHASES.items()]
        sensors += [("initial", "initial-closed", 0), ("half-matrix", "half-matrix-hinge", 90),
                    ("restored", "restored-closed", 0)]
        for phase, filename, angle in sensors:
            try:
                hinge[phase] = {"angle": read_hinge(directory / (filename + ".json"), angle)}
                if phase in FOLD_PHASES:
                    hinge[phase]["displayPixels"] = check_picture(directory / (phase + "-os.png"))
            except DATA_ERRORS as exc:
                failures[filename] = [str(exc)]
        if all(p in retained_rows for p in phases):
            closed, half, opened, restored = [retained_rows[p] for p in
                                             ("fold-before", "half-ready", "inner-ready", "outer-return-ready")]
            if closed["framebuffer"] == opened["framebuffer"] or closed != restored:
                failures["display-transfer"] = ["Active viewport did not transfer and restore"]
            if layout_updates["inner-ready"] <= layout_updates["half-ready"]:
                failures["automatic-reflow"] = ["The mounted page did not observe the hinge-only change before capture"]
            if (half["framebuffer"] != opened["framebuffer"] or not half["activeDivisions"] or
                    opened["activeDivisions"] or half["pane"] == opened["pane"] or
                    opened != retained_rows["inner-again-ready"]):
                failures["separating-division"] = ["Expected a divided half-open pane and a repeatable clear open pane"]

    completion = frames / "complete"
    if not completion.is_file() or completion.read_text().strip() != "0":
        failures["complete"] = ["Missing clean Player completion"]
    log = frames / "runtime-errors.txt"
    if log.exists() and log.read_text().strip():
        failures["native-errors"] = [log.read_text().splitlines()[0]]
    return {"expectedScreenCells": len(displays) * len(ORIENTATIONS) * len(screen_names),
            "windows": windows, "retained": retained_rows, "geometryUpdates": layout_updates, "hinge": hinge,
            "beforePointFloor": baselines, "failures": failures}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--duo", action="store_true")
    parser.add_argument("--waypoint-only", action="store_true", help="Check both zoom buttons in a focused native replay")
    args = parser.parse_args()
    report = verify(args.directory, args.duo, args.waypoint_only)
    print(json.dumps(report, indent=2))
    raise SystemExit(bool(report["failures"]))
