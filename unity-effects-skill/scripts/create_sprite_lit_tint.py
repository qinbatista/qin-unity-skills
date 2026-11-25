"""Author an editable HDR tint graph from the installed URP 17.6 Sprite Lit template."""

import argparse
import json
import os
import stat
import uuid
from copy import deepcopy
from pathlib import Path


TEMPLATE = Path("Editor/ShaderGraph/GraphTemplates/2D/0_2D Sprite Lit.shadergraph")
GRAPH_NAMESPACE = uuid.UUID("0b63db09-4d76-5dd9-adfb-27cc94ab01fd")
SG = "UnityEditor.ShaderGraph."


def object_id(name):
    return uuid.uuid5(GRAPH_NAMESPACE, name).hex


def reference(obj):
    return {"m_Id": obj["m_ObjectId"]}


def one(items, description):
    matches = list(items)
    if len(matches) != 1:
        raise ValueError(f"Expected one {description} in the Sprite Lit template, found {len(matches)}")
    return matches[0]


def read_template(package):
    manifest = json.loads((package / "package.json").read_text(encoding="utf-8-sig"))
    version = manifest.get("version", "")
    supported = version.split(".")[:2] == ["17", "6"]
    if manifest.get("name") != "com.unity.render-pipelines.universal" or not supported:
        raise ValueError(f"Expected Universal RP 17.6.x, found {manifest.get('name')} {version}")

    # Shader Graph stores a stream of separate JSON objects. Slots have their own identities.
    source = (package / TEMPLATE).read_text(encoding="utf-8-sig").strip()
    objects = {}
    decoder = json.JSONDecoder()
    while source:
        obj, end = decoder.raw_decode(source)
        key = obj["m_ObjectId"]
        if key in objects:
            raise ValueError(f"Duplicate Shader Graph object ID {key}")
        objects[key] = obj
        source = source[end:].lstrip()
    return objects


def connect(source, output, destination, input_port):
    return {
        "m_OutputSlot": {"m_Node": reference(source), "m_SlotId": output},
        "m_InputSlot": {"m_Node": reference(destination), "m_SlotId": input_port},
    }


