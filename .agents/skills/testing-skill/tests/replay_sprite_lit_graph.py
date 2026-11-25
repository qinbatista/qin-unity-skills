"""Independently author, import and render the Sprite Lit Tint graph in three blank Unity receivers."""

import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import uuid
from xml.etree import ElementTree

TESTS = Path(__file__).resolve().parent
ROOT = TESTS.parents[3]
SKILL = ROOT / "unity-effects-skill"
sys.path.insert(0, str(ROOT / "unity-performance-skill/scripts"))
from replay_cases import make_workspace, native_temp
from hidden_process import hidden_process_options
from sprite_lit_tint_contract import inspect_graph

EDITOR = "6000.6.7f1"
PACKAGES = {
    "com.unity.render-pipelines.universal": "17.6.0",
    "com.unity.shadergraph": "17.6.0",
    "com.unity.test-framework": "1.8.0",
    "com.unity.modules.imageconversion": "1.0.0",
}
CASE = "SpriteLitTintCases.LightHdrTintAndSpriteCoverage"
STATES = ["dim", "lit", "hdrTint", "rendererHalfAlpha", "materialAlpha", "coloredSprite", "rendererRgb",
          "backgroundOnly", "transparentOnColor"]
GRAPH = "SpriteLitTint.shadergraph"
BASELINE_REVISION = "8cd932a0391f37f59cf3e037c7baad6256dbef83"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def unity(args, lane, env, label, action, options):
    project = lane / "receiver"
    log = lane / (label + ".log")
    timeout = 2100
    editor = ["--editor-path", str(args.editor)] if args.editor else ["--editor-version", EDITOR]
    command = [sys.executable, "-B", str(args.unity_policy), action, str(project), *editor, "--no-accelerator", "--timeout", str(timeout), *options]
    with (lane / (label + "-cli.log")).open("w", encoding="utf-8") as output:
        result = subprocess.run(command, cwd=project, env=env, stdin=subprocess.DEVNULL,
                                stdout=output, stderr=subprocess.STDOUT, timeout=timeout + 60, **hidden_process_options())
    if result.returncode:
        raise RuntimeError(f"{label} exited with {result.returncode} in {lane}; inspect {log}")


def capture(args, lane, env, label):
    receipt, xml = lane / (label + ".json"), lane / (label + ".xml")
    unity(args, lane, dict(env, QIN_SPRITE_GRAPH_CAPTURE=str(receipt)), label, "test", [
        "--mode", "EditMode", "--filter", CASE, "--output", str(xml),
        "--", "-logFile", str(lane / (label + ".log")), "-job-worker-count", "2", "-diag-debug-shader-compiler"])
    leaves = list(ElementTree.parse(xml).iter("test-case"))
    if [(t.get("fullname"), t.get("result")) for t in leaves] != [(CASE, "Passed")]:
        raise RuntimeError(f"Missing passing NUnit case in {xml}")
    value = json.loads(receipt.read_text(encoding="utf-8"))
    if value["unity"] != EDITOR or value["urp"] != PACKAGES["com.unity.render-pipelines.universal"]:
        raise RuntimeError(f"Editor or URP identity mismatch in {receipt}")
    frames = value["frames"]
    if value["shaderGraph"] != PACKAGES["com.unity.shadergraph"]:
        raise RuntimeError(f"Shader Graph package version mismatch in {receipt}")
    if [f["state"] for f in frames] != STATES or len({f["sha256"] for f in frames}) != len(STATES):
        raise RuntimeError(f"Expected distinct full-frame hashes in {receipt}")
    for frame in frames:
        raw = (lane / (label + "-frames") / (frame["state"] + ".rgba32f")).read_bytes()
        if len(raw) != 4 * 4 * value["width"] * value["height"] or digest(raw) != frame["sha256"]:
            raise ValueError(f"Full frame does not match its receipt in {lane}: {frame['state']}")
    return value


