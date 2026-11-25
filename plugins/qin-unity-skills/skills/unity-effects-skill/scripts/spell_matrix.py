"""Author and verify side-view combat water using actual parcel and camera samples."""
import csv
from pathlib import Path
from statistics import median
import hashlib
import math

from compose_water import compose
from fluid_replay_checks import live, read_state, require

PROFILES = {
    "UpwellGeyser": "upwell-geyser", "ChargedSurge": "charged-surge", "MovingWardJet": "moving-ward-jet",
    "ShieldIntercept": "shield-intercept", "RecastRelay": "recast-relay", "RadialBreak": "radial-break",
}
CASES = tuple(PROFILES)


def author(project, template):
    authored = {}
    for name, profile in PROFILES.items():
        prefab, _ = compose(project, template, "Assets/GameWater", name, profile, {})
        authored[name] = {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                          for p in sorted(prefab.parent.iterdir()) if p.is_file()}
    return authored


def change_mask(left, right):
    from PIL import Image, ImageChops

    with Image.open(left) as a, Image.open(right) as b:
        channels = ImageChops.difference(a.convert("RGB"), b.convert("RGB")).split()
        largest = ImageChops.lighter(ImageChops.lighter(channels[0], channels[1]), channels[2])
        return largest.point(lambda value: 255 if value >= 8 else 0)


def contacts(case, state, inputs, radius):
    inside = {"normal": 0, "control": 0}
    nearest = float("inf")
    displaced = []
    deflected = []
    movement = []
    for frame in range(6, 61, 6):
        row = inputs["normal"][frame]
        x, y = float(row["anchorX"]), float(row["anchorY"])
        movement.append((x, y))
        samples = {mode: state(mode, frame) for mode in inside}
        for mode, rows in samples.items():
            distance = [math.dist(p[:3], (x, y, 0.0)) for p in rows]
            inside[mode] += sum(d < radius - .025 for d in distance)
            if mode == "normal" and distance:
                nearest = min(nearest, min(distance))
        for normal, clear in zip(samples["normal"], samples["control"]):
            if normal[3] == clear[3] and math.dist(clear[:3], (x, y, 0.0)) < radius:
                displaced.append(math.dist(normal[:3], clear[:3]))
                deflected.append(math.dist(normal[4:7], clear[4:7]))
    require(nearest >= radius - .012 and inside["normal"] == 0 and inside["control"] >= 3,
            f"{case} must exclude real parcels that cross its matching unobstructed path")
    require(displaced and max(displaced) > .11 and max(deflected) > .22,
            f"{case} must change the trajectories and velocities of incident water")
    x_span = max(x for x, _ in movement) - min(x for x, _ in movement)
    y_span = max(y for _, y in movement) - min(y for _, y in movement)
    if case == "MovingWardJet":
        require(x_span > .75 and y_span > .55, "The receiving ward must traverse the jet in XY")
    return {"solidRadiusMetres": radius, "nearestNormalDistanceMetres": nearest,
            "normalInteriorParcelSamples": inside["normal"], "unobstructedInteriorParcelSamples": inside["control"],
            "maximumIncidentDisplacementMetres": max(displaced), "maximumVelocityChangeMetresPerSecond": max(deflected),
            "targetTravelXMetres": x_span, "targetTravelYMetres": y_span}


