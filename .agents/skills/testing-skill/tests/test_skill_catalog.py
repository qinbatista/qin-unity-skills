"""Check discovery and local reference resolution for the public Skill packages."""

import json
import re
import unittest
from pathlib import Path


PROJECT = Path(__file__).resolve().parents[4]
MARKDOWN_LINK = re.compile(r"\[[^\]]+\]\(([^)]+)\)")


class PublicSkillCatalogTests(unittest.TestCase):
    def test_readme_comparison_and_index_list_every_public_package(self):
        entries = set(PROJECT.glob("unity-*-skill/SKILL.md"))
        self.assertTrue(entries)
        index = json.loads((PROJECT / "skills/ai-index/index.json").read_text(encoding="utf-8"))
        routed = {
            PROJECT / source / "SKILL.md"
            for module in index["modules"]
            for source in module["sources"]
            if re.fullmatch(r"unity-[a-z0-9-]+-skill", source)
        }
        self.assertEqual(routed, entries)
        for relative_path in ("README.md", "docs/yofaassets-comparison.md"):
            document = PROJECT / relative_path
            targets = MARKDOWN_LINK.findall(document.read_text(encoding="utf-8"))
            local_entries = (
                (document.parent / target).resolve()
                for target in targets
                if target.endswith("-skill/SKILL.md") and "://" not in target
            )
            linked = {entry for entry in local_entries if entry.parent.parent == PROJECT}
            with self.subTest(document=relative_path):
                self.assertEqual(linked, entries)

    def test_local_links_survive_installing_one_public_folder(self):
        for entry in sorted(PROJECT.glob("unity-*-skill/SKILL.md")):
            package = entry.parent
            with self.subTest(package=package.name):
                _, frontmatter, _ = entry.read_text(encoding="utf-8").split("---\n", 2)
                self.assertIn(f"name: {package.name}", frontmatter.splitlines())
                self.assertRegex(frontmatter, r"(?m)^description: \S+")
            for document in sorted(package.rglob("*.md")):
                targets = MARKDOWN_LINK.findall(document.read_text(encoding="utf-8"))
                for target in targets:
                    local_path = target.split("#", 1)[0]
                    if not local_path or "://" in local_path:
                        continue
                    resolved = (document.parent / local_path).resolve()
                    with self.subTest(document=document.relative_to(PROJECT), target=target):
                        self.assertIn(package, resolved.parents)
                        self.assertTrue(resolved.is_file())
