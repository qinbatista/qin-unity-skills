"""List, inspect and author the named cases from the approved 2D fluid recordings."""
import argparse
import json
from pathlib import Path
import re

from channel_matrix import PROFILES as MATRIX
from channel_replay_checks import CHANNELS, COVE
from compose_water import (CHANNEL_BASES, PARCEL_BASES, SPELL_BASES, TOPDOWN_BASES,
                           asset_files, compose_bundle)
from fluid_application_checks import APPLICATIONS
from fluid_mixer import DEFAULT, TEMPLATE
from fluid_replay_checks import FORMS
from inlet_replay_checks import INLETS
from spell_matrix import PROFILES as SPELLS
from topdown_matrix import CASES as TOPDOWN_CASES, PROFILES as TOPDOWN_PROFILES
from water_limits import BRIEFS, brief

VERSION = 1
DEFAULT_EDITION = "original"
FAMILIES = {
    "gallery": ("QinFluidReplay", "Replay", 84),
    "curtain": ("QinFluidApplicationReplay", APPLICATIONS["curtain"][0], 92),
    "waterfall": ("QinFluidApplicationReplay", APPLICATIONS["waterfall"][0], 292),
    "channels": ("QinChannelReplay", "FlowChannel", 256),
    "inlets": ("QinWaterInletReplay", "CastWaterVariant", 114),
    "channel-matrix": ("QinChannelMatrix", "ChannelMatrix", 166),
    "topdown": ("QinTopdownMatrix", "TopdownMatrix", 180),
    "spells": ("QinSpellMatrix", "SpellMatrix", 96),
    "limits": ("QinWaterInletReplay", "ParameterLimits", 114),
}


def catalog():
    """Reference the same briefs, donor names and overrides used by native replay."""
    cases = {}

    def add(case, suite, profile, settings=None):
        cases[case] = (suite, [("", profile, settings or {})])

    for profile, donor in PARCEL_BASES.items():
        if donor in FORMS and profile not in ("rain", "drip"):
            add(donor, "gallery", profile)
    cases[APPLICATIONS["curtain"][1]] = ("curtain", [
        ("Gather", "vortex", {}), ("Needle", "needle", {}), ("Curtain", "curtain", {})])
    add(APPLICATIONS["waterfall"][1], "waterfall", "falls")
    for profile, donor in CHANNEL_BASES.items():
        if donor in CHANNELS:
            add(donor, "channels", profile)
    add("CoveWash", "channels", "shore", COVE)
    for case, profile in INLETS.items():
        add(case, "inlets", profile)
    for case, profile in MATRIX.items():
        add(case, "channel-matrix", profile)
    for case, profile in zip(TOPDOWN_CASES, TOPDOWN_PROFILES):
        add(case, "topdown", profile)
    for case, profile in SPELLS.items():
        add(case, "spells", profile)
    for case, (profile, settings) in BRIEFS.items():
        add(case, "limits", profile, settings)
    return cases


CASES = catalog()


def recipe(case, edition):
    suite, actors = CASES[case]
    if edition not in ("original", "refined") or (edition == "refined" and case != "WideCloudburst"):
        raise ValueError("The refined edition exists only for WideCloudburst.")
    if suite == "limits":
        profile, settings = brief(case, edition)
        actors = [("", profile, settings)]
    return suite, actors


# The camera setup and scene events are external to the serialized water prefab.
# The packaged fixture identified for each case executes this exact reference presentation.
def stage(case, suite):
    if suite == "gallery":
        return "QinFluidReplay.Open / MakeFluidBackdrop; place water at (0,-1.88,0), camera (0,-0.7,-10), orthographic size 4. Draw the configured solid obstacle when enabled."
    if suite in ("curtain", "waterfall"):
        return "SetApplicationCamera / DrawApplicationStage; floor -1.88, camera (0,floor+1.24,-10), orthographic size 2.16. Use the courtyard for Curtain and the grotto for Waterfall2D."
    if suite == "topdown":
        return "XY overhead field at the origin; camera (0,0,-10), orthographic size 2.25. Use the top-down solver, mesh and Shader Graph from the prefab."
    if suite in ("inlets", "limits", "spells"):
        view = "Open-river inlet at origin; QinChannelReplay.SetChannelStage(OpenRiver, null, canonical case ID); camera (0,1.10,-10), orthographic size 1.64."
        if case == "UpwellGeyser":
            view += " SpellMatrix scales scenery and orthographic size by 1.30 and recentres camera Y on the scaled scenery."
        return view
    closed = case == "BasinSlosh" if suite == "channels" else case != "TidalReversal"
    size = 1.76 if closed else 1.64
    return f"QinChannelReplay.SetChannelStage with the initial solver field; camera (0,0.13,-10), orthographic size {size}. Place the channel at the origin."


