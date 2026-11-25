"""Compare regenerated Unity scenes with the original filmed water library and rebuild its reel."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

from fluid_preview import hidden_process_options


BASELINE = Path(__file__).resolve().parents[1] / "assets/water-library-baseline.json"
RAIN = "WideCloudburst"


def file_digest(path):
    with path.open("rb") as stream:
        result = hashlib.sha256()
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(block)
    return result.digest()


def tree_digest(root):
    digest = hashlib.sha256()
    for path in sorted(p for p in root.rglob("*") if p.is_file()):
        digest.update(path.relative_to(root).as_posix().encode("utf-8"))
        digest.update(b"\0")
        digest.update(file_digest(path))
    return digest.hexdigest()


def ordered_frames(folder, count):
    files = sorted(folder.glob("*.png"))
    if [p.name for p in files] != [f"{i:04d}.png" for i in range(count)]:
        raise ValueError(f"Incomplete or shifted camera clock at {folder}")
    return files


def match(actual, expected, label):
    if actual != expected:
        raise ValueError(f"{label}: expected {expected}, read {actual}")
    return actual


def verify_lanes(work, gold):
    report = json.loads((work / "FluidVerification.json").read_text(encoding="utf-8"))
    for key in ("seed", "receiverPaths", "graphics"):
        match(report[key], gold[key], key)
    match(set(report["subjectVideos"]), set(gold["subjects"]), "Filmed subjects")
    match(report["families"], {c["id"]: c["cases"] for c in gold["chapters"]}, "Chapter order")
    for lane in ("A", "B"):
        tests = ET.parse(work / lane / "tests.xml").findall(".//test-case")
        match(len(tests), gold["testsPerReceiver"], f"{lane} native test count")
        if any(test.get("result") != "Passed" for test in tests):
            raise ValueError(f"{lane} native tests failed")
    return report


def verify_case(work, name, expected):
    roots = {lane: work / lane / "captures" / name for lane in ("A", "B")}
    if roots["A"].samefile(roots["B"]):
        raise ValueError(f"{name} needs two independent receiver trees")
    for lane, root in roots.items():
        source = root / ("frames" if (root / "frames").is_dir() else "normal")
        ordered_frames(source, expected["frames"])
        match(tree_digest(root), expected["nativeTreeSha256"], f"{lane}/{name} native captures and controls")
    movie = work / "Subjects" / (name + ".mp4")
    match(file_digest(movie).hex(), expected["fullRateMovieSha256"], f"{name} 30 fps movie")
    return {"case": name, "frames": expected["frames"], "matchedNativeReceivers": ["A", "B"],
            "nativeTreeSha256": expected["nativeTreeSha256"], "movieSha256": expected["fullRateMovieSha256"]}


def verify_cloudburst(work, gold):
    expected = gold["subjects"][RAIN]["frames"]
    hashes = {}
    for mode, signature in gold["cloudburstModes"].items():
        folder = work / "captures" / RAIN / mode
        frames = ordered_frames(folder, expected)
        digest = hashlib.sha256()
        for path in frames:
            digest.update(file_digest(path))
        hashes[mode] = match(digest.hexdigest(), signature, f"Cloudburst {mode} camera")
    return hashes


def caption_reel(source, destination, gold, font_path, scratch):
    from PIL import Image, ImageDraw, ImageFont

    fonts = {size: ImageFont.truetype(str(font_path), size) for size in (16, 18, 26)}
    overlays, filters = [], []
    clock = 0
    for i, chapter in enumerate(gold["chapters"]):
        cases = chapter["cases"]
        counts = {gold["subjects"][name]["frames"] for name in cases}
        if len(counts) != 1:
            raise ValueError(f"Inconsistent chapter clock for {chapter['id']}")
        frames = counts.pop()
        overlay = Image.new("RGBA", (1152, 542))
        draw = ImageDraw.Draw(overlay)
        background = "#0b1820"
        draw.rectangle([0, 0, 1151, 49], fill=background)
        draw.text((12, 7), chapter["label"], font=fonts[26], fill="#e6f6fb")
        draw.text((930, 14), "Unity 原速实录 · 30 帧/秒", font=fonts[16], fill="#a9c3cd")
        for k, name in enumerate(cases):
            x = k % 3 * 384
            y = 50 + (123 if len(cases) <= 3 else 0) + k // 3 * 246 + 216
            draw.rectangle([x, y, x + 383, y + 29], fill=background)
            draw.text((x + 8, y + 3), gold["subjects"][name]["label"], font=fonts[18], fill="#e6f6fb")
        path = scratch / (chapter["id"] + ".png")
        overlay.save(path)
        overlays.extend(["-i", str(path)])
        previous = "[0:v]" if i == 0 else f"[v{i - 1}]"
        filters.append(f"{previous}[{i + 1}:v]overlay=0:0:enable='between(n,{clock},{clock + frames - 1})'[v{i}]")
        clock += frames
    subprocess.run(["ffmpeg", "-v", "error", "-nostdin", "-i", str(source), *overlays,
                    "-filter_complex_threads", "2", "-filter_complex", ";".join(filters), "-map", f"[v{len(filters)-1}]",
                    "-an", "-c:v", "libx264", "-crf", "18", "-threads", "3", "-pix_fmt", "yuv420p",
                    "-movflags", "+faststart", str(destination)], check=True, timeout=180, **hidden_process_options())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", required=True, type=Path, help="Completed replay_fluids --case all --edition original --video")
    parser.add_argument("--output", required=True, type=Path, help="New evidence directory inside your task cache")
    parser.add_argument("--cloudburst", type=Path, help="Also check all three focused replay_cloudburst camera takes")
    parser.add_argument("--caption-font", type=Path, help="Rebuild both approved reels with the original Hiragino Sans GB TTC")
    args = parser.parse_args()
    gold = json.loads(BASELINE.read_text(encoding="utf-8"))
    report = verify_lanes(args.work, gold)
    args.output.mkdir(parents=True, exist_ok=False)
    with tempfile.TemporaryDirectory(prefix="encode-", dir=args.output) as temp:
        scratch = Path(temp)
        def check(item):
            return verify_case(args.work, *item)

        with ThreadPoolExecutor(max_workers=2) as pool:
            cases = list(pool.map(check, gold["subjects"].items()))
        result = {"baselineVersion": gold["version"], "graphics": gold["graphics"], "seed": gold["seed"],
                  "fps": gold["fps"], "subjects": cases, "framesPerReceiver": sum(c["frames"] for c in cases)}
        if "loadedNamedPrefabsPerReceiver" in report:
            result["namedPrefabsPerReceiver"] = report["loadedNamedPrefabsPerReceiver"]
        if args.cloudburst:
            result["cloudburstModes"] = verify_cloudburst(args.cloudburst, gold)
        if args.caption_font:
            english = args.output / "WaterMatrix.mp4"
            chinese = args.output / "WaterMatrix-CN.mp4"
            shutil.copyfile(args.work / "WaterMatrix.mp4", english)
            match(file_digest(english).hex(), gold["reel"]["englishSha256"], "Original English reel")
            caption_reel(english, chinese, gold, args.caption_font, scratch)
            match(file_digest(chinese).hex(), gold["reel"]["chineseSha256"], "Original Chinese reel")
            result["reels"] = gold["reel"]
    receipt = args.output / "WaterLibraryVerification.json"
    receipt.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(f"Verified {len(cases)} subjects, {result['framesPerReceiver']} frames per receiver. {receipt}")


if __name__ == "__main__":
    main()
