"""Check the saved editable structure of the five-node Sprite Lit Tint example."""

import json
from collections import Counter
from itertools import combinations
from pathlib import Path


def require(condition, message):
    if not condition:
        raise ValueError(message)


def inspect_graph(path):
    source = Path(path).read_text(encoding="utf-8-sig").lstrip()
    decoder = json.JSONDecoder()
    objects = {}
    while source:
        obj, end = decoder.raw_decode(source)
        key = obj["m_ObjectId"]
        require(key not in objects, f"Duplicate graph object {key}")
        objects[key] = obj
        source = source[end:].lstrip()

    def resolve(ref):
        key = ref["m_Id"]
        require(key in objects, f"Missing graph object {key}")
        return objects[key]

    def kind(obj):
        return obj["m_Type"].rsplit(".", 1)[-1]

    graphs = [obj for obj in objects.values() if kind(obj) == "GraphData"]
    require(len(graphs) == 1, "Expected one graph root")
    graph = graphs[0]
    targets = [resolve(ref) for ref in graph["m_ActiveTargets"]]
    require(len(targets) == 1 and kind(resolve(targets[0]["m_ActiveSubTarget"])) == "UniversalSpriteLitSubTarget",
            "Expected the active URP Sprite Lit target")
    require(not targets[0]["m_DisableTint"], "The pass must apply renderer tint")
    nodes = [resolve(ref) for ref in graph["m_Nodes"]]
    operations = [node for node in nodes if kind(node) != "BlockNode"]
    expected = {"PropertyNode": 2, "SampleTexture2DNode": 1, "MultiplyNode": 1, "SplitNode": 1}
    require(Counter(map(kind, operations)) == expected, "Expected the five editable stock nodes")

    groups = [resolve(ref) for ref in graph["m_GroupDatas"]]
    notes = [resolve(ref) for ref in graph["m_StickyNoteDatas"]]
    require(len(groups) == 1 and groups[0]["m_Title"].strip(), "Missing the named tint frame")
    require(len(notes) == 1 and notes[0]["m_Content"].strip(), "Missing the coverage note")
    require(all(resolve(item["m_Group"]) is groups[0] for item in [*operations, *notes]),
            "Every working node and its note must belong to the tint frame")
    rectangles = [node["m_DrawState"]["m_Position"] for node in operations] + [notes[0]["m_Position"]]
    for a, b in combinations(rectangles, 2):
        overlaps = all(max(a[pos], b[pos]) < min(a[pos] + a[size], b[pos] + b[size])
                       for pos, size in [("x", "width"), ("y", "height")])
        require(not overlaps, "The saved node or note rectangles overlap")

    names = {}
    for node in nodes:
        name = kind(node)
        if name == "PropertyNode":
            prop = resolve(node["m_Property"])
            name = prop.get("m_OverrideReferenceName") or prop["m_DefaultReferenceName"]
            if name == "_Tint":
                require(prop["m_ColorMode"] == 1 and all(prop["m_Value"][c] == 1 for c in "rgba"),
                        "Expected a white-default HDR tint")
        elif name == "BlockNode":
            name = node["m_Name"]
        elif name == "SampleTexture2DNode":
            require(node["m_TextureType"] == 0, "Color samples must use Default mode")
        names[node["m_ObjectId"]] = name

    def endpoint(slot):
        node = resolve(slot["m_Node"])
        ports = {resolve(ref)["m_Id"] for ref in node["m_Slots"]}
        require(slot["m_SlotId"] in ports, "Edge references a missing port")
        return names[node["m_ObjectId"]], slot["m_SlotId"]

    edges = [(endpoint(edge["m_OutputSlot"]), endpoint(edge["m_InputSlot"])) for edge in graph["m_Edges"]]
    wiring = [
        (("_MainTex", 0), ("SampleTexture2DNode", 1)),
        (("SampleTexture2DNode", 0), ("MultiplyNode", 0)),
        (("_Tint", 0), ("MultiplyNode", 1)),
        (("MultiplyNode", 2), ("SurfaceDescription.BaseColor", 0)),
        (("MultiplyNode", 2), ("SplitNode", 0)),
        (("SplitNode", 4), ("SurfaceDescription.Alpha", 0)),
    ]
    require(Counter(edges) == Counter(wiring), "Incorrect sprite RGB or coverage wiring")
    fragment = {resolve(ref)["m_Name"] for ref in graph["m_FragmentContext"]["m_Blocks"]}
    require({"SurfaceDescription.BaseColor", "SurfaceDescription.Alpha"} <= fragment, "Missing live fragment outputs")
    return {"target": "URP Sprite Lit", "workingNodes": len(operations), "textureSamples": 1,
            "edges": len(edges), "group": groups[0]["m_Title"], "note": notes[0]["m_Title"]}
