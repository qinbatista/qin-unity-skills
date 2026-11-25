"""Measure the 2D hit and continuous inlet in two native receivers."""
from math import dist
from pathlib import Path
from statistics import median

from fluid_replay_checks import live, match_receivers, read_state, require

APPLICATIONS = {"curtain": ("ApplyWaterCurtain", "Curtain"),
                "waterfall": ("ApplyContinuousWaterfall", "Waterfall2D")}


def sample(folder, tick, mode="normal"):
    return read_state(folder / mode / f"state-{tick:02d}.bin")


def frame(folder, tick, mode="normal"):
    return folder / mode / f"{tick:04d}.png"


def curtain_contacts(folder):
    contacts = {}
    for mode in ("normal", "miss", "variation"):
        impact, anchor = (folder / mode / "contact.txt").read_text().splitlines()
        contacts[mode] = (int(impact), float(anchor))
    hit, x = contacts["normal"]
    require(21 <= hit <= 28 and contacts["variation"] == (hit, x) and contacts["miss"][0] == -1,
            "The swept 2D projectile did not create exactly one seed-independent hit and a clean miss")
    require(not list((folder / "miss").glob("state-*.bin")), "A curtain was admitted on a miss")
    require(all(frame(folder, k).read_bytes() == frame(folder, k, "miss").read_bytes() for k in range(hit - 2)),
            "The missed cast changed the water before contact")
    return hit, x


def curtain_physics(folder):
    hit, x = curtain_contacts(folder)
    early = live(sample(folder, 3)[0])
    open_body, open_wave = sample(folder, 14)
    peak = live(open_body)
    closing = live(sample(folder, 26)[0])
    after, wave = sample(folder, 45)
    width = median(abs(p[0]) for p in peak if p[1] > .30)
    narrow = median(abs(p[0]) for p in closing if p[1] > .30)
    draining = sum(p[1] < .125 for p in live(after)) / len(live(after))
    require(early and max(p[1] for p in early) < .35 and max(p[1] for p in peak) > 1.30,
            "The water curtain did not rise progressively from the contact")
    require(width > .51 and narrow < .52 * width, "Curtain sides did not converge")
    require(draining > .4 and sum(map(abs, wave)) > sum(map(abs, open_wave)),
            "Curtain runoff did not transfer to the receiving surface")
    require(frame(folder, hit + 14).read_bytes() != frame(folder, hit + 14, "variation").read_bytes(),
            "Seeded curtain breakup did not change the image")
    return {"impactFrame": hit, "anchorX": x, "closureStartAfterImpactSeconds": .50,
            "openMedianHalfWidthMetres": width, "closingMedianHalfWidthMetres": narrow,
            "floorParcelFractionAt150sAfterHit": draining,
            "opticsFrame": hit + 14}


def valve_control(folder):
    def live_count(tick, mode):
        return len(live(sample(folder, tick, mode)[0]))

    before = 66
    require(sample(folder, before) == sample(folder, before, "early-stop")
            and frame(folder, before).read_bytes() == frame(folder, before, "early-stop").read_bytes(),
            "Changing the future valve closing time changed the current fluid or image")
    ticks = (81, 96, 132, 162)
    early = [live_count(tick, "early-stop") for tick in ticks]
    normal = [live_count(tick, "normal") for tick in ticks]
    require(0 < early[1] < early[0] < 384 and early[-1] == 0 and normal == [384] * len(ticks),
            "An early valve closure must leave downstream parcels, drain them and diverge from the open inlet")
    require(frame(folder, 162, "early-stop").read_bytes() == (folder / "dry.png").read_bytes()
            and frame(folder, 162).read_bytes() != frame(folder, 162, "early-stop").read_bytes(),
            "The early valve closure must remove the drawn water while the other inlet remains visible")
    return {"earlyCloseSeconds": 2.4, "normalCloseSeconds": 7.4, "matchedBeforeCloseFrame": before,
            "sampleFrames": list(ticks), "earlyLiveParcels": early, "normalLiveParcels": normal,
            "earlyClosureMatchesDryAtFrame": 162}


def waterfall_physics(folder):
    steady = (66, 81, 96, 132, 162, 190, 221)
    snapshots = [sample(folder, k) for k in steady]
    early, early_wave = sample(folder, 12)
    minimum_inlet = min(sum(p[1] > 1.94 and p[5] < -.3 for p in live(ps)) for ps, _ in snapshots)
    minimum_wave = min(sum(map(abs, w)) for _, w in snapshots)
    clearance = min(dist(p[:3], (-.26, .79, 0.0)) for ps, _ in snapshots for p in live(ps))
    require(clearance >= .519, "A waterfall parcel penetrated the solid boundary")
    require(min(len(live(ps)) for ps, _ in snapshots) == 384 and minimum_inlet >= 22,
            "The waterfall developed a gap or lost its supply across the old cast duration")
    require(sum(map(abs, early_wave)) < .001 and minimum_wave > 6.0,
            "The basin must receive its first wave from actual wet contact")
    require(max(abs(z) for k in steady for z in sample(folder, k, "no-waves")[1]) < 1e-6,
            "Waterfall no-wave control has surface disturbance")
    later = [live(sample(folder, k)[0]) for k in (231, 253, 279)]
    settled = sum(p[1] < .17 for p in later[1]) / len(later[1])
    require(len(live(early)) < 160 and len(later[2]) == 0 and len(later[1]) < len(later[0]) < 384 and
            settled >= .90 and max(p[1] for p in later[1]) < 1.60,
            "Closing the source must drain the falling sheet before retiring the basin")
    require(frame(folder, 162).read_bytes() != frame(folder, 162, "variation").read_bytes(),
            "Seeded waterfall detail did not change the image")
    return {"valveControl": valve_control(folder), "feedOpenSeconds": 7.4, "steadySamples": len(steady),
            "minimumFallingParcelsAtInlet": minimum_inlet, "minimumPoolHeightMagnitude": minimum_wave,
            "minimumDistanceFromRockCentreMetres": clearance,
            "parcelsAfterShutoff": [len(p) for p in later], "settledFractionAt843s": settled,
            "opticsFrame": 162}


def review_application(work, case):
    _, name = APPLICATIONS[case]
    a, b = (Path(work) / lane / "captures" / name for lane in ("A", "B"))
    match_receivers(a, b, name)
    duration_frames = 92 if case == "curtain" else 292
    frames = sorted((a / "normal").glob("*.png"))
    dry = (a / "dry.png").read_bytes()
    require(len(frames) == duration_frames and frames[0].read_bytes() == frames[-1].read_bytes() == dry,
            name + " did not begin and end at the same dry frame")
    report = curtain_physics(a) if case == "curtain" else waterfall_physics(a)
    at_optics = frame(a, report["opticsFrame"]).read_bytes()
    offs = [(a / f).read_bytes() for f in ("no-chroma-reversal.png", "no-optics.png")]
    require(len(set([at_optics, *offs])) == 3, name + " did not exercise scene refraction and local colour reversal")
    return {"equalFrames": len(frames), "equalStateSamples": len(list(a.rglob("*.bin"))),
            "physics": report}