def driving(case, suite):
    if suite == "gallery":
        return ["SetRecipe, set the water sorting layer, BeginCast(seed), SetCastTime(frame / 30f). Use the saved recipe and shared surface material."]
    if suite == "curtain":
        return [
            "Place Gather at (-2.69,floor+0.948,0), uniform scale 0.245, Z rotation 22 degrees. Place Needle and Curtain at (0,floor,0).",
            "Place a CircleCollider2D of radius 0.32 at (0.92,floor+1.02,0), sync 2D transforms, and SetObstacleAnchor on Needle and Curtain.",
            "Frame 1: Gather.BeginCast(seed ^ 41u). At frame 10 end Gather and begin Needle at seed. Advance each cast from its own launch frame.",
            "Sweep right with Physics2D.CircleCast of radius 0.055 at the needle outlet height, from the previous front to origin.x + 8.45 * max(0, needleAge - 0.087).",
            "On the first target hit, end Needle, position Curtain at (target.bounds.center.x,floor,0), and begin it with seed ^ 63u. Drive Curtain time from the hit through hold and runoff. A miss skips Curtain.",
        ]
    if suite == "waterfall":
        return ["The continuous recipe runs on enable. For the recording reset the recipe, BeginCast(seed), SetCastTime(frame / 30f); EndCast on frame 222, then continue the same clock through runoff. The early-stop control closes on frame 72."]
    if suite in ("inlets", "limits"):
        return ["SetRecipe, BeginCast(seed), then SetCastTime(frame / 30f) before and after the rendered player-frame boundary. Allow the saved emission, reception and retirement schedule to finish."]
    if suite in ("channels", "channel-matrix"):
        stop = 241 if suite == "channels" else 151
        steps = [f"BeginFlow(seed), SetTime(frame / 30.0). EndFlow on frame {stop} after SetTime; advance the same clock through retirement. The matched stationary control uses SetDriver(0)."]
        if case == "DrivenTank":
            steps.append("Immediately after SetTime at frames 60, 87 and 114 call SetTankAcceleration with +1.55, -1.55 and 0 respectively.")
        if suite == "channel-matrix":
            steps.append("DressChannelFixture supplies the reference gauge and hardware. For the two dam cases, place gate X at -0.609375 and Y at bed + 0.58 + 1.20 * GateAperture while running.")
        return steps
    if suite == "topdown":
        return ["Set Automatic=false and InputEnabled=true, ResetWater, capture tick zero, then Advance(1f/30f) once per tick 1 through 179. The solver uses its deterministic profile. For the coast sample disable input and advance 12 ticks."]
    steps = ["Place the outlet at ground + recipe.Origin. Place the target at ground + recipe.ObstacleCenter with the saved radius and show it when recipe.SolidObstacle is enabled. SetObstacleAnchor(target), then BeginChargedCast(seed, 1f). Drive SetCastTime from the start of this cast."]
    if case in ("RadialBreak", "ChargedSurge"):
        steps.append("The weaker reference take uses normalized charge 0.20; compare with 1.0 at matched times.")
    if case == "MovingWardJet":
        steps.append("Before each sample move the target by (0.49*sin(4.15*t),0.38*sin(5.1*t+0.18),0) from its initial world anchor.")
    if case in ("UpwellGeyser", "MovingWardJet", "ShieldIntercept"):
        steps.append("The reference control binds a transform at local Y=9 to remove contact from the stream; the normal target uses the saved radius.")
    if case == "RecastRelay":
        steps.append("Frame 14: EndCast. At frame 30 verify Recycled, mirror the whole emitter with localScale=(-1,1,1), move the outlet to TransformPoint(recipe.Origin), BeginCast(seed), and restart local time at zero. The control omits this second launch.")
    return steps


