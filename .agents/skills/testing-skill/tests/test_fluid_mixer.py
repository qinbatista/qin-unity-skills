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
        self.assertEqual(len(list((path/"Presets").glob("*.asset"))),7)
        self.assertEqual(fluid_mixer.install(self.project,"Assets/Owner/Water")[1],"already installed")
        with self.assertRaisesRegex(ValueError,"GUID"):
            fluid_mixer.install(self.project,"Assets/Second")
        pellet = path/"Presets/FallingMass.asset"
        pellet.write_text(pellet.read_text().replace("_gravity: 8","_gravity: 4"))
        with self.assertRaisesRegex(ValueError,"preserved"):
            fluid_mixer.install(self.project,"Assets/Owner/Water")
        self.assertIn("_gravity: 4",pellet.read_text())

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
