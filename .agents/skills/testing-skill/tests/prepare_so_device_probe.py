"""Prepare an external, non-shipping iOS probe from an installed Unity Skill pack."""

import argparse
import hashlib
import json
import shutil
from pathlib import Path


PROJECT = Path(__file__).resolve().parents[4]


def prepare(run, pack, settings):
    run = run.resolve(strict=True)
    pack = pack.resolve(strict=True)
    if run.parent != PROJECT / "Cache" or not run.name.startswith("temp-"):
        raise ValueError("Use a fresh allocated task run in the project Cache.")
    if not settings.resolve(strict=True).is_relative_to(run):
        raise ValueError("Keep private service settings inside this task run.")
    config = json.loads(settings.read_text(encoding="utf-8"))
    if set(config) != {"projectId", "environment", "profile"} or not all(config.values()):
        raise ValueError("Supply projectId, environment and a disposable authentication profile.")
    receiver = run / "device-probe"
    receiver.mkdir()
    runtime = receiver / "Assets/script"
    editor = receiver / "Assets/Editor"
    resources = receiver / "Assets/Resources"
    for directory in [runtime, editor, resources, receiver / "Packages", receiver / "ProjectSettings"]:
        directory.mkdir(parents=True, exist_ok=True)
    template = pack / "unity-code-skill/assets/script/SODataController"
    shutil.copytree(template, runtime / "SODataController")
    source_hashes = {}
    for source in template.rglob("*"):
        if source.is_file():
            relative = source.relative_to(template)
            target = runtime / "SODataController" / relative
            if target.read_bytes() != source.read_bytes():
                raise ValueError(f"Template copy differs: {relative}")
            source_hashes[relative.as_posix()] = hashlib.sha256(source.read_bytes()).hexdigest()
    fixture = Path(__file__).with_name("fixtures") / "scriptableobjects"
    for name in ["SODeviceValue.cs", "SODeviceProbe.cs"]:
        shutil.copy2(fixture / name, runtime / name)
    shutil.copy2(Path(__file__).with_name("Editor") / "SODeviceProbeBuilder.cs", editor)
    shutil.copy2(settings, resources / "SODeviceSettings.json")
    assembly = {"name": "SODevice.Runtime", "references": ["Unity.Newtonsoft.Json", "Unity.Services.CloudSave", "Unity.Services.Authentication", "Unity.Services.Core", "Unity.Services.Core.Environments", "Unity.Netcode.Runtime", "Unity.Networking.Transport", "Unity.Collections"], "versionDefines": [{"name": "com.unity.services.cloudsave", "expression": "3.4.2", "define": "SO_DATA_UNITY_CLOUD_SAVE"}, {"name": "com.unity.netcode.gameobjects", "expression": "2.14.0", "define": "SO_DATA_UNITY_NETCODE"}]}
    (runtime / "SODevice.Runtime.asmdef").write_text(json.dumps(assembly, indent=2), encoding="utf-8")
    (editor / "SODevice.Editor.asmdef").write_text(json.dumps({"name": "SODevice.Editor", "references": ["SODevice.Runtime", "Unity.Netcode.Runtime"], "includePlatforms": ["Editor"]}, indent=2), encoding="utf-8")
    packages = {"dependencies": {"com.unity.nuget.newtonsoft-json": "3.2.2", "com.unity.services.cloudsave": "3.4.2", "com.unity.netcode.gameobjects": "2.14.0", "com.unity.modules.physics": "1.0.0", "com.unity.modules.jsonserialize": "1.0.0", "com.unity.modules.unitywebrequest": "1.0.0", "com.unity.modules.unitywebrequestassetbundle": "1.0.0"}}
    (receiver / "Packages/manifest.json").write_text(json.dumps(packages, indent=2), encoding="utf-8")
    shutil.copy2(PROJECT / "examples/job-state-batches/ProjectSettings/ProjectVersion.txt", receiver / "ProjectSettings")
    # This setting is private execution input, not part of the published Skill or fixture source.
    (receiver / "ProjectSettings/ProjectSettings.asset").write_text("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!129 &1\nPlayerSettings:\n  m_ObjectHideFlags: 0\n  serializedVersion: 28\n  companyName: QinSkillTests\n  productName: SO Storage Probe\n  cloudProjectId: " + config["projectId"] + "\n", encoding="utf-8")
    print(json.dumps({"receiver": str(receiver), "template_files": len(source_hashes), "template_sha256": hashlib.sha256(json.dumps(source_hashes, sort_keys=True).encode()).hexdigest()}))
    return receiver


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run", type=Path)
    parser.add_argument("--pack", type=Path, required=True, help="Installed pack directory containing unity-code-skill")
    parser.add_argument("--settings", type=Path, required=True, help="Private task-owned JSON service configuration")
    arguments = parser.parse_args()
    prepare(arguments.run, arguments.pack, arguments.settings)
