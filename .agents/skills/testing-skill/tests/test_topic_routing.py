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

    def test_image_cases_reach_the_installed_runner_from_their_owning_routes(self):
        anchor = "#replay-the-image-optimization-examples"
        section = read_topic("performance", "references/performance-cases.md" + anchor)
        self.assertIn("scripts/replay_image_optimization.py", section)
        self.assertIn("scripts/compare_image_replays.py", section)
        for domain, entry in [
            ("code", "references/global-skill-routing.md#choose-by-task"),
            ("assets", "references/texture-intake.md#build-resizable-world-sprites"),
            ("ui", "references/artwork-intake.md#resize-buttons-and-panels"),
            ("performance", "#choose-an-operation"),
        ]:
            with self.subTest(domain=domain):
                menu = read_topic(domain, entry)
                route = next(link for link in re.findall(r"\]\(([^)]+)\)", menu) if link.endswith(anchor))
                self.assertEqual(read_topic(domain, str(Path(entry.split("#")[0]).parent / route)), section)

    def test_water_flow_and_spells_reach_the_solver_from_both_installed_menus(self):
        task_menu = run_reader()
        self.assertEqual(task_menu.returncode, 0, task_menu.stderr)
        water = next(row for row in task_menu.stdout.splitlines() if "2D water flow, a water spell" in row)
        material = next(row for row in task_menu.stdout.splitlines() if "explicitly requested animated material study" in row)
        for row in (water, material):
            target = re.search(r"unity-effects-skill/SKILL\.md(#[^)]+)", row).group(1)
            self.assertEqual(read_topic("effects", target), read_topic("effects", "#choose-an-operation"))
        effect_menu = read_topic("effects", "#choose-an-operation")
        water = next(row for row in effect_menu.splitlines() if "2D water flow, a water spell" in row)
        section = read_topic("effects", re.search(r"\]\(([^)]+)\)", water).group(1))
        self.assertIn("#choose-and-combine", section)
        self.assertIn("channel-water.md#reproduce-the-variants", section)
        self.assertIn("#author-a-water-effect-from-parameters", section)
        self.assertIn("#2d-application-replays", section)
        self.assertIn("solver state", section)
        material = next(row for row in effect_menu.splitlines() if "explicitly requested animated material study" in row)
        self.assertIn("flow-sky-2d.md#pick-a-subject", material)

        review_link = re.search(r"\]\(([^)]+#review-every-unity-task)\)", effect_menu).group(1)
        review = read_topic("effects", review_link)
        casebook_link = re.search(r"\]\(([^)]+#select-cases-from-the-change)\)", review).group(1)
        casebook = read_topic("performance", casebook_link)
        water_proof = re.search(r"\]\(([^)]+#verify-resource-reuse-across-casts)\)", casebook).group(1)
        section = read_topic("performance", str(Path(casebook_link).parent / water_proof))
        self.assertIn("--case reuse", section)
        self.assertIn("FluidVerification.json", section)

    def test_pack_changes_route_to_the_contribution_workflow(self):
        code_menu = read_topic("code", "#choose-an-operation")
        pack_menu = run_reader()
        self.assertEqual(pack_menu.returncode, 0, pack_menu.stderr)
        link = re.search(r"\((references/contributing\.md#[^)]+)\)", code_menu)
        self.assertIsNotNone(link)
        self.assertIn(f"({link.group(1).removeprefix('references/')})", pack_menu.stdout)
        contribution = run_reader("code", link.group(1))
        self.assertEqual(contribution.returncode, 0, contribution.stderr)
        self.assertIn("shared library of Unity methods", contribution.stdout)
        self.assertIn("problem its current guidance leaves unresolved", contribution.stdout)
        self.assertIn("local debugging or testing", contribution.stdout)
        self.assertIn("Use the contributor route whenever ownership is unverified", contribution.stdout)
        reference_path = link.group(1).split("#", 1)[0]
        owner_route = read_topic("code", reference_path + "#deliver-as-the-repository-owner")
        self.assertIn("matches the upstream repository owner", owner_route)
        self.assertIn("push it directly", owner_route)
        self.assertIn("remote branch, and tracking ref", owner_route)
        contributor_route = read_topic("code", reference_path + "#submit-as-a-contributor")
        self.assertIn("fresh topic branch", contributor_route)
        self.assertIn("your fork", contributor_route)
        self.assertIn("open one PR", contributor_route)
        for owner in json.loads((RELEASE / "skills.json").read_text(encoding="utf-8")):
            domain = owner[len("unity-"):-len("-skill")]
            with self.subTest(domain=domain):
                menu = read_topic(domain, "#choose-an-operation")
                reference = re.search(r"\(((?:\.\./unity-code-skill/)?references/contributing\.md#[^)]+)\)", menu)
                self.assertIsNotNone(reference)
                self.assertEqual(read_topic(domain, reference.group(1)), contribution.stdout)

    def test_each_domain_reaches_the_same_performance_review(self):
        review = read_topic("performance", "#review-every-unity-task")
        for owner in json.loads((RELEASE / "skills.json").read_text(encoding="utf-8")):
            domain = owner.removeprefix("unity-").removesuffix("-skill")
            if domain == "performance":
                continue
            with self.subTest(domain=domain):
                menu = read_topic(domain, "#choose-an-operation")
                link = re.search(r"\]\(([^)]+#review-every-unity-task)\)", menu)
                self.assertIsNotNone(link, "The direct domain menu must reach the performance review")
                self.assertEqual(read_topic(domain, link.group(1)), review)

    def test_performance_menu_opens_implementation_and_measurement_sections(self):
        menu = run_reader("performance")
        self.assertEqual(menu.returncode, 0, menu.stderr)
        scenarios = (
            ("addressables", "runtime-code.md", ("LoadAssetAsync", "ReleaseInstance")),
            ("followers", "simulation-and-effects.md", ("GameObject", "Entities", "Burst", "VFX Graph")),
            ("frame spike", "measure-and-verify.md", ("CPU", "GPU", "p95")),
        )
        for symptom, guide, decisions in scenarios:
            with self.subTest(symptom=symptom):
                rows = [line for line in menu.stdout.splitlines() if line.startswith("|") and symptom in line.lower()]
                self.assertEqual(len(rows), 1)
                link = re.search(r"\]\((references/[^)]+#[^)]+)\)", rows[0])
                self.assertIsNotNone(link)
                self.assertEqual(link.group(1).partition("#")[0], "references/" + guide)
                section = read_topic("performance", link.group(1))
                for decision in decisions:
                    self.assertIn(decision, section)

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
        edge_review = read_topic("effects", "references/rendering-and-verification.md#trace-an-unexplained-straight-edge")
        self.assertIn("longest contiguous arc", edge_review)
        repair = re.search(r"\(staged-effects\.md(#[^)]+)\)", edge_review)
        self.assertIsNotNone(repair)
        self.assertIn("premultiplied color", read_topic("effects", "references/staged-effects.md" + repair.group(1)))

    def test_heading_fragments_ignore_fenced_examples_and_disambiguate_repeats(self):
        lines = ["# Guide", "## C# checks", "````text", "## Example", "```", "## Still fenced", "````", "### C# checks", "~~~", "## Other example", "~~~"]
        self.assertEqual([heading.anchor for heading in markdown_headings(lines)], ["guide", "c-checks", "c-checks-1"])

    def test_effect_motion_menu_reaches_design_graph_and_native_control(self):
        menu = read_topic("effects", "#choose-an-operation")
        row = next(line for line in menu.splitlines() if "appears frozen" in line)
        design, graph, replay = [read_topic("effects", link) for link in re.findall(r"\]\(([^)]+)\)", row)]
        self.assertIn("thorax", design)
        self.assertIn("Custom Function", graph)
        self.assertIn("Master Stack", graph)
        self.assertIn("frozen-object control", replay)
        self.assertIn("trackable motion in the wrong direction", replay)
        self.assertIn("coverage over the full cast before encoding", replay)
        self.assertIn("stationary fading shape", replay)

    def test_effect_case_reproduction_routes_to_rebuild_and_native_replay(self):
        menu = read_topic("effects", "#choose-an-operation")
        row = next(line for line in menu.splitlines() if "audit a set of examples" in line)
        rebuild, replay = [read_topic("effects", link) for link in re.findall(r"\]\(([^)]+)\)", row)]
        self.assertIn("generator inputs", rebuild)
        self.assertIn("prior outputs and their import caches absent", rebuild)
        self.assertIn("repetition count in the brief", rebuild)
        self.assertIn("per effect and creation", rebuild)
        examples = next(line for line in menu.splitlines() if "three-creation replay" in line)
        reference = re.search(r"\]\(([^)]+)\)", examples).group(1)
        creation = read_topic("effects", reference)
        self.assertIn("replay_particle_graphs.py", creation)
        self.assertIn("particle_graph_checks.py", creation)
        self.assertIn("escaped newlines before `#include`", rebuild)
        self.assertIn("controller types stored inline", rebuild)
        self.assertIn("reimport Shader Graphs before their VFX Graph consumers", rebuild)
        self.assertIn("seed", replay.lower())
