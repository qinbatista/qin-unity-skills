"""Check discovery and local reference resolution for the public Skill packages."""

import json
import re
import unittest
from pathlib import Path


PROJECT = Path(__file__).resolve().parents[4]
MARKDOWN_LINK = re.compile(r"\[[^\]]+\]\(([^)]+)\)")
UNITY_CLI_GUIDE = PROJECT / "unity-code-skill/references/unity-cli.md"
UNITY_ROUTING_GUIDE = PROJECT / "unity-code-skill/references/global-skill-routing.md"


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
        for document in (PROJECT / "README.md", PROJECT / "docs/yofaassets-comparison.md", UNITY_ROUTING_GUIDE):
            targets = MARKDOWN_LINK.findall(document.read_text(encoding="utf-8"))
            local_entries = (
                (document.parent / target).resolve()
                for target in targets
                if target.endswith("/SKILL.md") and "://" not in target
            )
            linked = {entry for entry in local_entries if entry.parent.parent == PROJECT}
            with self.subTest(document=document.relative_to(PROJECT)):
                self.assertEqual(linked, entries)
        testing = PROJECT / "unity-testing-skill/SKILL.md"
        packaging = {
            (UNITY_ROUTING_GUIDE.parent / target.split("#", 1)[0]).resolve()
            for target in MARKDOWN_LINK.findall(UNITY_ROUTING_GUIDE.read_text(encoding="utf-8"))
            if target.endswith("#test-packaging")
        }
        self.assertEqual(packaging, {testing})
        self.assertIn("\n## Test packaging\n", testing.read_text(encoding="utf-8"))

    def test_screen_profiles_and_reader_ship_together(self):
        source = PROJECT / "unity-ui-skill"
        published = PROJECT / "plugins/qin-unity-skills/skills/unity-ui-skill"
        for path in ("assets/screen-layouts.json", "assets/ScreenLayoutPolicy.cs", "assets/camera-aperture.svg", "references/screen-profiles.md"):
            with self.subTest(path=path):
                self.assertEqual((source / path).read_bytes(), (published / path).read_bytes())
        policy = json.loads((source / "assets/screen-layouts.json").read_text(encoding="utf-8"))
        self.assertEqual(policy["version"], 2)
        self.assertEqual(policy["edgeActions"]["sideCameraPlacement"], "shift-inward")
        self.assertEqual(policy["edgeActions"]["sideCameraAppearance"], "artwork-ring")
        self.assertEqual(policy["edgeActions"]["cameraRingDiameterInHubs"], 2.0)
        limits = [profile["maxWidthOverHeight"] for profile in policy["profiles"]]
        self.assertEqual(limits[-1], 0)
        self.assertGreater(limits[0], 0)
        self.assertEqual(limits[:-1], sorted(set(limits[:-1])))
        self.assertEqual(len({p["id"] for p in policy["profiles"]}), len(limits))

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
        entries = set(PROJECT.glob("unity-*-skill/SKILL.md"))
        for entry in sorted(entries):
            package = entry.parent
            linked_files = set()
            with self.subTest(package=package.name):
                source = entry.read_text(encoding="utf-8")
                self.assertNotIn("qin-unity-csharp-minimal-style", source)
                frontmatter, instructions = source.split("---\n", 2)[1:]
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
                        routed_entry = document == UNITY_ROUTING_GUIDE and resolved in entries
                        shared_reader = resolved == PROJECT / "unity-code-skill/scripts/read_topic.py"
                        image_audit = resolved == PROJECT / "unity-assets-skill/SKILL.md" and target.endswith("#admit-artwork-and-textures")
                        self.assertTrue(routed_entry or shared_reader or image_audit or resolved == UNITY_CLI_GUIDE or package in resolved.parents)
                        self.assertTrue(resolved.is_file())
                        linked_files.add(resolved)
            with self.subTest(package=package.name):
                self.assertFalse(set((package / "references").glob("*.md")) - linked_files)
