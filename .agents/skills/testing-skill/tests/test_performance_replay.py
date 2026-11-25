"""Check selection and fail-closed result handling in the installed Unity replay."""

import hashlib
import importlib.util
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

PROJECT = Path(__file__).resolve().parents[4]
PACK = PROJECT / "plugins/qin-unity-skills/skills/unity-performance-skill"
spec = importlib.util.spec_from_file_location("packaged_performance_replay", PACK / "scripts/replay_cases.py")
replay = importlib.util.module_from_spec(spec)
spec.loader.exec_module(replay)


class PerformanceReplayTests(unittest.TestCase):
    def test_image_replay_prepares_all_inputs_from_an_isolated_pack(self):
        inputs = {
            "ProjectSettings/ProjectVersion.txt", "Packages/manifest.json", "Packages/packages-lock.json",
            "Assets/PulseTheme.tss", "Assets/ResizableBacking.uss",
            "Assets/Editor/ImageExample.Tests.asmdef", "Assets/Editor/WorldRepeatSprite.cs",
            "Assets/Editor/ControlArtExample.cs", "Assets/Editor/ImportAtlasExample.cs",
            "Assets/Editor/WorldArtExample.cs", "Assets/ImageExamples/UI/Watchtower.png",
            "Assets/ImageExamples/UI/WoodlandFrame.png", "Assets/ImageExamples/World/Ground.png",
            "Assets/ImageExamples/World/TreeBase.png", "Assets/ImageExamples/World/TreeCrown.png",
            "Assets/ImageExamples/World/TreeTrunk.png", "Assets/ImageExamples/World/Valley.png",
        }
        with tempfile.TemporaryDirectory() as directory:
            work = Path(directory)
            install = work / "pack"
            shutil.copytree(PACK.parent, install)
            runner = install / PACK.name / "scripts/replay_image_optimization.py"
            options = dict(cwd=work, capture_output=True, text=True, timeout=20, **replay.hidden_process_options())
            cli = subprocess.run([sys.executable, "-B", str(runner), "--help"], **options)
            self.assertEqual(cli.returncode, 0, cli.stderr)
            self.assertIn("--work", cli.stdout)

            receiver = work / "blank"
            prepare = "\n".join([
                "import json, sys",
                "from pathlib import Path",
                "sys.path.insert(0, sys.argv[1])",
                "from replay_image_optimization import prepare",
                "print(json.dumps(prepare(Path(sys.argv[2]))))",
            ])
            result = subprocess.run(
                [sys.executable, "-I", "-B", "-c", prepare, str(runner.parent), str(receiver)], **options
            )
            self.assertEqual(result.returncode, 0, result.stderr)
            hashes = json.loads(result.stdout)
            self.assertEqual({p.relative_to(receiver).as_posix() for p in receiver.rglob("*") if p.is_file()}, inputs)
            by_name = {Path(path).name: digest for path, digest in hashes.items()}
            self.assertEqual(len(hashes), len(inputs))
            for name, digest in hashes.items():
                self.assertEqual(hashlib.sha256((install / name).read_bytes()).hexdigest(), digest, name)
            for name in inputs:
                self.assertEqual(hashlib.sha256((receiver / name).read_bytes()).hexdigest(), by_name[Path(name).name], name)

    def test_all_shipped_native_cases_can_be_selected(self):
        cases = replay.catalog()
        actual = set()
        for source in (PACK / "assets/cases/Editor").glob("*.cs"):
            methods = re.findall(r"\[(?:UnityTest|Test)\]\s+public (?:void|IEnumerator) (\w+)\(",
                                 source.read_text(encoding="utf-8"))
            # One file may contain more than one fixture. Resolve each leaf to its declaring class.
            for method in methods:
                text = source.read_text(encoding="utf-8").split(f" {method}(", 1)[0]
                owner = re.findall(r"\bclass (\w+Cases)\b", text)[-1]
                actual.add(f"{owner}.{method}")
        self.assertEqual(set(cases), actual)
        all_cases = replay.select_cases(cases, [])
        self.assertCountEqual(all_cases, actual)
        for name in all_cases:
            self.assertEqual(replay.select_cases(cases, [name, name]), [name])
            self.assertIn(name, replay.select_cases(cases, [name.partition(".")[0]]))
        with self.assertRaises(ValueError):
            replay.select_cases(cases, ["misspelled"])

    def test_skips_duplicates_unrelated_leaves_and_empty_reports_cannot_pass(self):
        case = next(iter(replay.catalog()))
        expected = [case]
        name = f"{replay.NAMESPACE}.{case}"
        with tempfile.TemporaryDirectory() as run:
            result = Path(run) / "results.xml"
            for leaves in ([], [(name, "Skipped")], [(name, "Passed"), (name, "Passed")],
                           [("Unrelated.SameMethod", "Passed")], [(name, "Failed")], [(name, "Passed")]):
                root = ET.Element("test-run", result="Passed")
                for full_name, status in leaves:
                    ET.SubElement(root, "test-case", fullname=full_name, result=status)
                ET.ElementTree(root).write(result)
                with self.subTest(leaves=leaves):
                    if leaves == [(name, "Passed")]:
                        replay.verify_results(result, expected)
                    else:
                        with self.assertRaises(ValueError):
                            replay.verify_results(result, expected)