def verify_case(folder, case, *, full_control=False):
    inputs = {}
    for mode in ("normal", "control"):
        with (folder / mode / "input.csv").open(encoding="utf-8") as stream:
            inputs[mode] = list(csv.DictReader(stream))
        require([int(row["frame"]) for row in inputs[mode]] == list(range(96)), case + " input clock is incomplete")
        require(inputs[mode][-1]["stage"] == "Recycled", case + " did not retire")
    normal, control = folder / "normal", folder / "control"
    require(sorted(p.name for p in normal.glob("*.png")) == [f"{n:04d}.png" for n in range(96)],
            case + " is missing native film frames")
    probes = list(range(0, 96, 6))
    control_frames = list(range(96)) if full_control else [*probes, 95]
    require(sorted(p.name for p in control.glob("*.png")) == [f"{n:04d}.png" for n in control_frames],
            case + " is missing counterfactual film frames")
    require(all((a["anchorX"], a["anchorY"]) == (b["anchorX"], b["anchorY"])
                for a, b in zip(inputs["normal"], inputs["control"])), case + " changed its visible target trajectory")
    expected = {f"state-{n:04d}.bin" for n in probes}
    for mode in (normal, control):
        require({p.name for p in mode.glob("*.bin")} == expected, case + " missing a GPU readback")
    differences = {frame: change_mask(normal / f"{frame:04d}.png", control / f"{frame:04d}.png").histogram()[255]
                   for frame in probes}
    peak = max(differences, key=differences.get)
    require(differences[0] == 0 and differences[peak] > 120, case + " lacks an isolated visible fluid response")

    def state(mode, frame):
        return live(read_state(folder / mode / f"state-{frame:04d}.bin")[0])

    result = {"matchedFrames": 96, "matchedControlFrames": len(control_frames), "matchedGPUReadbacks": len(probes) * 2,
              "largestSampledPixelDifference": differences[peak], "largestDifferenceFrame": peak}
    if case in ("UpwellGeyser", "MovingWardJet", "ShieldIntercept"):
        radius = .50 if case == "ShieldIntercept" else .37
        result.update(contacts(case, state, inputs, radius))
        if case == "UpwellGeyser":
            start = state("normal", 6)
            upward = max(p[5] for p in start)
            require(upward > 4, "The geyser must accelerate upward into the receiver")
            result["earlyUpwardSpeedMetresPerSecond"] = upward
    elif case == "ChargedSurge":
        full, tap = (median(p[0] for p in state(mode, 18)) for mode in inputs)
        require(full - tap > .5, "Full charge needs more forward reach than a short tap")
        result.update({"fullChargeEnergy": 1, "tapEnergy": .2, "medianForwardLeadAt060sMetres": full - tap})
    elif case == "RecastRelay":
        require(all(differences[f] == 0 for f in probes if f <= 30), "The first cast and interruption must match")
        require(inputs["normal"][24]["stage"] == "Recycled" and inputs["normal"][36]["cast"] == "2"
                and inputs["control"][36]["stage"] == "Recycled", "The interruption must permit a same-instance recast")
        before, after = (normal / f"state-{f:04d}.bin" for f in (6, 36))
        require(before.read_bytes() == after.read_bytes(), "Reusing the seeded solver must clear the previous cast")
        first = change_mask(normal / "0006.png", folder / "dry.png")
        second = change_mask(normal / "0036.png", control / "0036.png")
        x0, _, x1, _ = first.getbbox()
        x2, _, x3, _ = second.getbbox()
        require((x0 + x1) / 2 < first.width * .40 and (x2 + x3) / 2 > second.width * .60,
                "The recast must visibly exchange sides on the actual gameplay camera")
        rightward = median(p[4] for p in state("normal", 36))
        require(rightward > 1.5 and differences[42] > 500, "The new cast must carry water with reversed world facing")
        result.update({"interruptFrame": 14, "secondCastFrame": 30, "matchedLocalStateAtAgeFrames": 6,
                       "firstWetBoundsX": [x0, x1], "mirroredWetBoundsX": [x2, x3],
                       "secondWorldMedianVelocityX": -rightward})
    elif case == "RadialBreak":
        sample = state("normal", 6)
        opposed = sum(p[4] < -1 for p in sample), sum(p[4] > 1 for p in sample)
        rising = sum(p[5] > 1 for p in sample)
        falling = sum(p[5] < -1 for p in sample)
        require(min(*opposed, rising, falling) > 5, "The burst must eject real fluid into all four XY directions")

        def span(mode):
            x = sorted(p[0] for p in state(mode, 18))
            return x[int(.95 * len(x))] - x[int(.05 * len(x))]
        spread, low = span("normal"), span("control")
        require(spread - low > .42, "The outward impulse must change the horizontal parcel envelope")
        result.update({"leftRightUpDownParcelCountsAt020s": [*opposed, rising, falling],
                       "normal90PercentWidthAt060sMetres": spread, "lowImpulse90PercentWidthAt060sMetres": low})
    return result


def verify(work):
    from fluid_replay_checks import match_receivers

    results = {}
    for case in CASES:
        a, b = (Path(work) / lane / "captures" / case for lane in ("A", "B"))
        match_receivers(a, b, case)
        results[case] = verify_case(a, case)
    return results
