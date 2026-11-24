"""Check falling drops and a fountain from the generated recipes' parcel and surface histories."""
from statistics import median
from fluid_replay_checks import live, match_receivers, read_state, require

INLETS = {"RainOnPool": "rain", "LedgeDrip": "drip", "SpringFountain": "fountain"}


def review_inlets(work):
    results = {}
    for name in INLETS:
        a, b = (work / lane / "captures" / name for lane in ("A", "B"))
        match_receivers(a, b, name)
        frames = sorted((a / "normal").glob("*.png"))
        require(len(frames) == 114 and frames[0].read_bytes() == frames[-1].read_bytes() == (a / "dry.png").read_bytes(),
                f"{name} must form and return to its dry plate")
        require(frames[45].read_bytes() != (a / "alternate/0045.png").read_bytes(), f"No seed variation in {name}")
        rows = [read_state(p) for p in sorted((a / "normal").glob("*.bin"))]
        controls = [read_state(p) for p in sorted((a / "no-waves").glob("*.bin"))]
        wave = [sum(map(abs, height)) for _, height in rows]
        impact = next(j for j, strength in enumerate(wave) if strength > .0001)
        require(impact > 2 and max(wave) > .15 and max(abs(v) for _, h in controls for v in h) == 0,
                f"{name} failed first contact or wave-off isolation")
        air = [r for particles, _ in rows for r in live(particles) if r[1] > .14]
        xspan = max(r[0] for r in air) - min(r[0] for r in air)
        data = {"matchedFrames": len(frames), "matchedNormalStates": len(rows),
                "firstMeasuredImpactSeconds": impact / 10, "airborneSpanMetres": xspan, "peakHeightSum": max(wave)}
        if name == "SpringFountain":
            height = max(r[1] for r in air)
            require(height > 1.5 and min(r[5] for r in air) < -1 and max(r[5] for r in air) > 3,
                    "Fountain requires an ascending and descending branch")
            data["apexMetres"] = height
        else:
            births = set(r[3] for r in air)
            require(max(r[5] for r in air) < .18 and median(r[5] for r in air) < -.8, f"{name} lacks downward momentum")
            if name == "RainOnPool": require(xspan > 3 and len(births) > 35, "Rain needs distributed arrivals")
            if name == "LedgeDrip": require(xspan < .32 and len(births) == 4, "The ledge must release four distinct drops")
            data["distinctBirths"] = len(births)
        results[name] = data
    return results
