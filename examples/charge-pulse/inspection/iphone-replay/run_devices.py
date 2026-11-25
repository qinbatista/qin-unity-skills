"""Run the built inspection Player quietly on an owned CoreSimulator device."""
import argparse
import hashlib
import json
import plistlib
from pathlib import Path
import shutil
import subprocess
import sys
import time
from urllib.parse import urlsplit

import duo_hinge

MODELS = ("SE3", "17e", "Air", "17", "18-Pro", "18-Pro-Max", "Duo")
BUNDLE = "com.DefaultCompany.receiver"


def wait_for(path, timeout):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if path.exists():
            return
        time.sleep(.5)
    raise TimeoutError(str(path))


def command(args, *, check=True, timeout=150):
    return subprocess.run(args, text=True, capture_output=True, timeout=timeout, check=check)


def hinge(port, uid, name, angle, output):
    result = {"udid": uid, "deviceName": name}
    try:
        duo_hinge.identify(port, name, uid)
        duo_hinge.measure(port, angle, result)
        result["passed"] = True
        time.sleep(1)
    except Exception as error:
        result.update(passed=False, error=str(error))
        raise
    finally:
        output.write_text(json.dumps(result, indent=2))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", required=True, type=Path)
    parser.add_argument("--model", required=True, choices=MODELS)
    parser.add_argument("--result", help="Fresh directory name for this model run")
    parser.add_argument("--runtime", default="com.apple.CoreSimulator.SimRuntime.iOS-27-2",
                        help="CoreSimulator runtime identifier or path used when creating the private test phone")
    parser.add_argument("--duo-udid", help="Dedicated booted test Duo in the default simulator set")
    parser.add_argument("--waypoint-only", action="store_true", help="Run only the Waypoint action in each pose plus the retained transition")
    parser.add_argument("--resizable", action="store_true", help="Compare the same Simulator binary with discrete resizing disabled from iOS 27")
    parser.add_argument("--wda", help="Loopback WebDriverAgent URL on that same dedicated Duo")
    args = parser.parse_args()
    work = duo_hinge.owned_work(args.work)
    app = work / "build/Build/Products/Release-iphonesimulator/receiver.app"
    if not (app / "receiver").is_file() and not (app / "ProductName").is_file():
        info = plistlib.loads((app / "Info.plist").read_bytes())
        if not (app / info["CFBundleExecutable"]).is_file():
            raise RuntimeError("Incomplete native Player")
    if args.model == "Duo" and not (args.duo_udid and args.wda):
        parser.error("Duo requires a dedicated booted XCTest device and loopback WebDriverAgent")
    port = None
    if args.wda:
        service = urlsplit(args.wda)
        if service.scheme != "http" or service.hostname != "127.0.0.1" or not service.port or service.username or service.password or service.path not in ("", "/") or service.query or service.fragment:
            parser.error("Specify http://127.0.0.1:<port> for the dedicated Simulator")
        port = service.port
    label = args.result or args.model
    if not label or not all(c.isalnum() or c in "-_" for c in label):
        parser.error("The result name must contain letters, numbers, hyphens or underscores")
    result = work / "devices" / label
    result.mkdir(parents=True, exist_ok=False)
    if (work / "build-manifest.json").exists():
        shutil.copy2(work / "build-manifest.json", result)
    private = work / "private-set"
    private.mkdir(exist_ok=True)
    sim = ["xcrun", "simctl"]
    created = args.model != "Duo"
    if created:
        sim += ["--set", str(private)]
        device_type = "SE-3rd-generation" if args.model == "SE3" else args.model
        uid = command(sim + ["create", "UI Replay iPhone " + args.model,
                             "com.apple.CoreSimulator.SimDeviceType.iPhone-" + device_type, args.runtime]).stdout.strip()
    else:
        uid = args.duo_udid
        name = duo_hinge.device_name(uid)
        duo_hinge.identify(port, name, uid)
        hinge(port, uid, name, 0, result / "initial-closed.json")
    (result / "device.txt").write_text(uid + "\n")
    output = None
    installed = False
    def run(*values, **kwargs):
        return command(sim + list(values), **kwargs)
    try:
        if created:
            run("boot", uid)
            startup = run("bootstatus", uid, "-b", check=False, timeout=450)
            (result / "boot.txt").write_text(startup.stdout + startup.stderr)
            if startup.returncode:
                raise RuntimeError("Simulator boot failed")
        if run("get_app_container", uid, BUNDLE, check=False).returncode == 0:
            raise RuntimeError("Refusing to replace an existing app")
        install_app = app
        if args.resizable:
            install_app = result / "resize-probe.app"
            shutil.copytree(app, install_app)
            plist = install_app / "Info.plist"
            info = plistlib.loads(plist.read_bytes())
            info["UIRequiresFullScreenIgnoredStartingWithVersion"] = "27"
            plist.write_bytes(plistlib.dumps(info, fmt=plistlib.FMT_BINARY))
            (result / "resize-setting.txt").write_text("UIRequiresFullScreenIgnoredStartingWithVersion=27\n")
        try:
            run("install", uid, str(install_app))
        finally:
            if args.resizable:
                shutil.rmtree(install_app)
        installed = True
        installed_app = Path(run("get_app_container", uid, BUNDLE, "app").stdout.strip())
        installed_plist = (installed_app / "Info.plist").read_bytes()
        installed_info = plistlib.loads(installed_plist)
        if args.resizable and installed_info.get("UIRequiresFullScreenIgnoredStartingWithVersion") != "27":
            raise RuntimeError("The installed inspector did not receive the requested resize opt-in")
        receipt = {key: installed_info.get(key) for key in ("CFBundleIdentifier", "CFBundleExecutable", "DTSDKName",
                                                            "UIRequiresFullScreen", "UIRequiresFullScreenIgnoredStartingWithVersion")}
        receipt["infoPlistSHA256"] = hashlib.sha256(installed_plist).hexdigest()
        (result / "installed-app.json").write_text(json.dumps(receipt, indent=2))
        container = Path(run("get_app_container", uid, BUNDLE, "data").stdout.strip())
        output = container / "Documents/iphone-replay"
        if not created:
            (container / "Documents/duo-replay").touch()
        if args.waypoint_only:
            (container / "Documents/waypoint-only").touch()
            (result / "screens.txt").write_text("Waypoint\n")
        try:
            run("launch", uid, BUNDLE, timeout=300)
        except subprocess.TimeoutExpired:
            # CoreSimulator can still complete an accepted launch while the host is busy.
            wait_for(output, 90)
        wait_for(output, 180)
        if not created:
            wait_for(output / "ready-for-hinge", 330)
            for phase, angle in (("half-ready", 90), ("inner-ready", 180), ("outer-return-ready", 0), ("inner-again-ready", 180)):
                hinge(port, uid, name, angle, result / (phase + "-hinge.json"))
                (output / phase).touch()
                wait_for(output / ("retained-" + phase + ".txt"), 32)
                ports = run("io", uid, "enumerate")
                (result / (phase + "-ports.txt")).write_text(ports.stdout + ports.stderr)
                display = "1" if angle == 0 else "3"
                run("io", uid, "screenshot", "--display=" + display, str(result / (phase + "-os.png")))
                (output / (phase + ".photographed")).touch()
            wait_for(output / "ready-for-half-matrix", 240)
            hinge(port, uid, name, 90, result / "half-matrix-hinge.json")
            (output / "half-matrix").touch()
        wait_for(output / "complete", 360)
    finally:
        try:
            if installed:
                try:
                    run("terminate", uid, BUNDLE, check=False)
                    if output and output.is_dir():
                        shutil.copytree(output, result / "frames")
                finally:
                    run("uninstall", uid, BUNDLE)
                    if run("get_app_container", uid, BUNDLE, check=False).returncode == 0:
                        raise RuntimeError("The test app is still installed")
        finally:
            if created:
                try:
                    run("shutdown", uid, check=False)
                finally:
                    run("delete", uid)
            elif installed:
                hinge(port, uid, name, 0, result / "restored-closed.json")
    errors = [(p.name, json.loads(p.read_text()).get("error")) for p in (result / "frames").glob("*.json")]
    errors = [pair for pair in errors if pair[1]]
    runtime_errors = result / "frames/runtime-errors.txt"
    if runtime_errors.exists() and runtime_errors.stat().st_size:
        errors.append(("runtime-errors.txt", "Native Player recorded errors"))
    print(json.dumps({"model": args.model, "failures": errors}), flush=True)
    return int(bool(errors))


if __name__ == "__main__":
    sys.exit(main())
