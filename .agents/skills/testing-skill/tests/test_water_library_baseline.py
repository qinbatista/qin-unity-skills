"""Keep the installed water library tied to the independently recorded native evidence."""
import json
from pathlib import Path
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(ROOT / "unity-effects-skill/scripts"))
import verify_water_library as verify
import water_templates


class WaterLibraryBaseline(unittest.TestCase):
    def test_catalog_and_checks_match_original_recordings(self):
        gold = json.loads(verify.BASELINE.read_text())
        examples = ROOT / "examples/fluid-mixer"
        matrix = json.loads((examples / "ScenarioMatrix.json").read_text())
        refined = json.loads((examples / "WideCloudburst.json").read_text())
        self.assertEqual(set(gold["subjects"]), set(water_templates.CASES))
        self.assertEqual(len(gold["subjects"]), 37)
        self.assertEqual({n: {k: v for k, v in row.items() if k != "label"}
                          for n, row in gold["subjects"].items()}, matrix["nativeSubjects"])
        self.assertEqual(gold["cloudburstModes"], refined["modeSha256OfOrderedPngDigests"])
        self.assertEqual(sum(row["frames"] for row in gold["subjects"].values()), 5332)
        self.assertEqual(sum(len(c["cases"]) for c in gold["chapters"]), 21)

    def test_original_and_refined_materials_can_coexist(self):
        from compose_water import asset_files
        from fluid_mixer import TEMPLATE, meta_guid

        original_mat = meta_guid(TEMPLATE / "OriginalFluidSurface.mat.meta")
        old_hlsl = meta_guid(TEMPLATE / "OriginalFluidSurface.hlsl.meta")
        self.assertIn(old_hlsl, (TEMPLATE / "OriginalFluidSurface.shadergraph").read_bytes())
        self.assertEqual(verify.file_digest(TEMPLATE / "OriginalFluidSurface.hlsl").hex(),
                         "5f1291f2b2af163347481da9324255634ac67f2e44c06e0de8e5fc1abf492ac2")
        for edition in ("original", "refined"):
            _, actors = water_templates.recipe("WideCloudburst", edition)
            _, profile, settings = actors[0]
            prefab = asset_files(TEMPLATE, "Shower", profile, settings,
                                 original_surface=edition == "original")["Shower.prefab"]
            self.assertEqual(prefab.count(original_mat), 2 if edition == "original" else 0)

    def test_verifier_rejects_a_changed_control_and_a_missing_frame(self):
        with tempfile.TemporaryDirectory() as tmp:
            work = Path(tmp)
            for lane in ("A", "B"):
                folder = work / lane / "captures/RockImpact"
                (folder / "normal").mkdir(parents=True)
                (folder / "normal/0000.png").write_bytes(b"one camera frame")
                (folder / "control.bin").write_bytes(b"solver state")
            movie = work / "Subjects/RockImpact.mp4"
            movie.parent.mkdir()
            movie.write_bytes(b"movie")
            expected = {"frames": 1, "nativeTreeSha256": verify.tree_digest(folder),
                        "fullRateMovieSha256": verify.file_digest(movie).hex()}
            verify.verify_case(work, "RockImpact", expected)
            (folder / "control.bin").write_bytes(b"changed state")
            with self.assertRaisesRegex(ValueError, "B/RockImpact"):
                verify.verify_case(work, "RockImpact", expected)
            (folder / "normal/0000.png").unlink()
            with self.assertRaisesRegex(ValueError, "camera clock"):
                verify.verify_case(work, "RockImpact", expected)


if __name__ == "__main__":
    unittest.main()
