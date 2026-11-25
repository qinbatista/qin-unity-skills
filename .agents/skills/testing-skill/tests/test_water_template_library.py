"""Author every approved water case twice through the public command-line interface."""
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[4]
SCRIPTS = ROOT / "unity-effects-skill/scripts"
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(ROOT / "unity-code-skill/scripts"))
import fluid_mixer
from hidden_process import hidden_process_options


def receiver_space():
    cache = ROOT / "Cache"
    cache.mkdir(exist_ok=True)
    for variable in ("TMPDIR", "TEMP", "TMP"):
        raw = os.environ.get(variable)
        if raw:
            parent = Path(raw).resolve()
            if parent.is_dir() and any(p.parent == cache and p.name.startswith("temp-")
                                       for p in (parent, *parent.parents)):
                return tempfile.TemporaryDirectory(dir=parent, prefix="receivers-")
    return tempfile.TemporaryDirectory(dir=cache, prefix="temp-water-library-tests-")


def cli(*args, check=True):
    return subprocess.run([sys.executable, "-B", str(SCRIPTS / "water_templates.py"), *map(str, args)],
                          check=check, capture_output=True, text=True, timeout=45, **hidden_process_options())


class WaterTemplateLibraryTests(unittest.TestCase):
    def test_reauthor_every_named_take_in_two_receivers(self):
        manifest = json.loads((ROOT / "examples/fluid-mixer/ScenarioMatrix.json").read_text())
        cases = manifest["nativeSubjects"]
        listing = [line.split("\t")[0] for line in cli("list").stdout.splitlines()]
        self.assertCountEqual(listing, cases)
        self.assertEqual(len(listing), 37)
        cases_and_editions = [(case, "original") for case in listing] + [("WideCloudburst", "refined")]
        first = {}
        with receiver_space() as work:
            for label, template in (("A", "Assets/Water"), ("B", "Assets/Relocated/Water")):
                project = Path(work) / label
                (project / "Assets").mkdir(parents=True)
                (project / "ProjectSettings").mkdir()
                (project / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 6000.6.7f1\n")
                fluid_mixer.install(project, template)
                for case, edition in cases_and_editions:
                    with self.subTest(lane=label, case=case, edition=edition):
                        name = "Game" + case + ("Revised" if edition == "refined" else "")
                        args = ("author", case, "--edition", edition, "--name", name, "--project", project,
                                "--template", template, "--destination", "Assets/MyGame")
                        result = json.loads(cli(*args).stdout)
                        self.assertEqual(result["status"], "composed")
                        subject = cases[case]
                        self.assertEqual(result["clock"]["frames"], subject["frames"])
                        self.assertEqual(result["clock"]["resolution"], [subject["width"], subject["height"]])
                        self.assertEqual(result["clock"]["fps"], 30)
                        fixture = ROOT / "unity-effects-skill" / result["fixture"]
                        self.assertIn("IEnumerator " + result["method"] + "(", fixture.read_text())
                        self.assertTrue(result["sceneEvents"])
                        paths = [Path(asset) for asset in result["prefabs"]]
                        self.assertEqual(len(paths), 3 if case == "Curtain" else 1)
                        for prefab, actor in zip(paths, result["actors"]):
                            self.assertEqual(prefab.stem, name + actor["suffix"])
                            self.assertEqual(prefab.parent, project / "Assets/MyGame" / name)
                            recipe = prefab.with_suffix(".asset")
                            serialized = prefab.read_text()
                            self.assertIn("value: " + prefab.stem + "\n", serialized)
                            if recipe.is_file():
                                guid = fluid_mixer.meta_guid(recipe.with_suffix(".asset.meta")).decode()
                                self.assertIn("guid: " + guid, serialized)
                                fields = dict(re.findall(r"^  (_\w+): (.+)$", recipe.read_text(), re.MULTILINE))
                            else:
                                fields = dict(re.findall(r"propertyPath: (_\w+)\n      value: ([^\n]+)", serialized))
                            self.assertEqual(fields, actor["serializedParameters"])
                            self.assertTrue(fields)
                        if case == "WideCloudburst":
                            self.assertEqual(fields["_dropCount"], "160")
                            self.assertEqual(fields["_dropSpan"], "5")
                            self.assertEqual(float(fields["_rainExposure"]), 0 if edition == "original" else .035)
                        hashes = {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                                  for p in paths[0].parent.iterdir()}
                        if label == "A":
                            first[case, edition] = hashes
                        else:
                            self.assertEqual(first[case, edition], hashes)
                        if case == "Curtain":
                            self.assertEqual(json.loads(cli(*args).stdout)["status"], "already composed")
                            paths[1].write_text("local artist changes\n")
                            self.assertNotEqual(cli(*args, check=False).returncode, 0)
                            self.assertEqual(paths[1].read_text(), "local artist changes\n")
        self.assertNotEqual(cli("show", "RainPond", "--edition", "refined", check=False).returncode, 0)

    def test_bundle_collision_leaves_no_partial_cast(self):
        with receiver_space() as work:
            project = Path(work)
            (project / "Assets").mkdir()
            (project / "ProjectSettings").mkdir()
            (project / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 6000.6.7f1\n")
            fluid_mixer.install(project)
            cli("author", "RockImpact", "--project", project, "--name", "TowerNeedle")
            failed = cli("author", "Curtain", "--project", project, "--name", "Tower", check=False)
            self.assertIn("GUID", failed.stderr)
            self.assertFalse((project / "Assets/GameWater/Tower").exists())
            self.assertTrue((project / "Assets/GameWater/TowerNeedle/TowerNeedle.prefab").is_file())


if __name__ == "__main__":
    unittest.main()
