"""Pin legacy game checkout identities in the published Unity package data."""

import json
import unittest
from pathlib import Path


PROJECT = Path(__file__).resolve().parents[4]


class ProjectMemoryOwnerTests(unittest.TestCase):
    def test_legacy_roots_keep_their_canonical_owners(self):
        catalog = PROJECT / "unity-code-skill/assets/project-memory-owners.json"
        data = json.loads(catalog.read_text(encoding="utf-8"))
        self.assertEqual(data["schema_version"], 1)
        roots = dict(data["document_roots"])
        self.assertEqual(len(roots), len(data["document_roots"]))
        for owner in ("ThisIsMyOregon", "AIAnimation2D", "AIShaderGraphic2D", "AIVFX2D", "YoFaAssets", "YoFaPublisher", "YoFaUnityPlguin", "Destiny", "FunctionWebsite", "MetaStory", "UnityCodexTest", "XNews"):
            with self.subTest(owner=owner):
                self.assertEqual(roots[f"YofaGames/{owner}"], owner)
        self.assertEqual(roots["YofaGames/SpriteTamer"], "ThisIsMyOregon")
        self.assertEqual(roots["Mokozoo/Unity-淘金热"], "Unity-淘金热")
        for owner in ("MetaStory", "UnityCodexTest"):
            self.assertEqual(roots[f"Unity3DPersonalProject/{owner}"], roots[f"YofaGames/{owner}"])
