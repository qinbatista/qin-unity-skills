"""Assemble the two published C# bases and their documented consumers for Unity Test Runner."""

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


def prepare(run):
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

    for name in ("Singleton.cs", "SOLocalData.cs"):
        shutil.copy2(PACK / "assets" / name, runtime)
    examples = {
        "AudioPreferencesManager": "references/unity-game-code-structure-design.md#singleton-managers",
        "SOAudioPreferences": "references/unity-lifecycle-and-serialization.md#scriptableobject-local-files",
    }
    for name, section in examples.items():
        code, = re.findall(r"^```csharp\n(.*?)^```$", read_topic("code", section), re.MULTILINE | re.DOTALL)
        (runtime / f"{name}.cs").write_text(code, encoding="utf-8")
    (runtime / "Foundations.asmdef").write_text(json.dumps({"name": "Foundations"}), encoding="utf-8")
    assembly = {"name": "Foundations.EditorTests", "references": ["Foundations"], "includePlatforms": ["Editor"], "optionalUnityReferences": ["TestAssemblies"]}
    (editor / "Foundations.EditorTests.asmdef").write_text(json.dumps(assembly), encoding="utf-8")
    shutil.copy2(Path(__file__).with_name("Editor") / "UnityFoundationTests.cs", editor)
    return fixture


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run", type=Path, help="Fresh task Cache run")
    print(prepare(parser.parse_args().run))
