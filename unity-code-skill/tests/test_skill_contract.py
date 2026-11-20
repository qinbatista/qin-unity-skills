"""Verify the moved rule contracts and local links in the standalone Skill."""

import re
import unittest
from pathlib import Path


SKILL_ROOT = Path(__file__).resolve().parents[1]


class UnitySkillContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        paths = [SKILL_ROOT / "SKILL.md", *sorted((SKILL_ROOT / "references").glob("*.md"))]
        cls.documents = {path.relative_to(SKILL_ROOT).as_posix(): path.read_text(encoding="utf-8") for path in paths}

    def assert_concepts(self, relative_path, concepts):
        document = self.documents[relative_path].casefold()
        for concept in concepts:
            with self.subTest(file=relative_path, concept=concept):
                self.assertIn(concept.casefold(), document)

    def test_unity_style_and_applicability_are_explicit(self):
        self.assert_concepts("references/unity-csharp-rules.md", ["other C# work uses its actual runtime", "one physical line", "single-statement", "explicit concrete type", "`private`", "underscore names", "`internal` access modifier", "case _:", "unless requested"])

    def test_unity_structure_is_general_and_keeps_data_exceptions(self):
        self.assert_concepts("references/unity-game-code-structure-design.md", ["not feature recipes", "do not apply to Editor", "XXController", "XXManager", "single-instance", "operate autonomously", "serializes ScriptableObject references only", "Transient runtime state", "Immutable constants", "canonical ScriptableObject location", "Factory", "Object Pool", "State", "Command", "Observer", "Prototype", "Singleton"])
        for consumer_name in ("SpriteTamerSystem", "SpriteMovementState", "DamageNumberManager", "CombatService"):
            self.assertNotIn(consumer_name, self.documents["references/unity-game-code-structure-design.md"])

    def test_optional_unity_categories_preserve_lifecycle_and_provider_ownership(self):
        self.assert_concepts("references/unity-lifecycle-and-serialization.md", ["symmetric pair", "named field", "Unity main thread", "serialized field compatibility", "Do not repeat `GetComponent`"])
        self.assert_concepts("references/unity-service-integration.md", ["optional or interchangeable", "neutral contract", "Do not manufacture", "Forward each callback or operation once", "uninitialized, initializing, ready, and failed", "exact surface has fresh evidence"])

    def test_references_resolve_and_entry_is_compact(self):
        for relative_path, content in self.documents.items():
            if not relative_path.endswith(".md"):
                continue
            for target in re.findall(r"\[[^\]]+\]\(([^)]+)\)", content):
                if "://" in target:
                    continue
                with self.subTest(file=relative_path, target=target):
                    self.assertTrue(((SKILL_ROOT / relative_path).parent / target.split("#", 1)[0]).exists())
        self.assertLess(len(self.documents["SKILL.md"].split()), 850)