def describe(case, edition=DEFAULT_EDITION):
    suite, actors = recipe(case, edition)
    fixture, method, frames = FAMILIES[suite]
    definitions = []
    for role, profile, settings in actors:
        files = asset_files(TEMPLATE, "Preview", profile, settings, original_surface=edition == "original")
        if "Preview.asset" in files:
            fields = dict(re.findall(r"^  (_\w+): (.+)$", files["Preview.asset"].decode(), re.MULTILINE))
        else:
            fields = dict(re.findall(r"propertyPath: (_\w+)\n      value: ([^\n]+)", files["Preview.prefab"].decode()))
        donor = (SPELL_BASES | PARCEL_BASES | CHANNEL_BASES | TOPDOWN_BASES)[profile]
        definitions.append({"suffix": role, "profile": profile, "donor": donor,
                            "overrides": settings, "serializedParameters": fields})
    details = {"catalogVersion": VERSION, "case": case, "edition": edition, "actors": definitions,
               "camera": stage(case, suite), "clock": {"fps": 30, "frames": frames, "resolution": [960, 540]},
               "seed": None if suite == "topdown" else 29411, "sceneEvents": driving(case, suite),
               "fixture": f"scripts/fluid-replay/{fixture}.cs", "method": method,
               "referenceReplay": {"runner": "scripts/replay_fluids.py", "suite": suite, "edition": edition,
                                   "capture": f"A/captures/{case}/" + ("frames" if suite == "gallery" else "normal")}}
    if suite in ("curtain", "waterfall", "spells", "inlets", "limits"):
        details["presentation"] = ("QinFluidApplicationReplay.OpenVariant: set the water sorting layer, use per-instance "
                                   "Shader Graph material values _ReflectionTint=(0.65,0.94,0.98,1), "
                                   "_DepthTint=(0.030,0.226,0.356,1), _WaterOptics=(0.74,0.83,0,0). "
                                   "Keep the opt-in Side Rain material on its own shader.")
    if case == "WideCloudburst":
        mode = "legacy" if edition == "original" else "normal"
        details["referenceReplay"]["edition"] = edition
        details["contactProbe"] = {"runner": "scripts/replay_cloudburst.py",
                                      "capture": f"captures/WideCloudburst/{mode}"}
    return details


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("list", help="List all recorded case identifiers and native replay families.")
    inspect = commands.add_parser("show", help="Print the donor, actual serialized fields, stage and event sequence.")
    author = commands.add_parser("author", help="Create a named, editable prefab or actor bundle in a Unity project.")
    for command in (inspect, author):
        command.add_argument("case", choices=CASES)
        command.add_argument("--edition", choices=("original", "refined"), default=DEFAULT_EDITION)
    author.add_argument("--project", type=Path, required=True)
    author.add_argument("--name", help="Game-owned asset name. Defaults to the case identifier.")
    author.add_argument("--template", default=DEFAULT, help="Project-relative path to the installed fluid solver.")
    author.add_argument("--destination", default="Assets/GameWater")
    args = parser.parse_args()
    if args.command == "list":
        for case, (suite, _) in CASES.items():
            print(f"{case}\t{suite}")
        return
    try:
        result = describe(args.case, args.edition)
        if args.command == "author":
            _, actors = recipe(args.case, args.edition)
            prefabs, status = compose_bundle(args.project, args.template, args.destination, args.name or args.case, actors,
                                             original_surface=args.edition == "original")
            result.update(status=status, prefabs=[str(path) for path in prefabs])
        print(json.dumps(result, indent=2))
    except (ValueError, OSError) as error:
        parser.exit(1, str(error) + "\n")


if __name__ == "__main__":
    main()
