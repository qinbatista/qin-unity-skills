"""Compare native motion and population across three independently authored VFX Graph receivers."""
import argparse
import json
from pathlib import Path
import re
from statistics import mean, median

FORMS = ("Rain", "Embers", "Fountain", "Leaves", "Motes")


def require(value, message):
    if not value:
        raise ValueError(message)


def assess(form, run):
    normal = run["normal"]
    cohort = {s["tick"]: s for s in run["cohort"] if s["pixels"] >= 8}
    extremes = run.get("driverExtremes", [])
    require(len(extremes) == 12 and all(s["pixels"] > 0 for s in extremes),
            f"{form}: missing wide-camera captures at the two supported driver extremes")

    def phase(tick):
        require(tick in cohort, f"{form}: the short cohort must be visible at tick {tick}")
        return cohort[tick]
    def displacement(start, end, axis):
        return phase(end)[axis] - phase(start)[axis]
    if form == "Rain":
        fall = -displacement(18, 42, "centerY")
        raised = {s["tick"]: s for s in run["raisedGround"] if s["pixels"] >= 8}
        contact = [t for t in (60, 66) if t in raised and t in cohort]
        require(contact, "Rain: both planes must have a visible splash after the last parent drop hits")
        lift = median(raised[t]["centerY"] - cohort[t]["centerY"] for t in contact)
        require(fall > 28 and lift > 15, f"Rain: falling droplets and raised contact response required ({fall=}, {lift=})")
        splash = run["systems"].index("Splash")
        parent = run["systems"].index("Rain")
        require(phase(66)["alive"][parent] == 0 and phase(66)["alive"][splash] > 0,
                "Rain: the contact comparison must isolate the surviving splash cohort")
        lengths = [fall, lift]
    elif form == "Embers":
        rise = displacement(24, 72, "centerY")
        early = displacement(24, 36, "centerY")
        late = displacement(60, 72, "centerY")
        require(rise > 25 and late > early, f"Embers: buoyant rise must gain speed ({rise=}, {early=}, {late=})")
        lengths = [rise]
    elif form == "Fountain":
        apex = max(cohort.values(), key=lambda sample: sample["centerY"])
        rise = apex["centerY"] - phase(18)["centerY"]
        fall = apex["centerY"] - phase(78)["centerY"]
        require(30 <= apex["tick"] <= 72 and rise > 35 and fall > 12,
                f"Fountain: the short jet must rise, crest, and fall ({apex['tick']=}, {rise=}, {fall=})")
        lengths = [rise, fall]
    elif form == "Leaves":
        drift = displacement(24, 84, "centerX")
        descent = -displacement(24, 84, "centerY")
        require(drift > 35 and descent > 15, f"Leaves: the cohort must travel downwind and descend ({drift=}, {descent=})")
        lengths = [drift, descent]
    else:
        contraction = -displacement(24, 66, "meanRadius")
        require(contraction > 8, f"Motes: the original cohort must converge on the attractor ({contraction=})")
        lengths = [contraction]
    peak = [max(s["alive"][i] for s in normal) for i in range(len(run["systems"]))]
    require(len(peak) == (2 if form == "Rain" else 1), f"{form}: unexpected number of particle systems")
    require(all(0 < p <= c for p, c in zip(peak, run["capacities"])), f"{form}: each system must emit inside its capacity")
    require(len(normal) == 40 and run["retired"]["pixels"] == 0 and run["replay"]["pixels"] > 20, f"{form}: incomplete lifecycle")
    require(not any(run["retired"]["alive"]) and all(run["replay"]["alive"]),
            f"{form}: stopping must clear all systems and replay must emit again")
    shift = run["shifted"]["centerX"] - run["replay"]["centerX"]
    require(shift > 4, f"{form}: moving the driver did not move the visible result")
    mature = [s for s in normal if s["tick"] >= 90]
    return {
        "guid": run["graphGuid"], "travelPixels": lengths, "drivenShiftPixels": shift,
        "averageOccupiedPixels": round(mean(s["pixels"] for s in mature), 1),
        "peakAlive": peak, "capacities": run["capacities"], "systemNames": run["systems"],
        "driverExtremeSamples": len(extremes), "backend": run["graphics"]
    }


def verify_shader(receiver, name, system_count):
    project = receiver / "Project"
    graph = project / "Assets/GraphTrials" / (name + ".vfx")
    bindings = re.findall(r"^\s*shaderGraph: \{fileID: (?!0,)[^,]+, guid: ([0-9a-f]{32}), type: 3\}", graph.read_text(), re.M)
    require(len(bindings) == system_count, f"{name}: every output must have a live Shader Graph binding")
    shaders = list((project / "Library/PackageCache").glob("com.unity.visualeffectgraph*/ShaderGraph/VFXDefault.shadergraph"))
    require(len(shaders) == 1 and shaders[0].stat().st_size > 0, f"{name}: expected the resolved VFX Graph package shader")
    meta = shaders[0].with_name(shaders[0].name + ".meta").read_text()
    require(all("guid: " + guid in meta for guid in bindings), f"{name}: a saved output does not bind the resolved Shader Graph")
    return "Packages/com.unity.visualeffectgraph/ShaderGraph/VFXDefault.shadergraph"


def compare(roots):
    results = {}
    for form in FORMS:
        trials = [assess(form, json.loads((root / "out" / f"{form}-{trial}" / "result.json").read_text()))
                  for trial, root in enumerate(roots, 1)]
        require(len({trial["guid"] for trial in trials}) == 3, f"{form}: all graph identities must be fresh")
        for index, trial in enumerate(trials, 1):
            trial["shaderGraph"] = verify_shader(roots[index - 1], f"{form}-{index}", len(trial["systemNames"]))
        require(all(t["capacities"] == trials[0]["capacities"] for t in trials), f"{form}: unexpected capacity change")
        for values in zip(*(t["travelPixels"] for t in trials)):
            require(max(values) <= min(values) * 1.5 + 7, f"{form}: recreated trajectory differs: {values}")
        pixels = [t["averageOccupiedPixels"] for t in trials]
        require(max(pixels) <= min(pixels) * 1.75, f"{form}: recreated density differs: {pixels}")
        results[form] = trials
    return results


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("receivers", type=Path, nargs=3, help="Receiver roots in trial order.")
    parser.add_argument("--output", type=Path, help="Optional aggregate JSON path in task Cache.")
    args = parser.parse_args()
    results = compare(args.receivers)
    report = json.dumps(results, indent=2)
    if args.output:
        args.output.write_text(report + "\n")
    print(report)


if __name__ == "__main__":
    main()
