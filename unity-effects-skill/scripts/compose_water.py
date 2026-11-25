"""Author an editable 2D water prefab from a source, boundary and physical inputs."""
import argparse
import math
import os
from pathlib import Path
import re
import tempfile
import uuid

from fluid_mixer import DEFAULT, TEMPLATE, digest, meta_guid, project_lock

CHANNEL_BASES = {"river": "RiverCurrent", "shore": "ShoreWash", "basin": "BasinSlosh",
                 "dam-dry": "DryDamBreak", "dam-wet": "WetBedBore", "fill": "ReservoirFill",
                 "drain": "ReservoirDrawdown", "tide": "TidalReversal", "tilt": "DrivenTank"}
TOPDOWN_BASES = {"boat-wake": "BoatWake", "island-current": "IslandCurrent",
                 "stream-confluence": "StreamConfluence", "forked-channel": "ForkedChannel",
                 "drain-vortex": "DrainVortex", "rain-pond": "RainPond"}
SPELL_BASES = {"upwell-geyser": "UpwellGeyser", "charged-surge": "ChargedSurge",
               "moving-ward-jet": "MovingWardJet", "shield-intercept": "ShieldIntercept",
               "recast-relay": "RecastRelay", "radial-break": "RadialBreak"}
TOPDOWN_RANGES = {"drive": ("_drive", 0, 1.4), "wave-speed": ("_waveSpeed", .8, 2),
                  "drag": ("_drag", .1, 1.8)}
RANGES = {"depth": ("_poolDepth", .25, .78), "bank": ("_bankRise", 0, 1.55),
          "drive": ("_drive", 0, 1.4), "period": ("_period", 2.2, 6)}

PARCEL_BASES = {"ripple": "PlanarRipples", "mass": "FallingMass", "jet": "FreeJet", "impact": "RockImpact",
                "opposed": "OpposedJets", "waterfall": "Waterfall", "vortex": "Vortex", "needle": "WaterNeedle2D",
                "curtain": "WaterCurtain2D", "falls": "ContinuousWaterfall", "rain": "FreeJet", "drip": "FreeJet",
                "fountain": "WaterNeedle2D"}
PARCEL_RANGES = {"gravity": ("_gravity", 0, 18), "strength": ("_strength", .05, 2),
                 "feed": ("_feedDuration", .18, 10), "duration": ("_duration", .4, 16),
                 "span": ("_dropSpan", 0, 5), "count": ("_dropCount", 1, 160),
                 "rain-exposure": ("_rainExposure", 0, .05)}
DROP_DEFAULTS = {
    "rain": {"_feed": 8, "_origin": "{x: -0.21, y: 2.25, z: 0}", "_velocity": "{x: 0.63, y: -1.25, z: 0}",
             "_dropSpan": 4.2, "_dropCount": 70, "_feedDuration": 2.35, "_duration": 3.6},
    "drip": {"_feed": 8, "_origin": "{x: -0.58, y: 1.67, z: 0}", "_velocity": "{x: 0.09, y: -0.08, z: 0}",
             "_dropSpan": 0.035, "_dropCount": 4, "_feedDuration": 2.15, "_duration": 3.6},
    "fountain": {"_gravity": 8, "_responses": 5, "_playOnEnable": 1, "_origin": "{x: -0.94, y: 0.09, z: 0}", "_velocity": "{x: 1.32, y: 5.9, z: 0}",
                 "_feedDuration": 1.55, "_duration": 3.6}
}


def channel_ranges(profile):
    if profile in ("dam-dry", "dam-wet"):
        return RANGES | {"bank": ("_bankRise", 0, .85)}
    return RANGES


def parcel_ranges(profile):
    ranges = dict(PARCEL_RANGES)
    if profile not in ("rain", "drip"):
        del ranges["count"], ranges["span"]
    if profile != "rain":
        del ranges["rain-exposure"]
    return ranges


def in_assets(project, relative):
    path = Path(relative)
    if path.is_absolute() or not path.parts or path.parts[0] != "Assets" or ".." in path.parts:
        raise ValueError("Use a project-relative Assets path.")
    target = project / path
    if target.resolve() != target:
        raise ValueError(f"Choose an Assets path without filesystem links: {target}")
    return target


def asset_files(base, name, profile, settings, *, original_surface=False):
    if profile in CHANNEL_BASES:
        return surface_files(base, name, CHANNEL_BASES[profile], settings, "Channels", "ChannelWater2D", channel_ranges(profile))
    elif profile in TOPDOWN_BASES:
        return surface_files(base, name, TOPDOWN_BASES[profile], settings, "TopDown", "TopDownWater2D", TOPDOWN_RANGES)
    elif profile in PARCEL_BASES or profile in SPELL_BASES:
        return parcel_files(base, name, profile, settings, original_surface=original_surface)
    else:
        raise ValueError(f"Unknown water profile: {profile}")


