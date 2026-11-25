"""Run WebDriverAgent in the background or read/set its measured iPhone Duo Simulator hinge."""
import argparse
from contextlib import suppress
import json
import math
from pathlib import Path
import plistlib
import signal
import socket
import subprocess
import time
from urllib.error import HTTPError
from urllib.request import Request, urlopen


def request(port, route, body=None, timeout=15):
    data = None if body is None else json.dumps(body).encode()
    req = Request(f"http://127.0.0.1:{port}{route}", data=data, headers={"Content-Type": "application/json"})
    attempts = 2 if body is None else 1
    for attempt in range(attempts):
        try:
            with urlopen(req, timeout=timeout) as reply:
                return json.load(reply)["value"]
        except HTTPError as exc:
            raise RuntimeError(f"{route} returned {exc.code}: {exc.read().decode(errors='replace')}") from exc
        except OSError:
            if attempt + 1 == attempts:
                raise
            time.sleep(1)


def owned_work(value):
    folder = Path(value).resolve()
    if not any(p.parent.name == "Cache" and p.name.startswith("temp-") for p in (folder, *folder.parents)):
        raise ValueError("Use a directory inside your task's Cache/temp-* folder.")
    folder.mkdir(parents=True, exist_ok=True)
    return folder


def device_name(udid):
    listing = json.loads(subprocess.check_output(["xcrun", "simctl", "list", "devices", "-j"], timeout=60))
    devices = [d for group in listing["devices"].values() for d in group]
    device = next((d for d in devices if d["udid"] == udid), None)
    if not device or device["deviceTypeIdentifier"] != "com.apple.CoreSimulator.SimDeviceType.iPhone-Duo" or device["state"] != "Booted":
        raise ValueError("Supply the UDID of the already booted, task-owned Duo in the default simulator set.")
    return device["name"]


def identify(port, name, udid):
    # Simulator processes share host PIDs. Check that this loopback listener is the XCTest app inside this device.
    sockets = subprocess.check_output(["/usr/sbin/lsof", "-wnP", f"-iTCP:{port}", "-sTCP:LISTEN", "-Fpcn"],
                                      text=True, timeout=12).splitlines()
    pids = [field[1:] for field in sockets if field.startswith("p")]
    if (len(pids) != 1 or "cWebDriverAgentRunner-Runner" not in sockets or
            [field for field in sockets if field.startswith("n")] != [f"n127.0.0.1:{port}"]):
        raise RuntimeError(f"Expected a dedicated WebDriverAgent loopback listener on {port}.")
    executable = Path(subprocess.check_output(["/bin/ps", "-p", pids[0], "-o", "comm="],
                                              text=True, timeout=8).strip())
    containers = Path.home() / "Library/Developer/CoreSimulator/Devices" / udid / "data/Containers/Bundle/Application"
    if (executable.parent.parent.parent == containers and
            executable.parts[-2:] == ("WebDriverAgentRunner-Runner.app", "WebDriverAgentRunner-Runner")):
        return
    # A relocated device set can lack the standard host path. Consult its own launch service in that case.
    jobs = subprocess.check_output(["xcrun", "simctl", "spawn", udid, "launchctl", "list"],
                                   text=True, timeout=20).splitlines()
    for line in jobs:
        fields = line.split(None, 2)
        if len(fields) == 3 and fields[0] == pids[0] and fields[2].startswith(
                "UIKitApplication:com.facebook.WebDriverAgentRunner.xctrunner["):
            return
    raise RuntimeError(f"The loopback WebDriverAgent runner does not belong to {name} ({udid}).")


def read_angle(port):
    angle = request(port, "/wda/device/hingeAngle")
    if type(angle) not in (float, int) or not math.isfinite(angle) or not 0 <= angle <= 180:
        raise RuntimeError(f"Invalid native hinge measurement: {angle}")
    return angle


def measure(port, target, result):
    result["beforeDegrees"] = read_angle(port)
    if target is None:
        return
    if not math.isfinite(target) or not 0 <= target <= 180:
        raise ValueError("A hinge target must be between 0 and 180 degrees.")
    result["requestedDegrees"] = target
    samples = result["samplesDegrees"] = []
    # The destination can apply a pose before the client receives the POST response under boot load.
    # Read the physical simulator sensor in either case and never replay an ambiguous mutation.
    try:
        request(port, "/wda/device/hingeAngle", {"angle": target}, timeout=75)
    except OSError as exc:
        result["commandTransportError"] = f"{type(exc).__name__}: {exc}"
    for _ in range(12):
        time.sleep(1)
        samples.append(read_angle(port))
        recent = samples[-3:]
        if target in (0, 180):
            settled = abs(samples[-1] - target) < 3
        else:
            settled = (len(recent) == 3 and max(recent) - min(recent) < 2 and
                       0 < samples[-1] < 180 and abs(samples[-1] - target) <= 15)
        if settled:
            result["measuredDegrees"] = samples[-1]
            return
    raise RuntimeError(f"Hinge did not settle at {target} degrees: {samples}")


def claim_receipt(work, label):
    if not label or not all(c.isalnum() or c in "-_" for c in label):
        raise ValueError("Use letters, numbers, hyphens and underscores for the receipt label.")
    return (work / (label + ".json")).open("x", encoding="utf-8")


