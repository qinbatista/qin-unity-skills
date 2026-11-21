import json
import os
import plistlib
import sqlite3
import subprocess
import tempfile
import unittest
from contextlib import closing
from pathlib import Path
from zipfile import ZipFile

from evidence_cases import checked_nunit, grant_pending_order, hidden_process_options, ipa_identity, pair_motion, reference_frames, spine_bone_inventory


class EvidenceCases(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(dir=os.environ["CHARGE_PULSE_EVIDENCE"])
        self.addCleanup(temporary.cleanup)
        self.folder = Path(temporary.name)

    def test_testing_02_report_requires_the_right_case_and_a_pass(self):
        report = self.folder / "results.xml"
        report.write_text('<test-run result="Passed"><test-case name="Replay" result="Passed"/></test-run>', encoding="utf-8")
        self.assertEqual(checked_nunit(report, {"Replay"}), {"Replay": "Passed"})
        with self.assertRaises(ValueError):
            checked_nunit(report, {"MissingCase"})
        report.write_text('<test-run result="Failed"><test-case name="Replay" result="Failed"/></test-run>', encoding="utf-8")
        with self.assertRaises(ValueError):
            checked_nunit(report, {"Replay"})

    def test_testing_03_package_readback_selects_the_top_level_app(self):
        file = self.folder / "example.ipa"
        with ZipFile(file, "w") as archive:
            archive.writestr("Payload/Case.app/Info.plist", plistlib.dumps({
                "CFBundleIdentifier": "example.qin.charge-pulse", "CFBundleDisplayName": "Charge Pulse"
            }))
            archive.writestr("Payload/Case.app/PlugIns/Widget.appex/Info.plist", plistlib.dumps({
                "CFBundleIdentifier": "example.qin.widget"
            }))
        identity = ipa_identity(file, "example.qin.charge-pulse", "Charge Pulse")
        self.assertEqual(identity["name"], "Charge Pulse")
        self.assertEqual(len(identity["sha256"]), 64)
        with self.assertRaises(ValueError):
            ipa_identity(file, "example.wrong", "Charge Pulse")
        with self.assertRaises(ValueError):
            ipa_identity(file, "example.qin.charge-pulse", "Wrong name")

    def test_code_13_failed_confirmation_retries_without_another_reward(self):
        ledger = self.folder / "rewards.sqlite3"
        attempts = []

        def confirm(order):
            with closing(sqlite3.connect(ledger)) as database:
                self.assertEqual(database.execute("SELECT SUM(amount) FROM grant_log").fetchone(), (10,))
            attempts.append(order)
            if len(attempts) == 1:
                raise OSError("The local test adapter lost its first acknowledgement.")

        with self.assertRaises(OSError):
            grant_pending_order(ledger, "fixture-order", 10, confirm)
        grant_pending_order(ledger, "fixture-order", 10, confirm)
        self.assertEqual(attempts, ["fixture-order", "fixture-order"])
        with self.assertRaises(ValueError):
            grant_pending_order(ledger, "fixture-order", 12, confirm)

    def test_animation_07_gif_clock_and_horizontal_pair_are_decoded(self):
        for index, colors in enumerate(("255 0 0 0 0 0", "0 0 0 0 255 0", "0 0 255 0 0 0")):
            (self.folder / f"frame-{index}.ppm").write_text(f"P3\n2 1\n255\n{colors}\n", encoding="ascii")
        gif = self.folder / "reference.gif"
        video = self.folder / "fixture.mp4"
        for output, flags in ((gif, ["-loop", "0"]), (video, ["-c:v", "libx264", "-pix_fmt", "yuv420p", "-vf", "scale=4:2"])):
            subprocess.run(
                ["ffmpeg", "-nostdin", "-v", "error", "-framerate", "10", "-i", str(self.folder / "frame-%d.ppm"),
                 *flags, str(output)], capture_output=True, timeout=30, check=True, **hidden_process_options()
            )
        frames, last_hold = reference_frames(gif)
        self.assertEqual(frames, [0.0, 0.1, 0.2])
        self.assertAlmostEqual(last_hold, 0.1)
        paired, _ = pair_motion(gif, video, self.folder / "paired.mp4")
        self.assertEqual(len(paired), 3)

    def test_animation_08_spine_inventory_preserves_parent_and_flags_unmapped_channels(self):
        source = self.folder / "rig.json"
        source.write_text(json.dumps({
            "skeleton": {"spine": "4.1.24"},
            "bones": [{"name": "root"}, {"name": "hip", "parent": "root"},
                      {"name": "wrist", "parent": "hip", "x": 4, "rotation": 14}],
            "slots": [{"name": "hand", "bone": "wrist", "attachment": "palm"}],
            "ik": [{"name": "reach", "bones": ["wrist"], "target": "root"}]
        }), encoding="utf-8")
        inventory = spine_bone_inventory(source, 0.25)
        self.assertEqual(inventory["bones"][-1]["target_path"], "root/hip/wrist")
        self.assertEqual(inventory["bones"][-1]["position_unity_units"], [1.0, 0.0])
        self.assertEqual(inventory["remaining_channels"], ["ik", "slots"])