def compose(project, template, destination, name, profile, settings):
    prefabs, status = compose_bundle(project, template, destination, name, [("", profile, settings)])
    return prefabs[0], status


def compose_bundle(project, template, destination, name, actors, *, original_surface=False):
    """Stage every actor and publish the complete named recipe with one directory rename."""
    project = Path(project).resolve(strict=True)
    version = project / "ProjectSettings/ProjectVersion.txt"
    if not version.is_file() or not (project / "Assets").is_dir():
        raise ValueError("A Unity receiving project is required.")
    if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]{0,59}", name):
        raise ValueError("Use an asset identifier of up to 60 ASCII letters, digits and underscores.")
    base = in_assets(project, template)
    output = in_assets(project, destination) / name
    if output.resolve() != output or base == output or base in output.parents or output in base.parents:
        raise ValueError("Keep the new variant in its own game-owned folder outside the template.")
    files = {}
    prefabs = []
    for suffix, profile, settings in actors:
        if not re.fullmatch(r"[A-Za-z0-9_]*", suffix):
            raise ValueError("Use identifier characters for the prefab role suffix.")
        actor = name + suffix
        prefab = output / (actor + ".prefab")
        if prefab in prefabs:
            raise ValueError(f"Duplicate actor name: {actor}")
        prefabs.append(prefab)
        files.update(asset_files(base, actor, profile, settings, original_surface=original_surface))
    if not prefabs:
        raise ValueError("The water template requires at least one actor.")
    owned_guids = {re.search(rb"(?m)^guid: ([0-9a-f]+)$", data)[1]
                   for filename, data in files.items() if filename.endswith(".meta")}
    with project_lock(version):
        if output.exists():
            if output.is_dir() and all((output / file).is_file() and (output / file).read_bytes() == data
                                       for file, data in files.items()):
                return prefabs, "already composed"
            raise ValueError(f"Existing artist changes or a different recipe were preserved at {output}.")
        for root in (project / "Assets", project / "Packages"):
            for path in root.rglob("*.meta"):
                if path.is_file() and meta_guid(path) in owned_guids:
                    raise ValueError(f"The same variant GUID already exists at {path}. Use a new name.")
        output.parent.mkdir(parents=True, exist_ok=True)
        cache = project / "Cache"
        cache.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="temp-channel-", dir=cache) as temporary:
            staged = Path(temporary) / name
            staged.mkdir()
            for file, data in files.items():
                (staged / file).write_bytes(data)
            os.rename(staged, output)
    return prefabs, "composed"


def surface_files(base, name, donor, settings, directory, parent, ranges):
    source = Path(directory) / (donor + ".prefab")
    parent_asset = Path(directory) / (parent + ".prefab")
    for relative in (source, parent_asset, Path(str(parent_asset) + ".meta")):
        if not (base / relative).is_file() or digest(base / relative) != digest(TEMPLATE / relative):
            raise ValueError(f"Install this release of the water template before composing: {relative}")
    definition = (base / source).read_text(encoding="utf-8").replace("value: " + donor, "value: " + name)
    for key, value in settings.items():
        if key not in ranges:
            raise ValueError(f"{key} is not an input for {donor}.")
        field, low, high = ranges[key]
        if not math.isfinite(value) or not low <= value <= high:
            raise ValueError(f"{key} must be between {low} and {high}.")
        definition, changed = re.subn(r"(propertyPath: " + field + r"\n      value: )[^\n]+",
                                      lambda match: match[1] + format(value, ".7g"), definition)
        if changed != 1:
            raise ValueError(f"The source variant has no unique setting for {field}.")
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "qin-unity-skills/author-channel/" + name).hex
    meta = (TEMPLATE / (str(source) + ".meta")).read_text(encoding="utf-8")
    files = {name + ".prefab": definition.encode(), name + ".prefab.meta":
             re.sub(r"(?m)^guid: [0-9a-f]+$", "guid: " + guid, meta).encode()}
    return files

