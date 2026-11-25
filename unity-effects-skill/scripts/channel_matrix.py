"""Author six side-elevation water boundaries and verify their native causal responses."""
from pathlib import Path
from statistics import mean
from math import sqrt

from channel_replay_checks import load_surface
from compose_water import compose
from fluid_mixer import digest
from fluid_replay_checks import match_receivers, require

PROFILES = {"DryDamBreak": "dam-dry", "WetBedBore": "dam-wet", "ReservoirFill": "fill",
            "ReservoirDrawdown": "drain", "TidalReversal": "tide", "DrivenTank": "tilt"}
CASES = tuple(PROFILES)
DX = 6 / 128


def author(project, template):
    outputs = {}
    for name, profile in PROFILES.items():
        prefab, _ = compose(project, template, "Assets/GameWater", name, profile, {})
        outputs[name] = {file.name: digest(file) for file in sorted(prefab.parent.iterdir()) if file.is_file()}
    return outputs


def measure(path):
    surface, motion = load_surface(path)
    depths = [top - bed for top, bed, _, _ in surface]
    require(min(depths) >= -1.e-6, f"Negative water depth in {path}")
    return {"area": sum(depths) * DX, "edge": max((i for i, h in enumerate(depths) if h > .035), default=-1),
            "station": mean(depths[87:94]), "flow": mean(motion[i][0] * depths[i] for i in range(25, 40)),
            "leftDepth": depths[0], "rightDepth": depths[-1],
            "speed": max(abs(v[0]) for v in motion),
            "tilt": mean(v[0] for v in surface[-8:]) - mean(v[0] for v in surface[:8])}


def verify(work):
    report = {}
    for name in CASES:
        a, b = (Path(work) / lane / "captures" / name for lane in ("A", "B"))
        match_receivers(a, b, name)
        frames = sorted((a / "normal").glob("*.png"))
        require([p.stem for p in frames] == [f"{i:04d}" for i in range(166)], f"{name} has missing camera frames")
        dry = (a / "dry.png").read_bytes()
        require(frames[0].read_bytes() == dry == frames[-1].read_bytes(), f"{name} lifecycle did not clear")
        controls = sorted((a / "control").glob("*.png"))
        changed = sum((a / "normal" / p.name).read_bytes() != p.read_bytes() for p in controls)
        require(changed >= 12, f"{name} generated too few visible changes against the stationary control")
        gauge = {int(frame): int(pixels) for frame, pixels in
                 (line.split(",") for line in (a / "gauge-pixels.csv").read_text().splitlines())}
        require(set(gauge) == {int(p.stem) for p in controls} and max(gauge.values()) > 250
                and sum(count > 30 for count in gauge.values()) > 6,
                f"{name} lacks a sustained camera change in the gauge region clear of all hardware")
        expected = [f"{i:04d}.bin" for i in range(0, 151, 3)]
        normal, control = ([measure(a / (mode + "-state") / p) for p in expected] for mode in ("normal", "control"))
        require((a / "normal-state" / expected[0]).read_bytes() == (a / "control-state" / expected[0]).read_bytes(),
                f"{name} did not use matched initial conditions")
        max_still = max(r["speed"] for r in control)
        require(max_still < .002, f"{name} zero driver produced speed {max_still}")
        area = [r["area"] for r in normal]
        drift = max(abs(v - area[0]) for v in area)
        metrics = {"matchedFrames": len(frames), "matchedControlFrames": len(controls), "changedControlFrames": changed,
                   "matchedStatesPerMode": len(expected), "controlPeakSpeedMps": max_still,
                   "peakDownstreamChangedPixels": max(gauge.values()), "downstreamRegionPixels": 248 * 225}
        if name in {"DryDamBreak", "WetBedBore", "DrivenTank"}:
            require(drift < .005, f"{name} closed-basin area changed by {drift} m²")
            metrics["closedAreaMaxDriftM2"] = drift
        if name == "DryDamBreak":
            advance = (max(r["edge"] for r in normal) - normal[0]["edge"]) * DX
            require(normal[0]["station"] < .001 and advance > 2.4 and max(r["station"] for r in normal) > .15,
                    f"{name} failed to inundate its dry reach")
            arrival = next(i for i, v in enumerate(normal) if v["station"] > .035) / 10
            require(arrival > .65, f"{name} wetted the downstream station before the release could arrive")
            metrics.update(wetFrontAdvanceM=advance, downstreamArrivalS=arrival)
        elif name == "WetBedBore":
            rise = max(r["station"] for r in normal[:22]) - normal[0]["station"]
            require(normal[0]["edge"] == 127 and normal[0]["station"] > .20 and rise > .12,
                    f"{name} failed to deliver a bore to the already-wet gauge")
            metrics.update(initialGaugeDepthM=normal[0]["station"], boreGaugeRiseM=rise)
        elif name in {"ReservoirFill", "ReservoirDrawdown"}:
            gain = 1 if name == "ReservoirFill" else -1
            amount = area[-1] - area[0]
            require(amount * gain > .65 and all((b - a) * gain > -.0001 for a, b in zip(area, area[1:])),
                    f"{name} failed its signed boundary-volume budget: {amount}")
            # Integrate independent 10 Hz port gauges through the prefab's documented discharge law.
            rates = []
            for i, record in enumerate(normal):
                w = min(1, i / (10 * .38))
                ramp = w * w * (3 - 2 * w)
                if name == "ReservoirFill":
                    rate = .36 * 1.12 * min(1, max(0, (1.04 - record["leftDepth"]) / .32))
                else:
                    rate = -.37 * 1.25 * sqrt(max(0, record["rightDepth"]))
                rates.append(rate * ramp)
            predicted = sum((a + b) * .05 for a, b in zip(rates, rates[1:]))
            require(abs(amount - predicted) < .045, f"{name} volume disagrees with the independently integrated port flow")
            metrics.update(areaChangeM2=amount, estimatedPortIntegralM2=predicted, finalGaugeDepthM=normal[-1]["station"])
        elif name == "TidalReversal":
            flow = [r["flow"] for r in normal]
            require(min(flow) < -.12 and max(flow) > .12, f"{name} lacks ebb or flood discharge: {min(flow)}, {max(flow)}")
            metrics["gaugeDischargeRangeM2ps"] = [min(flow), max(flow)]
        else:
            tilt = [r["tilt"] for r in normal]
            residual = max(r["speed"] for r in normal[40:])
            require(min(tilt) < -.10 and max(tilt) > .10 and residual > .10,
                    f"{name} lacks two wall signs or post-drive inertia")
            metrics.update(wallHeightDifferenceRangeM=[min(tilt), max(tilt)], postForcePeakSpeedMps=residual)
        report[name] = metrics
    return report
