"""Host-side code for the sample's artifact, reference, and provider cases."""

import hashlib
import json
import plistlib
import sqlite3
import subprocess
import sys
import xml.etree.ElementTree as ET
from contextlib import closing
from pathlib import Path, PurePosixPath
from zipfile import ZipFile


sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "unity-code-skill" / "scripts"))
from hidden_process import hidden_process_options


def checked_nunit(report, required_tests):
    root = ET.parse(report).getroot()
    cases = {case.attrib["name"]: case.get("result") for case in root.findall(".//test-case")}
    if root.get("result") != "Passed" or not required_tests or any(
        cases.get(name) != "Passed" for name in required_tests
    ):
        raise ValueError("The submitted report does not pass all required cases.")
    return cases


def ipa_identity(artifact, expected_identifier, expected_name):
    with ZipFile(artifact) as archive:
        apps = [
            name for name in archive.namelist()
            if len(PurePosixPath(name).parts) == 3
            and PurePosixPath(name).match("Payload/*.app/Info.plist")
        ]
        if len(apps) != 1:
            raise ValueError("Select an artifact with one top-level application plist.")
        info = plistlib.loads(archive.read(apps[0]))
    if info.get("CFBundleIdentifier") != expected_identifier:
        raise ValueError("The artifact's bundle identifier differs from the approved input.")
    display_name = info.get("CFBundleDisplayName", info.get("CFBundleName"))
    if display_name != expected_name:
        raise ValueError("The artifact's display name differs from the approved input.")
    digest = hashlib.sha256()
    with Path(artifact).open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return {"bundle_id": info["CFBundleIdentifier"], "name": display_name, "sha256": digest.hexdigest()}


def grant_pending_order(ledger, order_id, amount, confirm):
    """Local diagnostic adapter. The provider callback may fail after the ledger commits."""
    if not order_id or amount < 1:
        raise ValueError("The pending order must include an ID and a positive reward.")
    with closing(sqlite3.connect(ledger)) as database:
        with database:
            database.execute("CREATE TABLE IF NOT EXISTS grant_log (order_id TEXT PRIMARY KEY, amount INTEGER NOT NULL)")
            database.execute(
                "INSERT INTO grant_log VALUES (?, ?) ON CONFLICT(order_id) DO NOTHING", (order_id, amount)
            )
            stored = database.execute("SELECT amount FROM grant_log WHERE order_id = ?", (order_id,)).fetchone()
            if stored != (amount,):
                raise ValueError("The order ID was already committed with a different amount.")
        persisted = database.execute("SELECT amount FROM grant_log WHERE order_id = ?", (order_id,)).fetchone()
        if persisted != (amount,):
            raise ValueError("The committed reward could not be read back.")
    confirm(order_id)


def reference_frames(path):
    response = subprocess.run(
        ["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_frames", "-show_entries",
         "frame=best_effort_timestamp_time,duration_time", "-of", "json", str(path)],
        text=True, capture_output=True, check=True, timeout=30, **hidden_process_options()
    )
    frames = json.loads(response.stdout)["frames"]
    timestamps = [float(frame["best_effort_timestamp_time"]) for frame in frames]
    if len(timestamps) < 2 or any(right <= left for left, right in zip(timestamps, timestamps[1:])):
        raise ValueError("Motion evidence requires a sequence with increasing timestamps.")
    return timestamps, float(frames[-1]["duration_time"])


def pair_motion(reference, receiving_capture, output):
    left, left_hold = reference_frames(reference)
    right, right_hold = reference_frames(receiving_capture)
    left_duration = left[-1] - left[0] + left_hold
    right_duration = right[-1] - right[0] + right_hold
    if abs(left_duration - right_duration) > max(left_hold, right_hold):
        raise ValueError("The inputs have different timings. Review the original captures.")
    graph = "[0:v]setpts=PTS-STARTPTS,scale=-2:240,setsar=1[a];[1:v]setpts=PTS-STARTPTS,scale=-2:240,setsar=1[b];[a][b]hstack=shortest=1"
    subprocess.run(
        ["ffmpeg", "-nostdin", "-v", "error", "-n", "-i", str(reference), "-i", str(receiving_capture),
         "-filter_complex", graph, "-an", "-c:v", "libx264", "-pix_fmt", "yuv420p", str(output)],
        capture_output=True, check=True, timeout=45, **hidden_process_options()
    )
    subprocess.run(
        ["ffmpeg", "-nostdin", "-v", "error", "-i", str(output), "-f", "null", "-"],
        capture_output=True, check=True, timeout=45, **hidden_process_options()
    )
    return reference_frames(output)


def spine_bone_inventory(source, units_per_source_unit):
    """Map the portable transform subset and surface attachments/constraints for review."""
    data = json.loads(Path(source).read_bytes())
    if units_per_source_unit <= 0:
        raise ValueError("Supply a positive conversion from the source to the receiver.")
    paths = {}
    mapping = []
    for bone in data["bones"]:
        name = bone["name"]
        parent = bone.get("parent")
        if name in paths or "/" in name or (parent is not None and parent not in paths):
            raise ValueError("Each bone needs a unique name and an established parent.")
        paths[name] = f"{paths[parent]}/{name}" if parent is not None else name
        compatible = bone.get("transform", "normal") == "normal"
        mapping.append({
            "source_id": name,
            "target_path": paths[name],
            "position_unity_units": [bone.get(axis, 0) * units_per_source_unit for axis in ("x", "y")],
            "rotation_degrees": bone.get("rotation", 0),
            "classification": "direct" if compatible else "unsupported",
        })
    unresolved = [category for category in ("ik", "transform", "path", "physics", "slots", "skins", "animations") if data.get(category)]
    return {"version": data["skeleton"]["spine"], "bones": mapping, "remaining_channels": unresolved}
