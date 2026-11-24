"""Protect portable fluid installation, identity and the user's existing assets."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

REPO = Path(__file__).resolve().parents[4]
SCRIPTS = REPO / "unity-effects-skill/scripts"
sys.path.insert(0,str(SCRIPTS))
import fluid_mixer
from compose_water import compose


class FluidInstallTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.project = Path(self.directory.name)
        (self.project/"Assets").mkdir()
        (self.project/"ProjectSettings").mkdir()
        (self.project/"ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 6000.6.7f1\n")

    def test_relocation_reuse_and_artist_changes(self):
        path,result = fluid_mixer.install(self.project,"Assets/Owner/Water")
        self.assertEqual(result,"installed")
        expected = {"PlanarRipples", "FallingMass", "FreeJet", "RockImpact", "OpposedJets", "Waterfall",
                    "Vortex", "WaterNeedle2D", "WaterCurtain2D", "ContinuousWaterfall"}
        self.assertEqual({asset.stem for asset in (path / "Presets").glob("*.asset")}, expected)
        self.assertEqual({asset.stem for asset in (path / "Variants").glob("*.prefab")}, expected)
        self.assertEqual(fluid_mixer.install(self.project,"Assets/Owner/Water")[1],"already installed")
        with self.assertRaisesRegex(ValueError,"GUID"):
            fluid_mixer.install(self.project,"Assets/Second")
        pellet = path/"Presets/FallingMass.asset"
        pellet.write_text(pellet.read_text().replace("_gravity: 8","_gravity: 4"))
        with self.assertRaisesRegex(ValueError,"preserved"):
            fluid_mixer.install(self.project,"Assets/Owner/Water")
        self.assertIn("_gravity: 4",pellet.read_text())

    def test_author_custom_channel_identity_and_preserve_artist_work(self):
        base = "Assets/Second/Water"
        fluid_mixer.install(self.project, base)
        settings = {"bank": 1.12, "period": 2.6}
        def make(folder="Assets/Spells", name="SmallCove", values=None):
            return compose(self.project, base, folder, name, "shore", settings if values is None else values)
        prefab, status = make()
        self.assertEqual(status, "composed")
        self.assertIn("propertyPath: _period\n      value: 2.6", prefab.read_text())
        self.assertEqual(make(), (prefab, "already composed"))
        with self.assertRaisesRegex(ValueError, "GUID"):
            make("Assets/Other")
        for key, value in (("period", float("nan")), ("bank", 2)):
            with self.assertRaises(ValueError): make(name="Invalid", values={key: value})
        with self.assertRaises(ValueError): make(folder=base)
        prefab.write_text(prefab.read_text().replace("value: 2.6", "value: 4.0"))
        with self.assertRaisesRegex(ValueError, "preserved"): make()
        self.assertIn("value: 4.0", prefab.read_text())

    def test_author_rain_owns_its_recipe_and_preserves_the_donor(self):
        relative = "Assets/Installed/Water"
        base, _ = fluid_mixer.install(self.project, relative)
        donor = base / "Presets/FreeJet.asset"
        original = donor.read_bytes()
        def rain(settings):
            return compose(self.project, relative, "Assets/GameWater", "NarrowRain", "rain", settings)
        path, status = rain({"count": 12, "span": 1.4})
        self.assertEqual(status, "composed")
        recipe = path.with_suffix(".asset")
        self.assertIn("_dropCount: 12", recipe.read_text())
        self.assertIn("_dropSpan: 1.4", recipe.read_text())
        self.assertIn(fluid_mixer.meta_guid(Path(str(recipe) + ".meta")), path.read_bytes())
        self.assertEqual(rain({"span": 1.4, "count": 12}), (path, "already composed"))
        with self.assertRaisesRegex(ValueError, "Invalid count"): rain({"count": 3.5})
        with self.assertRaisesRegex(ValueError, "duration"): rain({"duration": .5})
        self.assertEqual(donor.read_bytes(), original)

    def test_conflict_in_embedded_package_and_path_escape(self):
        packages=self.project/"Packages/some.other.package"
        packages.mkdir(parents=True)
        (packages/"Other.cs.meta").write_bytes((fluid_mixer.TEMPLATE/"FluidMixController.cs.meta").read_bytes())
        with self.assertRaisesRegex(ValueError,"GUID"):
            fluid_mixer.install(self.project)
        with self.assertRaises(ValueError):
            fluid_mixer.install(self.project,"Assets/../settings")
        self.assertFalse((self.project/fluid_mixer.DEFAULT).exists())

    def test_concurrent_different_destinations_only_install_once(self):
        def launch(destination):
            return subprocess.run([sys.executable,"-B",str(SCRIPTS/"fluid_mixer.py"),"--project",str(self.project),
                                   "--destination",destination],capture_output=True,
                                  creationflags=subprocess.CREATE_NO_WINDOW if sys.platform=="win32" else 0)
        with ThreadPoolExecutor(max_workers=2) as pool:
            results=list(pool.map(launch,("Assets/WaterA","Assets/WaterB")))
        self.assertEqual(sorted(r.returncode for r in results),[0,1])
        self.assertIn(b"GUID",next(r.stderr for r in results if r.returncode))
        self.assertEqual(len(list((self.project/"Assets").rglob("WaterFluidMixer.prefab"))),1)
