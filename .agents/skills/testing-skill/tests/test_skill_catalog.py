"""Check discovery and local reference resolution for the public Skill packages."""

import json
import re
import unittest
from pathlib import Path


PROJECT = Path(__file__).resolve().parents[4]
MARKDOWN_LINK = re.compile(r"\[[^\]]+\]\(([^)]+)\)")
UNITY_CLI_GUIDE = PROJECT / "unity-code-skill/references/unity-cli.md"


class PublicSkillCatalogTests(unittest.TestCase):
    def test_all_catalogs_list_the_same_public_packages(self):
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
        release = json.loads((PROJECT / "plugins/qin-unity-skills/skills.json").read_text(encoding="utf-8"))
        self.assertCountEqual(release, [entry.parent.name for entry in entries])
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

    def test_layout_and_gameplay_skills_route_to_existing_game_architecture(self):
        self.assertIn("\n## Existing-game architecture\n", UNITY_CLI_GUIDE.read_text(encoding="utf-8"))
        for name in ("unity-assets-skill", "unity-code-skill"):
            entry = PROJECT / name / "SKILL.md"
            targets = MARKDOWN_LINK.findall(entry.read_text(encoding="utf-8"))
            with self.subTest(skill=name):
                routes = {
                    (entry.parent / target.split("#", 1)[0]).resolve()
                    for target in targets
                    if target.endswith("#existing-game-architecture")
                }
                self.assertEqual(routes, {UNITY_CLI_GUIDE})

    def test_package_links_resolve_and_references_are_linked(self):
        self.assertIn("\n## Standalone CLI\n", UNITY_CLI_GUIDE.read_text(encoding="utf-8"))
        for entry in sorted(PROJECT.glob("unity-*-skill/SKILL.md")):
            package = entry.parent
            linked_files = set()
            with self.subTest(package=package.name):
                frontmatter, instructions = entry.read_text(encoding="utf-8").split("---\n", 2)[1:]
                self.assertIn(f"name: {package.name}", frontmatter.splitlines())
                self.assertRegex(frontmatter, r"(?m)^description: \S+")
                opening = instructions.strip().split("\n\n", 2)[1]
                self.assertTrue(opening.startswith("**Project rules come first.**"))
                self.assertIn("no project or registered shared owner governs Unity CLI", opening)
                standalone_routes = {
                    (package / target.split("#", 1)[0]).resolve()
                    for target in MARKDOWN_LINK.findall(opening)
                    if target.endswith("#standalone-cli")
                }
                self.assertEqual(standalone_routes, {UNITY_CLI_GUIDE})
            for document in sorted(package.rglob("*.md")):
                targets = MARKDOWN_LINK.findall(document.read_text(encoding="utf-8"))
                for target in targets:
                    local_path = target.split("#", 1)[0]
                    if not local_path or "://" in local_path:
                        continue
                    resolved = (document.parent / local_path).resolve()
                    with self.subTest(document=document.relative_to(PROJECT), target=target):
                        self.assertTrue(resolved == UNITY_CLI_GUIDE or package in resolved.parents)
                        self.assertTrue(resolved.is_file())
                        linked_files.add(resolved)
            with self.subTest(package=package.name):
                self.assertFalse(set((package / "references").glob("*.md")) - linked_files)
