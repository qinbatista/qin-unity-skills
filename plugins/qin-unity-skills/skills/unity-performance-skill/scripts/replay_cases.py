"""Replay the Unity Performance casebook from a source checkout or installed pack."""

import argparse
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import xml.etree.ElementTree as ET

SKILL = Path(__file__).resolve().parents[1]
CODE_SCRIPTS = SKILL.parent / "unity-code-skill/scripts"
sys.path.insert(0, str(CODE_SCRIPTS))
from hidden_process import hidden_process_options

EDITOR = "6000.6.7f1"
NAMESPACE = "Qin.UnityPerformance.Cases"
PACKAGES = {
    "com.unity.addressables": "2.11.2",
    "com.unity.burst": "2.0.0",
    "com.unity.entities": "6.6.0",
    "com.unity.physics": "6.6.0",
    "com.unity.render-pipelines.universal": "17.6.0",
    "com.unity.visualeffectgraph": "17.6.0",
    "com.unity.test-framework": "1.8.0",
    **{"com.unity.modules." + module: "1.0.0" for module in (
        "animation", "imageconversion", "particlesystem", "physics2d", "physicscore2d"
    )},
}


def catalog():
    groups = json.loads((SKILL / "assets/casebook.json").read_text(encoding="utf-8"))
    return {f"{group}.{method}": description
            for group, methods in groups.items() for method, description in methods.items()}


def select_cases(cases, requested):
    if not requested:
        return list(cases)
    selected = []
    for token in requested:
        matches = [name for name in cases if token in (name, name.split(".")[0])]
        if not matches:
            raise ValueError(f"Unknown case or class {token!r}. Use --list for the available names.")
        selected.extend(matches)
    return list(dict.fromkeys(selected))


def make_workspace(path):
    # The caller owns the allocated Cache run. Each invocation gets a fresh child.
    parent = path.parent.resolve(strict=True)
    if parent.parent.name != "Cache" or not parent.name.startswith("temp-"):
        raise ValueError("--work must be a new directory immediately inside a task-owned Cache/temp-* run.")
    work = parent / path.name
    if work.exists() or work.is_symlink() or work.name in (".", ".."):
        raise ValueError(f"Choose a new --work directory. Path already in use: {work}")
    work.mkdir()
    return work


def native_temp(work):
    directory = work / "tmp"
    directory.mkdir()
    if sys.platform != "darwin":
        return directory
    if len(os.fsencode(directory)) > 62:
        stat = directory.stat()
        short = Path("/.vol") / str(stat.st_dev) / str(stat.st_ino)
        if not short.is_dir() or not short.samefile(directory):
            raise ValueError("Unity IPC requires a shorter task Cache path on this volume.")
    else:
        short = directory
    probe = short / "socket-probe"
    with socket.socket(socket.AF_UNIX) as connection:
        connection.bind(str(probe))
        if not (directory / probe.name).is_socket():
            raise OSError("The native IPC directory must resolve inside the task run.")
    probe.unlink()
    return short


def verify_results(xml_path, expected):
    root = ET.parse(xml_path).getroot()
    leaves = list(root.iter("test-case"))
    names = Counter(leaf.get("fullname", "") for leaf in leaves)
    target = Counter(f"{NAMESPACE}.{name}" for name in expected)
    if names != target:
        raise ValueError(f"Unity case inventory mismatch. Missing {list((target-names).elements())}. "
                         f"Unexpected {list((names-target).elements())}. See {xml_path}")
    failures = [(leaf.get("fullname"), leaf.get("result"), leaf.findtext("failure/message"))
                for leaf in leaves if leaf.get("result") != "Passed"]
    if root.get("result") != "Passed" or failures:
        raise ValueError(f"Unity reported {root.get('result')}: {failures}. See {xml_path}")
    return {leaf.get("fullname").removeprefix(NAMESPACE + "."): {
                "result": leaf.get("result"),
                "observations": [line.removeprefix("QIN_CASE ") for line in
                                 (leaf.findtext("output") or "").splitlines() if line.startswith("QIN_CASE ")]
            } for leaf in leaves}


def source_identity():
    inputs = sorted(path for path in (SKILL / "assets").rglob("*") if path.is_file())
    inputs.append(Path(__file__).resolve())
    digest = hashlib.sha256()
    for path in inputs:
        digest.update(path.relative_to(SKILL).as_posix().encode() + b"\0")
        digest.update(hashlib.sha256(path.read_bytes()).digest())
    return digest.hexdigest()



def run_unity(action, arguments, project, environment, work, editor):
    command = [sys.executable, "-B", str(CODE_SCRIPTS / "unity_cli_daily.py"), action, str(project),
               "--non-interactive", "--no-pager", "--timeout", "900"]
    if editor:
        command.extend(["--editor-path", str(editor.resolve(strict=True))])
    command.extend(arguments)
    log = work / f"{action}-cli.log"
    with log.open("w", encoding="utf-8") as output:
        result = subprocess.run(command, cwd=project, env=environment, stdin=subprocess.DEVNULL,
                                stdout=output, stderr=subprocess.STDOUT, timeout=960, **hidden_process_options())
    if result.returncode:
        raise RuntimeError(f"Unity {action} exited with {result.returncode}. See {log} and {work / (action + '.log')}")


