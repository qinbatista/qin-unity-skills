# Drive the Duo Simulator hinge in the background

Use a task-owned iPhone Duo in the default CoreSimulator set. Boot it through `xcrun simctl boot <udid>`. This recipe was verified on an Apple silicon Mac with Xcode 27.2, its compatible iOS 27.2 Duo simulator runtime, Python 3, and the unpacked npm package `appium-webdriveragent@16.14.2`. The verified Duo runtime was build 24B91016s. Use `xcrun simctl list runtimes -j` to find the runtime whose supported device types include `com.apple.CoreSimulator.SimDeviceType.iPhone-Duo`. Pass its full runtime path to `simctl create` if two installed runtimes share a name. Keep the package, build, logs, and receipts inside the task Cache.

Apple exposes a first-party XCTest hinge setter starting with Xcode 27.3 beta. The published WebDriverAgent simulator command also works on Xcode 27.2. The helper builds that upstream package and talks to its loopback HTTP endpoint. [Apple release notes](https://developer.apple.com/documentation/xcode-release-notes/xcode-27_3-release-notes) · [Appium iPhone Duo guide](https://appium.github.io/appium-xcuitest-driver/latest/guides/duo/)

Start the server as a background child of the replay process and keep its PID. Give the service a free loopback port. Supply an empty directory under `Cache/temp-*/` for its first run. The server checks that the listener's host PID belongs to the XCTest runner inside the selected Simulator UDID. It prints a JSON readiness record after reading that device's hinge sensor.

```text
python3 -B examples/charge-pulse/inspection/iphone-replay/duo_hinge.py serve --work "<task-cache>/hinge" --udid "<owned-duo-udid>" --wda "<task-cache>/package" --port 18711
```

After the Player or geometry observer reaches its stage, issue one command per requested pose. Use a new label for each receipt. An invocation without `--angle` only reads the current sensor. The angle is measured in degrees. The partially open simulation can settle a few degrees away from the requested target, so the helper records three stable readings for intermediate poses. Fully closed and fully open use a tighter endpoint tolerance.

```text
python3 -B examples/charge-pulse/inspection/iphone-replay/duo_hinge.py pose --work "<task-cache>/hinge" --udid "<owned-duo-udid>" --port 18711 --label outer --angle 0
python3 -B examples/charge-pulse/inspection/iphone-replay/duo_hinge.py pose --work "<task-cache>/hinge" --udid "<owned-duo-udid>" --port 18711 --label partial --angle 90
python3 -B examples/charge-pulse/inspection/iphone-replay/duo_hinge.py pose --work "<task-cache>/hinge" --udid "<owned-duo-udid>" --port 18711 --label inner --angle 180
```

Keep the Player running across each transfer. Read its window, native safe area, orientation, and route state after the change. Use `xcrun simctl io <udid> enumerate` to locate the active display and `xcrun simctl io <udid> screenshot --display=<display-id> <task-cache>/frame.png` to inspect the rendered frame. A hinge angle, a framebuffer port, or new dimensions alone cannot establish that the mounted screen followed the transition.

When finished, signal only this server process with SIGTERM or interrupt it. Its cleanup restores the observed initial angle while the service still runs, records the restoration, and stops the owned XCTest process. The device owner then uninstalls the test bundles and releases the disposable simulator after consuming its receipts.