def pose(args):
    work = owned_work(args.work)
    name = device_name(args.udid)
    result = {"udid": args.udid, "deviceName": name}
    with claim_receipt(work, args.label) as out:
        try:
            identify(args.port, name, args.udid)
            measure(args.port, args.angle, result)
            result["passed"] = True
        except Exception as exc:
            result.update(passed=False, error=str(exc))
            raise
        finally:
            json.dump(result, out, indent=2)
            print(json.dumps(result), flush=True)


def launch_config(source, work, port, log):
    package = json.loads((source / "package.json").read_text(encoding="utf-8"))
    if package.get("name") != "appium-webdriveragent" or package.get("version") != "16.14.2":
        raise ValueError("This recipe is verified with appium-webdriveragent 16.14.2.")
    # Keep __TESTROOT__ resolution intact by writing the local configuration beside the generated one.
    build = work / "wda-build"
    subprocess.run(["xcodebuild", "build-for-testing", "-project", str(source / "WebDriverAgent.xcodeproj"),
                    "-scheme", "WebDriverAgentRunner", "-configuration", "Debug", "-destination",
                    "generic/platform=iOS Simulator", "-derivedDataPath", str(build), "-jobs", "2",
                    "ARCHS=arm64", "ONLY_ACTIVE_ARCH=YES", "-quiet"], stdout=log, stderr=subprocess.STDOUT,
                   check=True, timeout=900)
    product = build / "Build/Products"
    configs = list(product.glob("WebDriverAgentRunner_*.xctestrun"))
    if len(configs) != 1:
        raise RuntimeError(f"Expected one built WebDriverAgentRunner configuration, got {configs}")
    configuration = plistlib.loads(configs[0].read_bytes())
    target = configuration["WebDriverAgentRunner"]
    # The HTTP service binds first. Reusing its port disables WDA's unused wildcard screenshot listener.
    target.setdefault("EnvironmentVariables", {}).update(USE_IP="127.0.0.1", USE_PORT=str(port), MJPEG_SERVER_PORT=str(port))
    local_config = product / "local-duo.xctestrun"
    local_config.write_bytes(plistlib.dumps(configuration))
    return local_config


def serve(args):
    work = owned_work(args.work)
    name = device_name(args.udid)
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", args.port))
    with (work / "wda-build.log").open("w", encoding="utf-8") as log:
        config = launch_config(Path(args.wda).resolve(), work, args.port, log)
    with (work / "wda-run.log").open("w", encoding="utf-8") as log:
        runner = subprocess.Popen(["xcodebuild", "test-without-building", "-xctestrun", str(config), "-destination",
                                   "platform=iOS Simulator,id=" + args.udid, "-parallel-testing-enabled", "NO",
                                   "-resultBundlePath", str(work / f"wda-{time.time_ns()}.xcresult")],
                                  stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
        original = None
        try:
            deadline = time.monotonic() + 240
            while time.monotonic() < deadline:
                if runner.poll() is not None:
                    raise RuntimeError(f"WebDriverAgent exited {runner.returncode}. Read wda-run.log.")
                try:
                    status = request(args.port, "/status")
                except (OSError, RuntimeError):
                    time.sleep(1)
                    continue
                if status.get("ready"):
                    identify(args.port, name, args.udid)
                    original = read_angle(args.port)
                    print(json.dumps({"ready": True, "udid": args.udid, "port": args.port,
                                      "initialDegrees": original, "version": status["build"]["version"]}), flush=True)
                    break
            else:
                raise TimeoutError("WebDriverAgent failed to become ready. Read wda-run.log.")
            if runner.wait():
                raise RuntimeError("WebDriverAgent stopped unexpectedly. Read wda-run.log.")
        finally:
            # This runner owns only the xcodebuild child it created and the explicitly named simulator.
            try:
                if original is not None and runner.poll() is None:
                    restoration = {"udid": args.udid}
                    try:
                        identify(args.port, name, args.udid)
                        measure(args.port, original, restoration)
                        restoration["passed"] = True
                    except Exception as exc:
                        restoration.update(passed=False, error=str(exc))
                        raise
                    finally:
                        (work / "restoration.json").write_text(json.dumps(restoration, indent=2), encoding="utf-8")
            finally:
                if runner.poll() is None:
                    runner.send_signal(signal.SIGINT)
                    try:
                        runner.wait(timeout=30)
                    except subprocess.TimeoutExpired:
                        runner.kill()
                        runner.wait()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="operation", required=True)
    server = sub.add_parser("serve", help="Build and host the pinned WDA runner on the selected, already booted test Duo.")
    transfer = sub.add_parser("pose", help="Read the hinge, or set a target and capture its measured angle.")
    for command in (server, transfer):
        command.add_argument("--work", required=True)
        command.add_argument("--udid", required=True)
        command.add_argument("--port", type=int, default=18711)
    server.add_argument("--wda", required=True, help="Path to the unpacked npm appium-webdriveragent 16.14.2 package.")
    transfer.add_argument("--label", required=True, help="Unique name of the JSON receipt in --work.")
    transfer.add_argument("--angle", type=float, help="Degrees, with closed at 0 and fully open at 180. Omit for read-only.")
    args = parser.parse_args()
    if args.operation == "serve":
        def stop(_signal, _frame):
            raise KeyboardInterrupt()
        signal.signal(signal.SIGTERM, stop)
        with suppress(KeyboardInterrupt):
            serve(args)
    else:
        pose(args)


if __name__ == "__main__":
    main()