def create_receiver(args, lane):
    run_id = str(uuid.uuid4())
    project = lane / "receiver"
    assets = project / "Assets"
    assets.mkdir(parents=True)
    assert not any(assets.iterdir()) and not (project / "Library").exists()
    graph = assets / GRAPH
    generator = SKILL / "scripts/create_sprite_lit_tint.py"
    subprocess.run([sys.executable, "-B", str(generator), "--urp-package", str(args.urp_package),
                    "--output", str(graph)], check=True, cwd=project, **hidden_process_options())
    if set(assets.iterdir()) != {graph}:
        raise RuntimeError("Generator must leave receiver-owned metadata to Unity.")
    layout = inspect_graph(graph)
    generated = digest(graph.read_bytes())
    scripts = assets / "Editor"
    scripts.mkdir()
    shutil.copyfile(TESTS / "Editor/SpriteLitTintCases.cs", scripts / "SpriteLitTintCases.cs")
    write_json(scripts / "Tint.Tests.asmdef", {
        "name": "Tint.Tests", "includePlatforms": ["Editor"], "optionalUnityReferences": ["TestAssemblies"],
        "references": ["Unity.RenderPipelines.Core.Runtime", "Unity.RenderPipelines.Universal.Runtime",
                       "Unity.RenderPipelines.Universal.2D.Runtime"],
    })
    (project / "Packages").mkdir()
    write_json(project / "Packages/manifest.json", {"dependencies": PACKAGES})
    (project / "ProjectSettings").mkdir()
    (project / "ProjectSettings/ProjectVersion.txt").write_text(f"m_EditorVersion: {EDITOR}\n", encoding="utf-8")
    # Select linear rendering before first import; Unity populates unspecified project defaults.
    (project / "ProjectSettings/ProjectSettings.asset").write_text(
        "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!129 &1\n"
        "PlayerSettings:\n  m_ObjectHideFlags: 0\n  serializedVersion: 30\n  m_ActiveColorSpace: 1\n",
        encoding="utf-8")
    (project / ".sprite-graph-receiver").write_text(run_id, encoding="utf-8")
    tmp = str(native_temp(lane))
    env = dict(os.environ, TEMP=tmp, TMP=tmp, TMPDIR=tmp, UPM_CACHE_ROOT=str(lane / "upm"),
               DOTNET_PROCESSOR_COUNT="2")
    if sys.platform == "darwin":
        profile = lane / "profile"
        (profile / "Library/Preferences").mkdir(parents=True)
        (profile / "Library/Caches").mkdir()
        env["CFFIXED_USER_HOME"] = str(profile)
    result = capture(args, lane, env, "candidate")
    meta = graph.with_suffix(graph.suffix + ".meta")
    guid = re.search(r"(?m)^guid: ([a-f0-9]{32})$", meta.read_text(encoding="utf-8"))
    if not guid or digest(graph.read_bytes()) != generated:
        raise ValueError("Check the imported graph and its Unity-assigned GUID.")
    return {"run": run_id, "graph_sha256": generated, "unity_guid": guid[1], "layout": layout,
            "native": result}, env


