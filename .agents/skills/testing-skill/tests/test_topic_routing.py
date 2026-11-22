"""Exercise task routing through the same files a grouped installation receives."""

import json
import re
import subprocess
import sys
import unittest
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[4]
RELEASE = PROJECT / "plugins/qin-unity-skills"
PACK = RELEASE / "skills"
SCRIPTS = PACK / "unity-code-skill/scripts"
sys.path.insert(0, str(SCRIPTS))

from hidden_process import hidden_process_options
from read_topic import markdown_headings, read_topic


def run_reader(*args):
    command = [sys.executable, "-B", str(SCRIPTS / "read_topic.py"), *args]
    return subprocess.run(command, cwd=PROJECT, capture_output=True, encoding="utf-8", timeout=15, **hidden_process_options())


class InstalledRoutingTests(unittest.TestCase):
    def test_task_and_domain_menus(self):
        entry = run_reader()
        self.assertEqual(entry.returncode, 0, entry.stderr)
        self.assertIn("## Choose by task", entry.stdout)
        self.assertIn("release distribution", entry.stdout)
        self.assertNotIn("## Require routing across projects", entry.stdout)
        optimize = run_reader("#optimize-by-cost")
        self.assertEqual(optimize.returncode, 0, optimize.stderr)
        self.assertIn("unknown cause", optimize.stdout)
        self.assertIn("Calibrate an allocation claim", optimize.stdout)
        self.assertNotIn("store submission", optimize.stdout)
        settings = run_reader("#project-settings-and-release")
        self.assertEqual(settings.returncode, 0, settings.stderr)
        self.assertIn("3D light baking", settings.stdout)
        self.assertIn("signing", settings.stdout)
        for owner in json.loads((RELEASE / "skills.json").read_text(encoding="utf-8")):
            domain = owner[len("unity-"):-len("-skill")]
            with self.subTest(domain=domain):
                result = run_reader(domain)
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertIn("**Project rules come first.**", result.stdout)
                self.assertIn("## Choose an operation", result.stdout)
                self.assertNotIn("description:", result.stdout)

    def test_published_files_match_their_sources(self):
        for owner in json.loads((RELEASE / "skills.json").read_text(encoding="utf-8")):
            source = PROJECT / owner
            published = PACK / owner
            with self.subTest(owner=owner):
                files = {
                    file.relative_to(source) for file in source.rglob("*") if file.is_file()
                    and file.relative_to(source).parts[0] in {"SKILL.md", "references", "scripts", "assets", "agents"}
                    and "__pycache__" not in file.parts
                }
                self.assertEqual(files, {p.relative_to(published) for p in published.rglob("*") if p.is_file() and "__pycache__" not in p.parts})
                for file in files:
                    self.assertEqual((source / file).read_bytes(), (published / file).read_bytes(), str(file))

    def test_every_published_markdown_fragment_resolves(self):
        for doc in PACK.glob("unity-*-skill/**/*.md"):
            for link in re.findall(r"\[[^\]]+\]\(([^)]+)\)", doc.read_text(encoding="utf-8")):
                target, separator, anchor = link.partition("#")
                if not separator or "://" in target:
                    continue
                destination = (doc.parent / target).resolve() if target else doc
                relative = destination.relative_to(PACK)
                domain = relative.parts[0][len("unity-"):-len("-skill")]
                with self.subTest(document=doc.relative_to(PACK), link=link):
                    section = read_topic(domain, relative.relative_to(relative.parts[0]).as_posix() + "#" + anchor)
                    self.assertTrue(section.strip())

    def test_sprite_size_and_crunch_keep_their_own_rules(self):
        address = "references/texture-intake.md"
        size = run_reader("assets", address + "#size-new-world-art")
        self.assertEqual(size.returncode, 0, size.stderr)
        self.assertIn("project's stated PPU", size.stdout)
        self.assertIn("350 visible pixels", size.stdout)
        self.assertNotIn("migrate PPU", size.stdout)
        self.assertNotIn("Crunch", size.stdout)
        crunch = read_topic("assets", address + "#target-formats-and-crunch")
        self.assertIn("strictest verified requirement", crunch)
        self.assertIn("base GPU format", crunch)
        self.assertNotIn("### Settings by texture role", crunch)
        apply = read_topic("assets", address + "#execute-and-verify-the-import")
        self.assertIn("SaveAndReimport", apply)
        self.assertIn("Restore the prior", apply)
        self.assertIn("local fileIDs", apply)

    def test_ui_profiling_and_build_sections_keep_acceptance(self):
        locale = read_topic("ui", "references/layout-and-input.md#verify-each-locale")
        self.assertIn("## Localized content", locale)
        self.assertIn("### Verify each locale", locale)
        self.assertNotIn("### Bind player-facing text by key", locale)
        self.assertNotIn("## Check buttons and destination pages", locale)
        path = "references/runtime-and-artifact-evidence.md"
        frame = read_topic("testing", path + "#frame-pacing-and-memory")
        self.assertIn("Compare cold startup", frame)
        self.assertNotIn("## Isolated test packaging", frame)
        existing = read_topic("testing", "#existing-test-package")
        self.assertIn("installed application", existing)
        self.assertNotIn("fresh copy", existing)
        build = read_topic("testing", path + "#isolated-test-packaging")
        self.assertIn("YoFaPublisher", build)
        self.assertIn("Read back and verify the exact produced package", build)
        self.assertNotIn("## macOS batch startup", build)

    def test_outline_and_bad_requests(self):
        outline = run_reader("assets", "references/texture-intake.md")
        self.assertEqual(outline.returncode, 0, outline.stderr)
        self.assertIn("(#world-sprite-scale)", outline.stdout)
        self.assertNotIn("350 visible pixels", outline.stdout)
        for arguments in (("unknown",), ("ui", "../../../README.md"), ("code", "scripts/read_topic.py"), ("assets", "references/texture-intake.md#missing")):
            with self.subTest(arguments=arguments):
                failed = run_reader(*arguments)
                self.assertEqual(failed.returncode, 2)
                self.assertTrue(failed.stderr.strip())
                self.assertEqual(failed.stdout, "")

    def test_gait_lookup_keeps_speed_step_count_and_contact_together(self):
        menu = read_topic("animation", "#choose-an-operation")
        link = re.search(r"\(([^)]+#ground-travel-and-step-cadence)\)", menu)
        self.assertIsNotNone(link)
        result = run_reader("animation", link.group(1))
        self.assertEqual(result.returncode, 0, result.stderr)
        rows = re.findall(r"^\| ([\d.]+) m/s \| ([\d.]+) s \| (\d+) \| (\d+) \|$", result.stdout, re.MULTILINE)
        self.assertEqual(len(rows), 2)
        full, half = [list(map(float, row)) for row in rows]
        self.assertGreater(full[0], 0)
        self.assertEqual(half[0], full[0] / 2)
        self.assertEqual(half[1], full[1] * 2)
        self.assertEqual(half[2], full[2] / 2)
        self.assertEqual(half[3], full[3])
        self.assertIn("support's local space", result.stdout)
        self.assertIn("even if the step counts pass", result.stdout)
        self.assertNotIn("## Fit the actual source", result.stdout)

    def test_gameplay_animation_and_effect_routes_preserve_native_contracts(self):
        jobs = read_topic("code", "references/unity-game-code-structure-design.md#completed-job-batches")
        self.assertIn("TransformAccess.position", jobs)
        self.assertIn("presentation subscription disabled", jobs)
        inventory = read_topic("animation", "references/spine-to-unity-contract.md#source-inventory-schema")
        self.assertIn("compatible reader for binary input", inventory)
        self.assertNotIn("## Conversion and rig signature", inventory)
        rendering = read_topic("effects", "references/rendering-and-verification.md#observe-the-actual-render-path-and-lifetime")
        self.assertIn("rendered pixels", rendering)
        self.assertIn("actual workload and platform", rendering)

    def test_heading_fragments_ignore_fenced_examples_and_disambiguate_repeats(self):
        lines = ["# Guide", "## C# checks", "````text", "## Example", "```", "## Still fenced", "````", "### C# checks", "~~~", "## Other example", "~~~"]
        self.assertEqual([heading.anchor for heading in markdown_headings(lines)], ["guide", "c-checks", "c-checks-1"])