def build_graph(package):
    objects = read_template(package)
    graph = one((o for o in objects.values() if o["m_Type"] == SG + "GraphData"), "GraphData")
    category = objects[one(graph["m_CategoryData"], "Blackboard category")["m_Id"]]
    target = objects[one(graph["m_ActiveTargets"], "render target")["m_Id"]]
    sprite_lit = objects[target["m_ActiveSubTarget"]["m_Id"]]
    if sprite_lit["m_Type"].rsplit(".", 1)[-1] != "UniversalSpriteLitSubTarget":
        raise ValueError("The installed template does not target URP Sprite Lit")

    nodes = [objects[ref["m_Id"]] for ref in graph["m_Nodes"]]
    properties = [objects[ref["m_Id"]] for ref in graph["m_Properties"]]
    main = one((prop for prop in properties if prop.get("m_OverrideReferenceName") == "_MainTex"),
               "_MainTex property")
    texture = one((n for n in nodes if n.get("m_Property") == reference(main)), "_MainTex node")
    sample_ref = one((edge["m_InputSlot"]["m_Node"] for edge in graph["m_Edges"]
                      if edge["m_OutputSlot"]["m_Node"] == reference(texture)), "sprite texture sample")
    sample = objects[sample_ref["m_Id"]]
    if sample["m_Type"] != SG + "SampleTexture2DNode":
        raise ValueError("Expected _MainTex to feed a Sample Texture 2D node")
    multiply = one((n for n in nodes if n["m_Type"] == SG + "MultiplyNode"), "Multiply node")
    split = one((n for n in nodes if n["m_Type"] == SG + "SplitNode"), "Split node")
    blocks = [n for n in nodes if n["m_Type"] == SG + "BlockNode"]
    outputs = {block["m_Name"]: block for block in blocks}

    # Keep the native master stack, UV0 sprite sample and arithmetic port schemas.
    # New identities are recipe-local and reproducible. Unity owns each asset's .meta.
    tint = {
        "m_SGVersion": 3, "m_Type": SG + "Internal.ColorShaderProperty",
        "m_ObjectId": object_id("Tint property"), "m_Name": "Tint",
        "m_DefaultRefNameVersion": 1, "m_RefNameGeneratedByDisplayName": "Tint",
        "m_DefaultReferenceName": "_Tint", "m_OverrideReferenceName": "_Tint",
        "m_ColorMode": 1, "m_GeneratePropertyBlock": True,
        "m_Value": {channel: 1.0 for channel in "rgba"},
        "m_Guid": {"m_GuidSerialized": "db3b65b8-fe68-4a14-b9f2-0f5027dca303"},
    }
    tint_node = deepcopy(texture)
    tint_node.update(m_ObjectId=object_id("Tint node"), m_Property=reference(tint))
    rgba = one((objects[slot["m_Id"]] for slot in sample["m_Slots"]
                if objects[slot["m_Id"]]["m_Id"] == 0), "sample RGBA output")
    tint_port = deepcopy(rgba)
    tint_port.update(m_ObjectId=object_id("Tint RGBA"), m_DisplayName="Tint",
                     m_ShaderOutputName="Out", m_StageCapability=3)
    tint_node["m_Slots"] = [reference(tint_port)]
    objects[tint_port["m_ObjectId"]] = tint_port

    group = {
        "m_SGVersion": 0, "m_Type": SG + "GroupData", "m_ObjectId": object_id("Tint group"),
        "m_Title": "Tint the sprite", "m_Position": {"x": -1070, "y": -230},
    }
    note = {
        "m_SGVersion": 0, "m_Type": SG + "StickyNoteData", "m_ObjectId": object_id("Coverage note"),
        "m_Title": "Keep the painted silhouette",
        "m_Content": "Multiply RGBA once. Alpha follows the artwork and tint opacity. "
                     "The Sprite Lit target applies renderer tint and 2D light after this stage.",
        "m_TextSize": 0, "m_Theme": 0, "m_Group": reference(group),
        "m_Position": {"serializedVersion": "2", "x": -1040, "y": -170, "width": 395, "height": 155},
    }
    layout = [(texture, -1040, 76), (sample, -790, 50), (tint_node, -790, 510),
              (multiply, -500, 65), (split, -230, 285)]
    for node, x, y in layout:
        node["m_Group"] = reference(group)
        node["m_DrawState"]["m_Position"].update(x=x, y=y)
    live_nodes = [n for n, _, _ in layout] + blocks
    target["m_DisableTint"] = False
    category["m_ChildObjectList"] = [reference(main), reference(tint)]
    graph.update(
        m_Path="Qin/Sprites", m_Keywords=[], m_Dropdowns=[],
        m_Nodes=[reference(n) for n in live_nodes], m_Properties=category["m_ChildObjectList"],
        m_GroupDatas=[reference(group)], m_StickyNoteDatas=[reference(note)],
        m_Edges=[
            connect(texture, 0, sample, 1), connect(sample, 0, multiply, 0),
            connect(tint_node, 0, multiply, 1), connect(multiply, 2, outputs["SurfaceDescription.BaseColor"], 0),
            connect(multiply, 2, split, 0), connect(split, 4, outputs["SurfaceDescription.Alpha"], 0),
        ],
    )
    output = [graph, target, sprite_lit, category, main, tint, group, note]
    for node in live_nodes:
        output.append(node)
        output.extend(objects[slot["m_Id"]] for slot in node["m_Slots"])
    return ("\n\n".join(json.dumps(obj, indent=4, ensure_ascii=False) for obj in output) + "\n").encode("utf-8")


def write_graph(destination, content):
    destination = destination.resolve()
    destination.parent.mkdir(parents=True, exist_ok=True)
    try:
        mode = stat.S_IMODE(destination.stat().st_mode)
    except FileNotFoundError:
        mode = None

    # Unity ignores hidden files. Keep the write on the destination volume so the
    # final rename is atomic, including when two producers run simultaneously.
    pending = destination.with_name(f".{destination.name}.{uuid.uuid4().hex}.tmp")
    stream = pending.open("xb")
    try:
        with stream:
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
        if mode is not None:
            pending.chmod(mode)
        os.replace(pending, destination)
    finally:
        if pending.exists():
            # A failed replacement may leave a read-only staging file on Windows.
            pending.chmod(stat.S_IRUSR | stat.S_IWUSR)
            pending.unlink()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--urp-package", type=Path, required=True, help="Installed Universal RP package directory")
    parser.add_argument("--output", type=Path, required=True, help="Destination .shadergraph asset")
    args = parser.parse_args()
    if args.output.suffix != ".shadergraph":
        parser.error("--output must name a .shadergraph asset")
    if args.output.resolve() == (args.urp_package / TEMPLATE).resolve():
        parser.error("Choose an output outside the source template")
    try:
        content = build_graph(args.urp_package)
        write_graph(args.output, content)
    except (OSError, ValueError, KeyError) as error:
        parser.exit(1, f"Could not author the Sprite Lit Tint graph: {error}\n")


if __name__ == "__main__":
    main()
