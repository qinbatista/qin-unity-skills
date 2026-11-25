"""Author six top-down water variants and compare their native current and height fields."""
from array import array
import math
from pathlib import Path
import sys

from compose_water import compose
from fluid_mixer import digest
from fluid_replay_checks import match_receivers, require

CASES = ("BoatWake", "IslandCurrent", "StreamConfluence", "ForkedChannel", "DrainVortex", "RainPond")
PROFILES = ("boat-wake", "island-current", "stream-confluence", "forked-channel", "drain-vortex", "rain-pond")
SAMPLES = (0, 30, 60, 105, 150, 179)


def author(project, template):
    result = {}
    for name, profile in zip(CASES, PROFILES):
        path, _ = compose(project, template, "Assets/GameWater", name, profile, {})
        result[name] = {p.name: digest(p) for p in sorted(path.parent.iterdir()) if p.is_file()}
    return result


def state(folder, tick):
    field = array("f")
    field.frombytes((folder / f"field-{tick:04d}.bin").read_bytes())
    if sys.byteorder != "little":
        field.byteswap()
    require(len(field) == 128 * 72 * 4 and all(map(math.isfinite, field)), "Invalid top-down RGBA32F grid")
    return field


def region(field, select, quantity):
    values = []
    for j in range(72):
        for i in range(128):
            x, y = (i + .5) * .0625 - 4, (j + .5) * .0625 - 2.25
            if select(x, y):
                k = 4 * (128 * j + i)
                values.append(quantity(x, y, *field[k:k + 4]))
    return sum(values) / len(values)


def verify(work):
    result = {}
    for name in CASES:
        a, b = [Path(work) / lane / "captures" / name for lane in ("A", "B")]
        match_receivers(a, b, name)
        normal, off = a / "normal", a / "off"
        frames = [f"{i:04d}.png" for i in range(180)]
        require([p.name for p in sorted(normal.glob("*.png"))] == frames, name + " missing normal frames")
        for tick in SAMPLES:
            require(not any(state(off, tick)), name + " water moved with its source disconnected")
        require(not any(state(normal, 0)) and (normal / frames[0]).read_bytes() == (off / frames[0]).read_bytes(),
                name + " must start from the same undisturbed field and image")
        for tick in SAMPLES[2:]:
            require((normal / frames[tick]).read_bytes() != (off / frames[tick]).read_bytes(), name + " has invisible input")
        late = state(normal, 179)
        coast = state(a / "coast", 12)
        residual = sum(abs(coast[i]) for i in range(0, len(coast), 4))
        require(coast != late and residual > .01, name + " must carry evolving water after source cutoff")
        extra = {}
        if name == "RainPond":
            annulus = lambda x, y: .75 < math.hypot(x + 1.15, y - .32) < 1.9
            magnitude = lambda x, y, h, u, v, tracer: abs(h)
            pulse = [state(a / "impulse", tick) for tick in (1, 30)]
            first, later = [region(field, annulus, magnitude) for field in pulse]
            # Gravity-wave energy per unit area, normalized by its constant height coefficient.
            # Drag and numerical dissipation must remove energy after the single disturbance.
            energy = [sum(field[k] ** 2 + (.48 / 1.6) ** 2 * (field[k+1] ** 2 + field[k+2] ** 2)
                          for k in range(0, len(field), 4)) for field in pulse]
            require(0 < energy[1] <= energy[0] * 1.001, "An isolated disturbance gained unforced wave energy")
            extra["isolatedImpulseNormalizedEnergy"] = energy
            hits = [state(a / "simultaneous", mask) for mask in (1, 2, 3)]
            error = max(abs(first_hit + second_hit - together) for first_hit, second_hit, together in zip(*hits))
            peaks = [max(field[::4]) for field in hits[:2]]
            spread = state(a / "simultaneous", 12)
            rings = [region(spread, lambda x, y: .28 < math.hypot(x - cx, y - cy) < .68, magnitude)
                     for cx, cy in ((-1.15, .32), (1.21, -.54))]
            require(peaks[0] > .085 and peaks[1] > .069 and error < 1e-6 and min(rings) > .0001,
                    "Two same-clock impacts must superpose and each create a travelling disturbance")
            extra["simultaneousImpacts"] = {"isolatedPeakMetres": peaks, "maximumSuperpositionError": error,
                                            "meanRingHeightAfter040s": rings}
            require(first < 1e-6 and later > 1e-5, "An isolated drop must reach the distant annulus after impact")
            extra["isolatedImpulseDistantMeanHeight"] = [first, later]
        result[name] = {"matchedFrames": len(frames), "matchedControlFrames": len(SAMPLES), "matchedPostCutoffFrames": 1,
                        "matchedFieldSamples": len(list(a.rglob("field-*.bin"))),
                        "postCutoffAbsoluteHeightSum": residual, **extra, **physics(name, state(normal, 105), late)}
    return result


def physics(name, mid, late):
    vx = lambda x, y, h, u, v, tracer: u
    vy = lambda x, y, h, u, v, tracer: v
    height = lambda x, y, h, u, v, tracer: abs(h)
    if name == "BoatWake":
        q = region(mid, lambda x, y: -2.8 < x < -.6 and .35 < abs(y) < 1.2, height)
        require(q > .0001, "The moving hull did not send a wave into its wake")
        return {"meanOffTrackWakeHeightMetres": q}
    if name == "IslandCurrent":
        flux = [region(late, lambda x, y: abs(x-.14)<.3 and .74 < s*y < 1.1, vx) for s in (-1, 1)]
        solid = region(late, lambda x,y: (x-.14)**2+(y-.02)**2 < .22**2,
                       lambda x,y,h,u,v,t: abs(h)+abs(u)+abs(v)+abs(t))
        require(min(flux) > .12 and solid == 0, "The island must exclude water and carry current on both sides")
        return {"lowerUpperCurrentMetresPerSecond": flux, "islandInteriorState": solid}
    if name in ("StreamConfluence", "ForkedChannel"):
        outgoing = name == "ForkedChannel"
        bands = [region(late, lambda x,y: 1.3 < (x if outgoing else -x) < 2.5 and .45 < s*y < 1.06, vy) for s in (-1,1)]
        trunk = region(late, lambda x,y: 1.4 < (-x if outgoing else x) < 2.7 and abs(y)<.35, vx)
        sign = 1 if outgoing else -1
        require(sign*bands[0] < -.025 and sign*bands[1] > .025 and trunk > .12, "Branches failed to split or merge current")
        return {"lowerUpperLateralCurrentMetresPerSecond": bands, "trunkCurrentMetresPerSecond": trunk}
    if name == "DrainVortex":
        ring = lambda x,y: .62 < math.hypot(x-.1,y+.09) < 1.2
        spin = region(late, ring, lambda x,y,h,u,v,t: ((x-.1)*v-(y+.09)*u)/math.hypot(x-.1,y+.09))
        center = region(late,lambda x,y: math.hypot(x-.1,y+.09)<.32,lambda x,y,h,u,v,t: h)
        require(spin > .1 and center < -.004, "The drain must spin water around a depressed centre")
        return {"meanTangentialSpeedMetresPerSecond": spin, "meanCoreDisplacementMetres": center}
    coverage = sum(abs(late[i]) > .0005 for i in range(0, len(late),4))
    peak = max(abs(late[i]) for i in range(0,len(late),4))
    require(coverage > 90 and peak > .004, "Rain failed to propagate beyond the drop footprint")
    return {"disturbedCells": coverage,"maximumWaveDisplacementMetres": peak}
