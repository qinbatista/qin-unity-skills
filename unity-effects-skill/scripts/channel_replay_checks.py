"""Verify a current, a moving shoreline and a reflecting basin from native GPU readbacks."""
from pathlib import Path
from statistics import mean
import math
import struct

from fluid_replay_checks import match_receivers, require

CHANNELS = ("RiverCurrent", "ShoreWash", "BasinSlosh", "CoveWash")
COVE = {"depth": .45, "bank": 1.12, "drive": .77, "period": 2.6}


def load_surface(path):
    values = list(struct.iter_unpack("<4f", path.read_bytes()))
    require(len(values) == 4 * 128 and all(math.isfinite(x) for row in values for x in row),
            f"Invalid field at {path}")
    return values[256:384], values[384:]


def review_channels(work):
    report = {}
    for name in CHANNELS:
        a, b = (Path(work) / lane / "captures" / name for lane in ("A", "B"))
        match_receivers(a, b, name)
        frames = sorted((a / "normal").glob("*.png"))
        require(len(frames) == 256 and frames[0].read_bytes() == frames[-1].read_bytes() == (a / "dry.png").read_bytes(),
                f"{name} did not form and retire cleanly")
        require(frames[60].read_bytes() != (a / "alternate.png").read_bytes()
                and frames[60].read_bytes() != (a / "still.png").read_bytes(), f"{name} lacks visual response")
        samples = [load_surface(p) for p in sorted((a / "normal-state").glob("*.bin"))]
        rest = [load_surface(p) for p in sorted((a / "still-state").glob("*.bin"))]
        max_still = max(abs(v[0]) for _, rows in rest for v in rows)
        require(max_still < .002, f"{name} failed the still-water control: {max_still}")
        data = {"matchedFrames": len(frames), "matchedNormalStates": len(samples), "maxStillSpeed": max_still}
        if name == "RiverCurrent":
            downstream = [mean(v[0] for v in rows[87:110]) for _, rows in samples[-10:]]
            require(min(downstream) > .18, f"{name} failed downstream transport: {downstream}")
            data["minimumLateDownstreamSpeed"] = min(downstream)
        elif name == "BasinSlosh":
            tilt = [mean(v[0] for v in surface[-10:]) - mean(v[0] for v in surface[:10]) for surface, _ in samples]
            volume = [sum(v[0] - v[1] for v in surface) * 6 / 128 for surface, _ in samples]
            loss = max(abs(v - volume[0]) for v in volume)
            require(max(tilt) > .11 and min(tilt) < -.10 and loss < .008,
                    f"{name} failed reversal or closed-volume conservation: {min(tilt)}, {max(tilt)}, {loss}")
            data.update(wallHeightDifferenceRange=[min(tilt), max(tilt)], maxVolumeDrift=loss)
        else:
            edges = [max(i for i, v in enumerate(surface) if v[0] - v[1] > .02) for surface, _ in samples]
            peak = max(range(len(edges)), key=edges.__getitem__)
            forward = (edges[peak] - edges[0]) * 6 / 128
            retreat = (edges[peak] - min(edges[peak:])) * 6 / 128
            require(forward > .14 and retreat > .14, f"{name} failed advance and return: {forward}, {retreat}")
            data.update(shoreAdvanceMetres=forward, subsequentRetreatMetres=retreat, firstPeakSeconds=peak / 5)
        report[name] = data
    require(abs(report["CoveWash"]["firstPeakSeconds"] - report["ShoreWash"]["firstPeakSeconds"]) > .15 or
            abs(report["CoveWash"]["shoreAdvanceMetres"] - report["ShoreWash"]["shoreAdvanceMetres"]) > .12,
            "The authored cove must have a different physical response from the parent shore")
    return report
