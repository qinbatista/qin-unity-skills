import importlib.util
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "scripts" / "csharp_rule_guard.py"
SPEC = importlib.util.spec_from_file_location("csharp_rule_guard", SCRIPT_PATH)
module = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(module)


class CSharpRuleGuardTests(unittest.TestCase):
    def codes(self, source):
        return {item["code"] for item in module.csharp_violations(source)}

    def test_pattern_wildcard_is_allowed(self):
        self.assertEqual(self.codes("case _:\n    break;\n"), set())

    def test_unity_csharp_rejects_named_pitfalls(self):
        source = "\n".join(["_ = SaveDataAsync();", "var controller = new PlayerController();", "void SaveData() { SaveData(); }", "public bool SaveData(Data value) => _provider.SaveData(value);", "SaveData(", "    value);", "Widget widget = new Widget(", "    first,", "    second);"])
        self.assertTrue({"discard-assignment", "obvious-var", "self-recursion", "pass-through-wrapper", "avoidable-vertical-call"}.issubset(self.codes(source)))

    def test_unity_csharp_rejects_multiline_self_recursion_and_pass_through_wrapper(self):
        source = "\n".join(["void SaveData()", "{", "    SaveData();", "}", "", "public bool PersistData(Data value)", "{", "    return _provider.PersistData(value);", "}"])
        self.assertTrue({"self-recursion", "pass-through-wrapper"}.issubset(self.codes(source)))

    def test_unity_csharp_allows_direct_call_explicit_type_and_marked_facade(self):
        source = "\n".join(["await SaveDataAsync();", "PlayerController controller = new PlayerController();", "// code-gate: semantic-boundary=public validation", "public bool SaveData(Data value) => _provider.SaveData(value);"])
        self.assertEqual(self.codes(source), set())

    def test_changed_line_filter_does_not_fail_for_legacy_violation_outside_patch(self):
        violations = [module.violation("legacy", 2, "old"), module.violation("new", 7, "new", 9)]
        self.assertEqual([item["code"] for item in module.filter_violations(violations, {8})], ["new"])
