"""Build an offline case selector and side-by-side frames from native replay evidence."""
from html import escape
import json
from pathlib import Path
import re

from PIL import Image, ImageChops, ImageOps, ImageStat

from fluid_replay_checks import read_state

GROUPS = {"channel-matrix": "Banks and reservoirs", "topdown": "Overhead currents",
          "spells": "Interactive casts", "limits": "Parameter limits"}
CONTROLS = {"off": "Drive disconnected", "control": "Changed physical input", "gravity-on": "Gravity restored",
            "no-waves": "Surface transfer disabled", "miss": "Target missed", "still": "Driver disconnected",
            "early-stop": "Valve closed earlier"}


CASE_CONTROLS = {
    "DryDamBreak": "Gate held shut", "WetBedBore": "Gate held shut",
    "ReservoirFill": "Supply closed", "ReservoirDrawdown": "Drain closed",
    "TidalReversal": "Tidal driver off", "DrivenTank": "Acceleration disconnected",
    "UpwellGeyser": "Collision moved out of reach", "MovingWardJet": "Collision moved out of reach",
    "ShieldIntercept": "Shield collision displaced", "ChargedSurge": "Reduced cast charge",
    "RecastRelay": "Second cast omitted", "RadialBreak": "Reduced initial energy",
    "PlanarRipples": "Surface impulse strength set to zero", "FallingMass": "Gravity disabled",
    "FreeJet": "Surface transfer disabled", "RockImpact": "Solid obstacle disabled",
    "OpposedJets": "Pressure disabled", "Waterfall": "Gravity disabled", "Vortex": "Swirl disabled",
}


def compare_wave_fields(folder, destination):
    samples = sorted((folder / "no-waves").glob("state-*.bin"))
    best = None
    for off in samples:
        on = folder / "normal" / off.name
        if not on.is_file():
            continue
        normal = read_state(on)[1]
        control = read_state(off)[1]
        score = sum(abs(a - b) for a, b in zip(normal, control))
        if best is None or score > best[0]:
            best = score, int(off.stem.split("-")[-1]), normal, control
    if best is None or best[0] == 0:
        return None
    _, tick, normal, control = best
    limit = max(max(map(abs, normal)), max(map(abs, control)))
    panels = Image.new("RGB", (1024, 256))
    for i, field in enumerate((normal, control)):
        values = bytes(round(127.5 * (1 + max(-1., min(1., value / limit)))) for value in field)
        height = Image.frombytes("L", (128, 64), values).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
        mapped = ImageOps.colorize(height, "#237dbe", "#ffc77b", "#182f39", midpoint=128)
        panels.paste(mapped.resize((512, 256), Image.Resampling.NEAREST), (512 * i, 0))
    panels.save(destination, quality=96, subsampling=0)
    return CONTROLS["no-waves"], tick, limit


def compare(folder, destination):
    normal = folder / ("frames" if (folder / "frames").is_dir() else "normal")
    for control, label in CONTROLS.items():
        files = sorted((folder / control).glob("[0-9][0-9][0-9][0-9].png"))
        if not files and control == "still" and (folder / "still.png").is_file():
            files = [folder / "still.png"]
        best = None
        for other in files:
            tick = 60 if other.name == "still.png" else int(other.stem)
            original = normal / f"{tick:04d}.png"
            if not original.is_file():
                continue
            with Image.open(original) as a, Image.open(other) as b:
                a.thumbnail((128, 72))
                b.thumbnail((128, 72))
                difference = ImageStat.Stat(ImageChops.difference(a.convert("RGB"), b.convert("RGB"))).mean
                score = sum(difference)
            if best is None or score > best[0]:
                best = (score, tick, original, other)
        if best and best[0] > 0:
            _, tick, first, second = best
            with Image.open(first) as a, Image.open(second) as b:
                a = a.convert("RGB")
                b = b.convert("RGB")
                pair = Image.new("RGB", (a.width + b.width, max(a.height, b.height)))
                pair.paste(a, (0, 0))
                pair.paste(b, (a.width, 0))
                pair.save(destination, quality=92)
            return CASE_CONTROLS.get(folder.name, label), tick, None
    return compare_wave_fields(folder, destination)


def title(case):
    return re.sub(r"(?<=[a-z])(?=[A-Z])", " ", case)


