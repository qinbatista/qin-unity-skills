"""Replay supported extremes through authored prefabs, GPU state and the XY camera."""
from hashlib import sha256
from statistics import median

from compose_water import compose
from fluid_replay_checks import live, match_receivers, read_state, require

BRIEFS = {
    "WeightlessMass": ("mass", {"gravity": 0, "duration": 3.6}),
    "SingleLedgeDrop": ("drip", {"count": 1, "span": 0}),
    "WideCloudburst": ("rain", {"count": 160, "span": 5, "rain-exposure": .035}),
}
CASES = tuple(BRIEFS)


def brief(name, edition):
    profile, settings = BRIEFS[name]
    if name == "WideCloudburst" and edition == "original":
        return profile, {**settings, "rain-exposure": 0}
    return profile, settings


def author(project, template, edition="refined"):
    assets = {}
    for name in CASES:
        profile, settings = brief(name, edition)
        prefab, _ = compose(project, template, "Assets/GameWater", name, profile, settings)
        assets[name] = {file.name: sha256(file.read_bytes()).hexdigest()
                        for file in sorted(prefab.parent.iterdir()) if file.is_file()}
    return assets


def verify(work):
    results = {}
    for name in CASES:
        a, b = (work / lane / "captures" / name for lane in ("A", "B"))
        match_receivers(a, b, name)
        frames = sorted((a / "normal").glob("*.png"))
        dry = (a / "dry.png").read_bytes()
        require(len(frames) == 114 and frames[0].read_bytes() == frames[-1].read_bytes() == dry,
                f"{name} failed its fixed-time capture or teardown")
        require(any(frame.read_bytes() != (a / "normal" / frame.name).read_bytes()
                    for frame in (a / "alternate").glob("*.png")),
                f"{name} lost seed variation")
        samples = sorted((a / "normal").glob("state-*.bin"))
        states = [read_state(s) for s in samples]
        result = {"matchedFrames": len(frames), "matchedStateSamples": len(states)}
        if name == "WeightlessMass":
            frame = 24
            mass, _ = read_state(a / "normal/state-0024.bin")
            falling, _ = read_state(a / "gravity-on/state-0024.bin")
            separation = median(p[1] for p in live(mass)) - median(p[1] for p in live(falling))
            require(separation > .7 and frames[frame].read_bytes() != (a / "gravity-on/0024.png").read_bytes(),
                    "Restoring gravity must lower the same seeded water mass and change its silhouette")
            result["heightDifferenceMetresAt080s"] = separation
        else:
            air = [p for parcels, _ in states for p in live(parcels) if p[1] > .14]
            births = len({p[3] for p in air})
            width = max(p[0] for p in air) - min(p[0] for p in air)
            amplitude = max(sum(abs(y) for y in height) for _, height in states)
            controls = [read_state(s)[1] for s in (a / "no-waves").glob("state-*.bin")]
            require(controls and all(not any(waves) for waves in controls) and amplitude > 0,
                    f"{name} did not transfer an impact to the pool")
            if name == "SingleLedgeDrop":
                require(births == 1 and width < .3, "A one-drop inlet must have one localized birth")
            else:
                require(births == 160 and width > 4, "The maximum drop count and span must reach the receiver")
            result.update(distinctBirths=births, airborneSpanMetres=width, peakHeightSum=amplitude)
        results[name] = result
    return results