def parcel_files(base, name, profile, settings, *, original_surface=False):
    if profile in SPELL_BASES:
        donor = SPELL_BASES[profile]
        assets = Path("Spells") / donor
        preset, prefab = assets / (donor + ".asset"), assets / (donor + ".prefab")
    else:
        donor = PARCEL_BASES[profile]
        preset = Path("Presets") / (donor + ".asset")
        prefab = Path("Variants") / (donor + ".prefab")
    required = [preset, prefab, Path(str(preset) + ".meta"), Path("WaterFluidMixer.prefab.meta")]
    if original_surface:
        required.extend("OriginalFluidSurface." + ext for ext in
                        ("mat", "mat.meta", "shadergraph", "shadergraph.meta", "hlsl", "hlsl.meta"))
    if profile == "rain" and settings.get("rain-exposure", 0) > 0:
        required.extend(("WaterFluidMixer.prefab", "FluidMix.compute", "FluidMixController.cs",
                         "FluidMixSimulation.cs", "SOFluidMixRecipe.cs", "FluidSurface.hlsl", "WaterStudyOptics.hlsl",
                         "SideRainRenderer.cs", "SideRain.mat", "SideRain.mat.meta",
                         "SideRain.shader", "SideRain.shader.meta"))
    for path in required:
        if not (base / path).is_file() or digest(base / path) != digest(TEMPLATE / path):
            raise ValueError(f"Install this release of the fluid template before composing: {path}")
    ranges = parcel_ranges(profile)
    new_fields = dict(DROP_DEFAULTS.get(profile, {}))
    for key, value in settings.items():
        if key not in ranges:
            raise ValueError(f"{key} is not an input for {profile}.")
        field, low, high = ranges[key]
        if not math.isfinite(value) or not low <= value <= high or (key == "count" and value != int(value)):
            raise ValueError(f"Invalid {key}. Expected a value from {low} through {high}.")
        new_fields[field] = int(value) if key == "count" else format(value, ".7g")
    recipe = (base / preset).read_text(encoding="utf-8").replace("m_Name: " + donor, "m_Name: " + name)
    for key, value in new_fields.items():
        pattern = r"(?m)^  " + key + r": [^\n]*$"
        if re.search(pattern, recipe):
            recipe = re.sub(pattern, lambda _: "  " + key + ": " + str(value), recipe)
        else:
            recipe += "  " + key + ": " + str(value) + "\n"
    feed = float(re.search(r"(?m)^  _feedDuration: (.+)$", recipe)[1])
    duration = float(re.search(r"(?m)^  _duration: (.+)$", recipe)[1])
    if feed > duration:
        raise ValueError("Feed duration must fit inside the cast duration.")
    if re.search(r"(?m)^  _feed: 6$", recipe):
        hold = re.search(r"(?m)^  _curtainHold: (.+)$", recipe)
        minimum = max(.34, float(hold[1]) if hold else .5) + 1.4
        if duration < minimum - 1e-6:
            raise ValueError(f"The curtain duration must allow its hold and runoff ({minimum:g} s).")
    recipe_guid = uuid.uuid5(uuid.NAMESPACE_URL, "qin-unity-skills/author-water-recipe/" + name).hex
    prefab_guid = uuid.uuid5(uuid.NAMESPACE_URL, "qin-unity-skills/author-channel/" + name).hex
    definition = (base / prefab).read_text(encoding="utf-8").replace("value: " + donor, "value: " + name)
    if original_surface:
        root_guid = meta_guid(base / "WaterFluidMixer.prefab.meta").decode()
        material_guid = meta_guid(base / "OriginalFluidSurface.mat.meta").decode()
        overrides = "".join(
            f"    - target: {{fileID: {renderer}, guid: {root_guid}, type: 3}}\n"
            "      propertyPath: m_Materials.Array.data[0]\n"
            "      value: \n"
            f"      objectReference: {{fileID: 2100000, guid: {material_guid}, type: 2}}\n"
            for renderer in (110203, 110303))
        definition = definition.replace("    m_RemovedComponents:", overrides + "    m_RemovedComponents:", 1)
    donor_guid = meta_guid(TEMPLATE / (str(preset) + ".meta")).decode()
    files = {name + ".asset": recipe.encode(), name + ".prefab": definition.replace(donor_guid, recipe_guid).encode()}
    for path, guid in ((preset, recipe_guid), (prefab, prefab_guid)):
        meta = (TEMPLATE / (str(path) + ".meta")).read_text(encoding="utf-8")
        files[name + path.suffix + ".meta"] = re.sub(r"(?m)^guid: [0-9a-f]+$", "guid: " + guid, meta).encode()
    return files


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--template", default=DEFAULT)
    parser.add_argument("--destination", default="Assets/GameWater")
    parser.add_argument("--name", required=True)
    parser.add_argument("--profile", choices=(*CHANNEL_BASES, *PARCEL_BASES, *SPELL_BASES, *TOPDOWN_BASES), required=True)
    options = RANGES | TOPDOWN_RANGES | PARCEL_RANGES
    for key, (_, low, high) in options.items():
        parser.add_argument("--" + key, dest=key, type=float, help=f"Override the profile value ({low} to {high}).")
    args = parser.parse_args()
    overrides = {key: getattr(args, key) for key in options if getattr(args, key) is not None}
    try:
        path, result = compose(args.project, args.template, args.destination, args.name, args.profile, overrides)
    except (ValueError, OSError) as error:
        parser.exit(1, str(error) + "\n")
    print(f"{result}: {path}")


if __name__ == "__main__":
    main()