def write_gallery(work, report):
    from water_templates import describe

    recipes = work / "Recipes"
    recipes.mkdir(exist_ok=True)
    groups = {case: GROUPS[group] for group, cases in report.get("families", {}).items() for case in cases}
    output = work / "Comparisons"
    output.mkdir(exist_ok=True)
    rows = []
    for case, path in report["subjectVideos"].items():
        if not (work / path).is_file():
            raise FileNotFoundError(work / path)
        label = title(case)
        group = groups.get(case, "Reference recipes")
        folder = work / "A/captures" / case
        normal = folder / ("frames" if (folder / "frames").is_dir() else "normal")
        frames = sorted(normal.glob("[0-9][0-9][0-9][0-9].png"))
        poster = output / (case + "-poster.jpg")
        with Image.open(frames[len(frames) // 3]) as shot:
            shot.convert("RGB").save(poster, quality=90)
        result = compare(folder, output / (case + ".jpg"))
        edition = report.get("edition", "refined") if case == "WideCloudburst" else "original"
        recipe_path = recipes / (case + ".json")
        recipe_path.write_text(json.dumps(describe(case, edition), indent=2) + "\n", encoding="utf-8")
        row = dict(name=case, label=label, group=group, video=path, poster="Comparisons/" + poster.name,
                   recipe=recipe_path.relative_to(work).as_posix())
        if result:
            row.update(pair="Comparisons/" + case + ".jpg", control=result[0], tick=result[1], fieldRange=result[2])
        rows.append(row)
    groups = list(dict.fromkeys([*GROUPS.values(), "Reference recipes"]))
    options = []
    for group in groups:
        items = [f'<option value="{i}">{escape(row["label"])}</option>' for i, row in enumerate(rows)
                 if row["group"] == group]
        if items:
            options.append(f'<optgroup label="{escape(group)}">' + "".join(items) + "</optgroup>")
    selected = next((i for i, row in enumerate(rows) if row["name"] == "DryDamBreak"), 0)
    html = TEMPLATE.replace("%%OPTIONS%%", "".join(options)).replace("%%DATA%%", json.dumps(rows))
    html = html.replace("%%COUNT%%", str(len(rows))).replace("%%DEFAULT%%", str(selected))
    target = work / "Review.html"
    target.write_text(html, encoding="utf-8")
    return target


TEMPLATE = """<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>2D water replay</title><style>
*{box-sizing:border-box}body{margin:0;background:#09151c;color:#e9f3f5;font:16px/1.5 system-ui,sans-serif}
main{max-width:1024px;margin:auto;padding:22px clamp(12px,3vw,38px) 48px}header,nav{display:flex;align-items:center;gap:12px;flex-wrap:wrap}
header{justify-content:space-between;border-bottom:1px solid #44606c;padding-bottom:10px;margin-bottom:16px}
h1{font-size:clamp(22px,4vw,32px);margin:0}h2{font-size:18px;margin:18px 0 6px}p{margin:5px 0 12px;color:#afc5cf}
select,button{font:inherit;color:inherit;background:#18313c;border:1px solid #54737f;border-radius:5px;padding:7px 11px;min-height:42px}
select{max-width:100%}button{cursor:pointer}video,img{display:block;width:100%;height:auto;background:#061017}
video{margin-top:12px;aspect-ratio:16/9;object-fit:contain}figcaption{display:flex;justify-content:space-between;gap:8px;color:#c2d7df}
figure{margin:0}a{color:#83dbeb}button:focus-visible,select:focus-visible,a:focus-visible{outline:3px solid #e9ba50}
@media(max-width:530px){main{padding-top:12px}nav select{flex:1;min-width:0}nav{gap:6px}nav label{width:100%}figcaption{font-size:14px}}
</style><main><header><h1>2D water replay</h1><span>%%COUNT%% scenes · 30 fps</span></header>
<nav aria-label="Select a replay"><label for="scene">Scene</label><select id="scene">%%OPTIONS%%</select>
<button id="prev" aria-label="Previous scene">←</button><button id="next" aria-label="Next scene">→</button></nav>
<video id="film" controls playsinline loop preload="metadata"></video><p><a id="file">Open full-size film</a> · <a id="recipe">View template and scene inputs</a></p>
<section id="comparison"><h2 id="moment"></h2><p id="scale" hidden></p><figure><img id="pair" alt="Native camera frames at the same simulation tick"><figcaption><span>Normal input</span><span id="condition"></span></figcaption></figure></section>
</main><script>const cases=%%DATA%%;const sel=document.getElementById('scene'),film=document.getElementById('film');
const ordered=Array.from(sel.options).map(o=>Number(o.value));
function show(i){const c=cases[i];sel.value=i;film.poster=c.poster;film.src=c.video;document.getElementById('file').href=c.video;document.getElementById('recipe').href=c.recipe;
const section=document.getElementById('comparison');section.hidden=!c.pair;
if(c.pair){document.getElementById('pair').src=c.pair;document.getElementById('moment').textContent=(c.fieldRange?'GPU surface height · ':'Same-camera comparison · ')+(c.tick/30).toFixed(2)+' s';
const scale=document.getElementById('scale');scale.hidden=!c.fieldRange;if(c.fieldRange){scale.textContent='Identical colour scale in both fields, ±'+c.fieldRange.toPrecision(3)+' m. Blue indicates a trough and gold a crest.';}
document.getElementById('pair').alt=c.fieldRange?'Matched horizontal GPU water-height grids':'Native camera frames at the same simulation tick';
document.getElementById('condition').textContent=c.control;}history.replaceState(null,'','#'+c.name);}
function step(n){show(ordered[(ordered.indexOf(+sel.value)+n+ordered.length)%ordered.length]);}
sel.onchange=()=>show(+sel.value);document.getElementById('prev').onclick=()=>step(-1);document.getElementById('next').onclick=()=>step(1);
const requested=cases.findIndex(c=>'#'+c.name===location.hash);show(requested>=0?requested:%%DEFAULT%%);</script></html>"""
