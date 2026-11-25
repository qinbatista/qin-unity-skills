"""Exercise the minimum, inherited default and maximum of every composer input."""
import argparse
from itertools import product
import json
from pathlib import Path
import re
import shutil

from compose_water import (CHANNEL_BASES, DROP_DEFAULTS, PARCEL_BASES, SPELL_BASES,
                           TOPDOWN_BASES, TOPDOWN_RANGES, channel_ranges, compose, parcel_ranges)
from fluid_mixer import DEFAULT, install, meta_guid


def recipe_values(source):
    values = re.findall(r"^  (_\w+): ([-.\d]+)$", source, re.MULTILINE)
    return {key: float(value) for key, value in values}


def inputs(base, profile):
    if profile in CHANNEL_BASES or profile in TOPDOWN_BASES:
        if profile in CHANNEL_BASES:
            directory, parent, donor, ranges = "Channels", "ChannelWater2D", CHANNEL_BASES[profile], channel_ranges(profile)
        else:
            directory, parent, donor, ranges = "TopDown", "TopDownWater2D", TOPDOWN_BASES[profile], TOPDOWN_RANGES
        source = (base / directory / (donor + ".prefab")).read_text(encoding="utf-8")
        fields = re.findall(r"propertyPath: (_\w+)\n      value: ([-.\d]+)", source)
        defaults = {key: float(value) for key, value in fields}
        defaults["sourcePrefabGuid"] = meta_guid(base / directory / (parent + ".prefab.meta")).decode()
        return ranges, defaults
    if profile in SPELL_BASES:
        name = SPELL_BASES[profile]
        donor = base / "Spells" / name / (name + ".asset")
    else:
        donor = base / "Presets" / (PARCEL_BASES[profile] + ".asset")
    defaults = recipe_values(donor.read_text(encoding="utf-8"))
    defaults.update(DROP_DEFAULTS.get(profile, {}))
    return parcel_ranges(profile), defaults


def read_authored(prefab, ranges, overrides, defaults):
    text = prefab.read_text(encoding="utf-8")
    if not meta_guid(Path(str(prefab) + ".meta")):
        raise ValueError(f"Missing authored prefab identity at {prefab}")
    if "_feed" in defaults:
        recipe = prefab.with_suffix(".asset")
        guid = meta_guid(Path(str(recipe) + ".meta")).decode()
        if guid not in text or f"m_Name: {prefab.stem}\n" not in recipe.read_text(encoding="utf-8"):
            raise ValueError(f"Broken recipe binding at {prefab}")
        actual = recipe_values(recipe.read_text(encoding="utf-8"))
    else:
        fields = re.findall(r"propertyPath: (_\w+)\n      value: ([-.\d]+)", text)
        actual = {key: float(value) for key, value in fields}
        parent = re.search(r"m_SourcePrefab: \{[^}]+guid: ([0-9a-f]{32})", text)
        if not parent or parent[1] != defaults["sourcePrefabGuid"]:
            raise ValueError(f"Missing source prefab reference at {prefab}")
    for key, (field, _, _) in ranges.items():
        if key in overrides or field in defaults:
            expected = overrides.get(key, defaults.get(field))
            if actual.get(field) != expected:
                raise ValueError(f"{prefab.stem} serialized {field} as {actual.get(field)}, expected {expected}")


def sweep(work):
    work.mkdir(parents=True, exist_ok=False)
    project = work / "receiver"
    (project / "Assets").mkdir(parents=True)
    (project / "ProjectSettings").mkdir()
    (project / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 6000.6.7f1\n")
    base, _ = install(project)
    results = {}
    for profile in (*CHANNEL_BASES, *TOPDOWN_BASES, *PARCEL_BASES, *SPELL_BASES):
        ranges, defaults = inputs(base, profile)
        keys = list(ranges)
        name = "Sweep" + "".join(word.title() for word in profile.split("-"))
        output = project / "Assets/WaterSweep" / name
        counts = {"authored": 0, "incompatibleSchedules": 0, "invalidInputs": 0}
        for values in product(*[(None, low, high) for _, low, high in ranges.values()]):
            overrides = {key: value for key, value in zip(keys, values) if value is not None}
            invalid = False
            if "_feed" in defaults:
                duration = overrides.get("duration", defaults["_duration"])
                feed = overrides.get("feed", defaults["_feedDuration"])
                invalid = feed > duration or (defaults["_feed"] == 6 and duration + 1e-6 < max(.34, defaults.get("_curtainHold", .5)) + 1.4)
            try:
                prefab, _ = compose(project, DEFAULT, "Assets/WaterSweep", name, profile, overrides)
            except ValueError:
                if not invalid or output.exists():
                    raise
                counts["incompatibleSchedules"] += 1
                continue
            if invalid:
                raise AssertionError(f"Accepted incompatible schedule for {profile}: {overrides}")
            read_authored(prefab, ranges, overrides, defaults)
            counts["authored"] += 1
            shutil.rmtree(output)
        bad_inputs = [{key: bad} for key, (_, low, high) in ranges.items()
                      for bad in (low - .01, high + .01, float("nan"), float("inf"), -float("inf"))]
        bad_inputs += [{"depth" if "_feed" in defaults else "count": 2}]
        if "count" in ranges:
            bad_inputs.append({"count": 1.5})
        for overrides in bad_inputs:
            try:
                compose(project, DEFAULT, "Assets/WaterSweep", name, profile, overrides)
            except ValueError:
                if output.exists():
                    raise AssertionError(f"A rejected input left a partial prefab at {output}")
                counts["invalidInputs"] += 1
            else:
                raise AssertionError(f"Accepted unsupported input for {profile}: {overrides}")
        results[profile] = counts
    totals = {key: sum(row[key] for row in results.values()) for key in counts}
    report = {"levels": ["inherited", "minimum", "maximum"], "profiles": results, "totals": totals}
    (work / "WaterAuthoringSweep.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n")
    return totals


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", required=True, type=Path, help="A new output directory in task Cache")
    print(json.dumps(sweep(parser.parse_args().work.resolve()), sort_keys=True))