def verify_lane(args, lane, original):
    result, env = create_receiver(args, lane)
    reference = None
    if original is not None:
        reference = {"commit": BASELINE_REVISION, "graph_sha256": digest(original)}
        if reference["graph_sha256"] != result["graph_sha256"]:
            graph = lane / "receiver/Assets" / GRAPH
            meta = graph.with_suffix(graph.suffix + ".meta")
            saved, identity = graph.read_bytes(), meta.read_bytes()
            try:
                graph.write_bytes(original)
                reference["native"] = capture(args, lane, env, "reference")
                if reference["native"] != result["native"] or identity != meta.read_bytes():
                    raise RuntimeError("The published graph's pixels or the receiving material identity changed.")
            finally:
                graph.write_bytes(saved)
        else:
            reference["comparison"] = "Identical source bytes"
    # A finished receiver has no subsequent import or render consumer.
    for name in ("receiver", "upm", "profile", "tmp"):
        child = lane / name
        if child.is_dir():
            shutil.rmtree(child)
    return result, reference


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", type=Path, required=True, help="New child of a registered project Cache/temp-* run")
    parser.add_argument("--urp-package", type=Path, required=True, help="Installed URP 17.6.0 package directory")
    parser.add_argument("--workers", type=int, choices=(1, 2), default=1, help="Maximum simultaneous cold imports")
    parser.add_argument("--editor", type=Path, help="Explicit Unity Editor path; otherwise resolve the pinned version via Unity CLI")
    parser.add_argument("--unity-policy", type=Path, default=ROOT / "unity-code-skill/scripts/unity_cli_daily.py")
    args = parser.parse_args()
    args.urp_package = args.urp_package.resolve(strict=True)
    urp_manifest = json.loads((args.urp_package / "package.json").read_text(encoding="utf-8"))
    if (urp_manifest["name"], urp_manifest["version"]) != ("com.unity.render-pipelines.universal", PACKAGES["com.unity.render-pipelines.universal"]):
        raise ValueError(f"Expected the installed URP {PACKAGES['com.unity.render-pipelines.universal']} package.")
    if args.editor:
        args.editor = args.editor.resolve(strict=True)
    args.unity_policy = args.unity_policy.resolve(strict=True)
    try:
        original = subprocess.check_output(
            ["git", "show", BASELINE_REVISION + ":unity-effects-skill/assets/" + GRAPH],
            stderr=subprocess.PIPE, cwd=ROOT, **hidden_process_options())
    except subprocess.CalledProcessError as error:
        raise ValueError(f"Fetch history containing {BASELINE_REVISION} before running the native comparison.") from error
    template = args.urp_package / "Editor/ShaderGraph/GraphTemplates/2D/0_2D Sprite Lit.shadergraph"
    sources = {"source_template_sha256": template, "generator_sha256": SKILL / "scripts/create_sprite_lit_tint.py",
               "fixture_sha256": TESTS / "Editor/SpriteLitTintCases.cs", "validator_sha256": TESTS / "sprite_lit_tint_contract.py",
               "checked_in_graph_sha256": SKILL / "assets" / GRAPH, "runner_sha256": Path(__file__)}
    inputs = {key: digest(path.read_bytes()) for key, path in sources.items()}
    work = make_workspace(args.work.resolve())
    lanes = [work / f"run-{n}" for n in range(1, 4)]
    with ThreadPoolExecutor(max_workers=args.workers) as pool:
        results = list(pool.map(lambda lane: verify_lane(args, lane, original if lane == lanes[0] else None), lanes))
    reference = results[0][1]
    runs = [run for run, _ in results]
    if len({r["unity_guid"] for r in runs}) != 3 or len({r["graph_sha256"] for r in runs}) != 1:
        raise RuntimeError("Independent graph bytes or receiver metadata identities disagree.")
    if digest((SKILL / "assets" / GRAPH).read_bytes()) != runs[0]["graph_sha256"]:
        raise RuntimeError("Checked-in graph differs from the generated bytes.")
    if any(r["native"] != runs[0]["native"] or r["layout"] != runs[0]["layout"] for r in runs[1:]):
        raise RuntimeError("Independent full-frame readbacks or graph layouts differ.")
    if inputs != {key: digest(path.read_bytes()) for key, path in sources.items()}:
        raise RuntimeError("A producer, template, graph, validator or verification fixture changed during the replay.")
    common = dict(runs[0]["native"])
    frames = common.pop("frames")
    result = {
        "shader": GRAPH, **inputs,
        "metadata_policy": "The generator writes only Shader Graph bytes. Unity allocates each receiver's GUID.",
        "frame_hash": "SHA-256 of every linear RGBAFloat pixel in native byte order, rows starting at the bottom",
        "bytes_per_frame": common["width"] * common["height"] * 16,
        "native": common, "layout": runs[0]["layout"],
        "metrics": {f["state"]: {k: v for k, v in f.items() if k not in ("state", "sha256")} for f in frames},
        "runs": [{"run": r["run"], "graph_sha256": r["graph_sha256"], "unity_guid": r["unity_guid"],
                  "full_frame_sha256": {f["state"]: f["sha256"] for f in r["native"]["frames"]}} for r in runs],
        "reference": {k: v for k, v in reference.items() if k != "native"},
    }
    if "native" in reference:
        result["reference"]["full_frame_sha256"] = {f["state"]: f["sha256"] for f in reference["native"]["frames"]}
    write_json(work / "receipt.json", result)
    print(f"Three cold receipts and the published-graph comparison passed: {work / 'receipt.json'}")


if __name__ == "__main__":
    main()