def prepare_fixture(project, selected):
    families = {name.split(".", 1)[0] for name in selected}
    fixtures = SKILL / "assets/cases"
    destination = project / "Assets/PerformanceCases"
    scripts = destination / "Editor"
    scripts.mkdir(parents=True)
    files = {"ReplaySetup.cs", *(family + ".cs" for family in families)}
    dependencies = {"com.unity.test-framework": PACKAGES["com.unity.test-framework"]}
    references = set()

    def include(packages, assemblies):
        dependencies.update((name, PACKAGES[name]) for name in packages)
        references.update(assemblies)

    if families & {"AddressableCases", "BuiltContentCases"}:
        files.discard("BuiltContentCases.cs")  # Both fixtures share the asset preparation source.
        files.add("AddressableCases.cs")
        include(["com.unity.addressables"], ["Unity.Addressables", "Unity.Addressables.Editor", "Unity.ResourceManager"])
    if "SimulationCases" in families:
        include(["com.unity.entities", "com.unity.burst", "com.unity.physics", "com.unity.modules.physics2d",
                 "com.unity.modules.physicscore2d", "com.unity.modules.animation"],
                ["Unity.Entities", "Unity.Burst", "Unity.Collections", "Unity.Mathematics", "Unity.Physics"])
    if "RenderingCases" in families:
        include(["com.unity.render-pipelines.universal", "com.unity.visualeffectgraph",
                 "com.unity.modules.imageconversion", "com.unity.modules.particlesystem"],
                ["Unity.RenderPipelines.Core.Runtime", "Unity.RenderPipelines.Universal.Runtime", "Unity.VisualEffectGraph.Runtime"])
        files.add("ImpactGraph.cs")
        shutil.copytree(fixtures / "Rendering", destination / "Rendering")
    for filename in sorted(files):
        shutil.copy2(fixtures / "Editor" / filename, scripts)
    assembly_file = "Qin.UnityPerformance.Cases.asmdef"
    assembly = json.loads((fixtures / "Editor" / assembly_file).read_text(encoding="utf-8"))
    assembly["references"] = sorted(references)
    assembly["versionDefines"] = [item for item in assembly["versionDefines"] if item["name"] in dependencies]
    (scripts / assembly_file).write_text(json.dumps(assembly, indent=2), encoding="utf-8")
    return dependencies


def replay(work, selected, editor, whole_suite):
    project = work / "receiver"
    for folder in ("Packages", "ProjectSettings"):
        (project / folder).mkdir(parents=True)
    dependencies = prepare_fixture(project, selected)
    (project / ".qin-performance-replay").touch()
    (project / "ProjectSettings/ProjectVersion.txt").write_text(f"m_EditorVersion: {EDITOR}\n", encoding="utf-8")
    (project / "Packages/manifest.json").write_text(json.dumps({"dependencies": dependencies}, indent=2), encoding="utf-8")
    output = work / "evidence"
    output.mkdir()
    temporary = str(native_temp(work))
    graph = "RenderingCases.SharedImpactKeepsConcurrentPayloadsAtTheirHitPositions" in selected
    graphics = graph or "RenderingCases.CombinedShaderPreservesOverlappingCuesWithFewerSubmittedDraws" in selected
    loading = any(name.startswith("AddressableCases.") for name in selected)
    environment = dict(os.environ, TMPDIR=temporary, TMP=temporary, TEMP=temporary,
                       UPM_CACHE_ROOT=str(work / "upm"), PYTHONDONTWRITEBYTECODE="1",
                       QIN_PERFORMANCE_OUTPUT=str(output), QIN_PERFORMANCE_GRAPHICS=str(int(graphics)),
                       QIN_PERFORMANCE_LOADING=str(int(loading)), QIN_PERFORMANCE_GRAPH=str(int(graph)))
    native = ["-job-worker-count", "4"]
    if not graphics:
        native.append("-nographics")
    run_unity("run", ["--log-file", str(work / "run.log"), "--no-tail", "--", *native,
                      "-executeMethod", f"{NAMESPACE}.ReplaySetup.Configure"], project, environment, work, editor)
    if f"QIN_PERFORMANCE_REPLAY_READY {EDITOR}" not in (work / "run.log").read_text(errors="replace"):
        raise RuntimeError(f"Unity setup or version verification failed. See {work / 'run.log'}")
    locked = json.loads((project / "Packages/packages-lock.json").read_text(encoding="utf-8"))["dependencies"]
    for package, version in dependencies.items():
        if locked.get(package, {}).get("version") != version:
            raise ValueError(f"Receiver did not resolve {package} {version}.")
    pattern = NAMESPACE + "." if whole_suite else ";".join(f"{NAMESPACE}.{name}" for name in selected)
    xml_path = work / "results.xml"
    run_unity("test", ["--mode", "EditMode", "--filter", pattern, "--output", str(xml_path), "--", *native,
                       "-perfTestResults", str(output / "performance.json"), "-logFile", str(work / "test.log")],
              project, environment, work, editor)
    observations = verify_results(xml_path, selected)
    report = {"editor": EDITOR, "scope": "Unity Editor native simulation", "packageVersions": dependencies,
              "fixtureSha256": source_identity(), "cases": observations}
    (work / "case-results.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"{len(selected)} native cases passed in Unity {EDITOR}. Results: {xml_path}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--list", action="store_true", help="Show every case and what it exercises.")
    parser.add_argument("--case", action="append", help="Select Class.Method or a whole class. Repeat to combine.")
    parser.add_argument("--work", type=Path, help="New child of an allocated project Cache/temp-* run.")
    parser.add_argument("--editor", type=Path, help=f"Explicit path to the Unity {EDITOR} Editor.")
    args = parser.parse_args()
    try:
        cases = catalog()
        selected = select_cases(cases, args.case)
        if args.list:
            for name in selected:
                print(f"{name}  {cases[name]}")
            return
        if args.work is None:
            parser.error("Supply --work to run Unity, or --list to inspect the casebook.")
        replay(make_workspace(args.work), selected, args.editor, not args.case)
    except (OSError, ValueError, RuntimeError, ET.ParseError, subprocess.TimeoutExpired) as error:
        parser.exit(1, f"{error}\n")


if __name__ == "__main__":
    main()
