"""Assemble the published C# foundations, documented consumers and previous writer's fixtures."""

import argparse
import json
import re
import shutil
import sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[4]
PACK = PROJECT / "plugins/qin-unity-skills/skills/unity-code-skill"
sys.path.insert(0, str(PACK / "scripts"))

from read_topic import read_topic


def prepare(run, cloud=False, netcode=False):
    run = run.resolve(strict=True)
    if run.parent != PROJECT / "Cache" or not run.name.startswith("temp-"):
        raise ValueError("Use an allocated task directory under the project Cache.")
    fixture = run / "foundations"
    fixture.mkdir()
    runtime = fixture / "Assets/Foundations"
    editor = fixture / "Assets/Editor"
    runtime.mkdir(parents=True)
    editor.mkdir()
    sample = PROJECT / "examples/job-state-batches"
    shutil.copytree(sample / "Packages", fixture / "Packages")
    settings = fixture / "ProjectSettings"
    settings.mkdir()
    shutil.copy2(sample / "ProjectSettings/ProjectVersion.txt", settings)

    for source in (PACK / "assets").glob("*.cs"):
        shutil.copy2(source, runtime)
    for directory in ("WriteHooks", "CodeGen", "Compatibility"):
        shutil.copytree(PACK / "assets" / directory, runtime / directory)
    shutil.copy2(PACK / "assets/Editor/SOStorageModeDrawer.cs", editor)
    (runtime / "TestVisibility.cs").write_text(
        '[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Foundations.EditorTests")]\n', encoding="utf-8"
    )
    old_formats = Path(__file__).with_name("fixtures") / "scriptableobjects"
    shutil.copy2(old_formats / "SOSpellCatalog.cs.meta", runtime)
    plain = fixture / "Assets/Game/PlainDomain"
    plain.mkdir(parents=True)
    shutil.copy2(old_formats / "PlainMoney.cs", plain)
    (plain / "PlainDomain.asmdef").write_text('{"name": "PlainDomain"}', encoding="utf-8")
    caller = fixture / "Assets/Game/PlainCaller"
    caller.mkdir()
    shutil.copy2(old_formats / "SODataNoEngineCaller.cs", caller)
    (caller / "PlainCaller.asmdef").write_text(
        '{"name": "PlainCaller", "references": ["PlainDomain"], "noEngineReferences": true}', encoding="utf-8"
    )
    fixtures = fixture / "Assets/Fixtures"
    fixtures.mkdir()
    for name in ("SpellCatalogV1.asset", "WalletV1.json", "AudioPreferencesLegacy.json"):
        shutil.copy2(old_formats / name, fixtures)
    examples = {
        "AudioPreferencesManager": "references/unity-game-code-structure-design.md#singleton-managers",
        "SOAudioPreferences": "references/unity-lifecycle-and-serialization.md#scriptableobject-local-files",
    }
    for name, section in examples.items():
        code, = re.findall(r"^```csharp\n(.*?)^```$", read_topic("code", section), re.MULTILINE | re.DOTALL)
        (runtime / f"{name}.cs").write_text(code, encoding="utf-8")
    runtime_assembly = {"name": "Foundations", "references": ["SOData.Runtime"]}
    assembly = {"name": "Foundations.EditorTests", "references": ["Foundations", "PlainDomain", "PlainCaller", "SOData.Runtime"], "includePlatforms": ["Editor"], "optionalUnityReferences": ["TestAssemblies"]}
    manifest = fixture / "Packages/manifest.json"
    packages = json.loads(manifest.read_text(encoding="utf-8"))
    packages["dependencies"]["com.unity.nuget.mono-cecil"] = "1.11.6"
    tests = [
        "UnityFoundationTests.cs", "ScriptableObjectDataTests.cs", "SODataControllerTests.cs",
        "SOFileStorageTests.cs", "SOCheckpointTests.cs", "SOAutoFieldsTests.cs", "SODataCodeGenTests.cs",
    ]
    if cloud:
        packages["dependencies"]["com.unity.services.cloudsave"] = "3.4.2"
        for target in (runtime_assembly, assembly):
            target["references"].extend(["Unity.Services.CloudSave", "Unity.Services.Authentication", "Unity.Services.Core"])
            target["versionDefines"] = [{"name": "com.unity.services.cloudsave", "expression": "3.4.2", "define": "SO_DATA_UNITY_CLOUD_SAVE"}]
        shutil.copy2(PACK / "assets/Providers/UnityCloudSave/UnityCloudSaveKeyStore.cs", runtime)
        tests.append("SOCloudProviderTests.cs")
    if netcode:
        packages["dependencies"]["com.unity.netcode.gameobjects"] = "2.14.0"
        for target in (runtime_assembly, assembly):
            target["references"].extend(["Unity.Netcode.Runtime", "Unity.Collections"])
        assembly["references"].append("Unity.Networking.Transport")
        for source in (PACK / "assets/Providers/UnityNetcode").glob("*.cs"):
            shutil.copy2(source, runtime)
        tests.append("SOUnityNetcodeTests.cs")
    manifest.write_text(json.dumps(packages, indent=2), encoding="utf-8")
    (runtime / "Foundations.asmdef").write_text(json.dumps(runtime_assembly), encoding="utf-8")
    (editor / "Foundations.EditorTests.asmdef").write_text(json.dumps(assembly), encoding="utf-8")
    for name in tests:
        shutil.copy2(Path(__file__).with_name("Editor") / name, editor)
    return fixture


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run", type=Path, help="Fresh task Cache run")
    parser.add_argument("--cloud", action="store_true", help="Compile the Unity Cloud Save adapter and run its SDK contract cases.")
    parser.add_argument("--netcode", action="store_true", help="Compile the NGO adapter and run a real localhost server/client pair.")
    args = parser.parse_args()
    print(prepare(args.run, args.cloud, args.netcode))
