"""Check selection and fail-closed result handling in the installed Unity replay."""

import importlib.util
from pathlib import Path
import re
import tempfile
import unittest
import xml.etree.ElementTree as ET

PROJECT = Path(__file__).resolve().parents[4]
PACK = PROJECT / "plugins/qin-unity-skills/skills/unity-performance-skill"
spec = importlib.util.spec_from_file_location("packaged_performance_replay", PACK / "scripts/replay_cases.py")
replay = importlib.util.module_from_spec(spec)
spec.loader.exec_module(replay)


class PerformanceReplayTests(unittest.TestCase):
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
