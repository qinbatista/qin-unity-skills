# Charge Pulse

A Unity 6000.6.7f1 example that traces one button action through the six Skills. The focused native test loads [ChargePulse.unity](Assets/Scenes/ChargePulse.unity) and invokes **Charge** in batch mode. The center gathers, the ring expands with a burst of sparks, and the effect returns to its original pose. The button becomes available again after the effect has recycled.

The [case-by-case code review](CASEBOOK.md) links the runnable Skill cases, negative controls, findings and integration examples. It includes the native and host test commands.

| Skill | Example | Decision demonstrated |
| --- | --- | --- |
| [Unity Code](../../unity-code-skill/SKILL.md) | [ChargePulseController.cs](Assets/Scripts/ChargePulse/ChargePulseController.cs) | The controller admits one request at a time. Button callbacks and authored animation events advance the state and publish changes to the HUD. |
| [Unity Assets](../../unity-assets-skill/SKILL.md) | [ChargePulse.prefab](Assets/Prefabs/ChargePulse/ChargePulse.prefab) | The scene instantiates a saved Prefab. The effect owns its clips, materials and UI assets. Unity saves the serialized bindings and tracks their identities through the accompanying `.meta` files. |
| [Unity UI](../../unity-ui-skill/SKILL.md) | [UXML](Assets/Prefabs/ChargePulse/ChargePulseHud.uxml), [USS](Assets/Prefabs/ChargePulse/ChargePulseHud.uss), [HUD controller](Assets/Scripts/ChargePulse/ChargePulseHudController.cs) | UI Toolkit owns layout and input. The 3840 × 2160 design scales uniformly through PanelSettings. State text and input availability follow the controller. |
| [Unity Animation](../../unity-animation-skill/SKILL.md) | [Pulse.anim](Assets/Prefabs/ChargePulse/Pulse.anim), [ChargePulse.controller](Assets/Prefabs/ChargePulse/ChargePulse.controller) | A one-shot clip controls ring and core scale independently. Timed events advance the pulse and return to the idle pose. |
| [Unity Effects](../../unity-effects-skill/SKILL.md) | [PulseGlow.shader](Assets/Prefabs/ChargePulse/PulseGlow.shader), [Particle System authoring](Assets/Editor/ChargePulseSampleAuthoring.cs) | The shader supplies an outer rim and core glow with separate materials. The controller starts a bounded spark burst at release and clears it at retirement. |
| [Unity Testing](../../unity-testing-skill/SKILL.md) | [ChargePulseTests.cs](Assets/Editor/Tests/ChargePulseTests.cs) | One Editor case checks the saved asset bindings and curve timing. A second case enters Play Mode, submits the UI Toolkit button, reads rendered pixels, and checks duplicate admission, cancellation and replay. |

## Responsive UI cases

Five fixtures mount their own UXML screens and apply the same geometry policy to the live tree as the viewport changes. All use a 4K design baseline with quarter-resolution USS units. The Canvas dialog uses the same content and an independent native layout for comparison.

The [fixture string table](Assets/Editor/Tests/ResponsiveLayouts/LayoutStrings.json) contains the player-facing copy in English (`en-US`) and German (`de-DE`). [LayoutText](Assets/Editor/Tests/ResponsiveLayouts/LayoutText.cs) updates existing UI Toolkit elements from locale keys and typed arguments. The Canvas dialog reads the same resources and returns stable action IDs. The sample table is Editor test data. Production screens use their game's localization service.

[View the Unity-rendered English portrait and German landscape comparison](ui-layout-preview.jpg).

| Layout | Portrait | Landscape | Native check |
| --- | --- | --- | --- |
| [Expedition HUD](Assets/Editor/Tests/ResponsiveLayouts/Hud.uxml) | Map and Pack remain at the top while the title clears the center cutout. | The header follows the clear side margins in each rotation. | Obstruction clearance, equal top offsets, projected size and one response per scripted action. |
| [Inventory](Assets/Editor/Tests/ResponsiveLayouts/Inventory.uxml) | Two columns, three rows. | Three columns, two rows. | A single [item template](Assets/Editor/Tests/ResponsiveLayouts/Item.uxml) creates six instances that survive resize. Icon and label slots stay aligned and bounded. |
| [Settings](Assets/Editor/Tests/ResponsiveLayouts/Settings.uxml) | Display above Audio. | Display and Audio side by side. | Peer frame alignment, caption and value containment, and enough room for the longest supplied label. |
| [Confirmation](Assets/Editor/Tests/ResponsiveLayouts/Dialog.uxml) | Centered dialog with both actions on one line. | Same dialog and actions centered in the usable region. | [Canvas](Assets/Editor/Tests/ResponsiveLayouts/CanvasConfirmation.cs) and Toolkit compare projected bounds and return the same choice through native handlers. |
| [Trail journal](Assets/Editor/Tests/ResponsiveLayouts/Quests.uxml) | Tall list between header and footer. | Wider, shorter scrolling region. | The final item scrolls into the content viewport and the header and footer stay fixed. |

[ResponsiveLayoutCases.cs](Assets/Editor/Tests/ResponsiveLayouts/ResponsiveLayoutCases.cs) runs each layout at the 2160 × 3840 and 3840 × 2160 reference sizes, at 540 × 1170 and 1080 × 2340 with synthetic center cutouts, and at 1170 × 540 with synthetic left and right obstructions. The iPhone Duo outer display (1398 × 2034) and inner display (1878 × 2670) each replay in portrait and landscape using their rectangular resolutions. Device insets, physical folding and touch need their own device run. Input here uses scripted events in Unity Play Mode. [Apple iPhone Duo display specifications](https://www.apple.com/iphone-duo/specs/)

Each viewport runs all locales in the table on the same mounted tree. The tests check text fit, the journal's scroll position during language changes, native focus, and Canvas dialog parity. Capture file names include the locale.

Run the class from a task-owned snapshot, with a graphics device and an optional `CHARGE_PULSE_EVIDENCE` directory for rendered frames.

```text
python3 -B unity-code-skill/scripts/unity_cli_daily.py test "<task-project>" --non-interactive --no-pager --mode EditMode --filter ResponsiveLayoutCases --output "<task-cache>/ui-layout-results.xml" -- -logFile "<task-cache>/ui-layout.log"
```

## Run the focused tests

Follow the [daily Unity CLI gate](../../unity-code-skill/references/unity-cli.md). Run the commands from the repository root with a task-owned project copy and log directory. Use `py -3` in place of `python3` in Windows PowerShell. The runtime case uses the built-in render pipeline and needs a graphics device. It exercises scripted UI Toolkit navigation input and checks full-viewport layouts at 3840 × 2160, 1280 × 720 and 1080 × 1440.

```text
python3 -B unity-code-skill/scripts/unity_cli_daily.py test "<task-project>" --non-interactive --no-pager --mode EditMode --filter ChargePulseTests --output "<task-cache>/results.xml" -- -logFile "<task-cache>/unity.log"
```

Set `CHARGE_PULSE_EVIDENCE` to an allocated task directory to capture native HUD and effect PNGs. The test assembly and authoring command live under `Assets/Editor`. The authoring command recreates the sample scene and refreshes the existing effect assets in place.

```text
python3 -B unity-code-skill/scripts/unity_cli_daily.py run "<task-project>" --non-interactive --no-pager --log-file "<task-cache>/authoring.log" -- -executeMethod Qin.UnitySamples.ChargePulseSampleAuthoring.Rebuild
```

## Run a macOS package smoke test

Use a fresh registered run under the repository's `Cache/temp-*` for each build. Copy this sample's saved `Assets`, `Packages`, and `ProjectSettings` into `<run>/charge-pulse` and verify their bytes against the source. Keep `UPM_CACHE_ROOT` and build outputs in the run. Set `TMP`, `TEMP`, and `TMPDIR` to its verified short native temporary path when Bee needs one. Follow the [test packaging lifecycle](../../unity-testing-skill/references/runtime-and-artifact-evidence.md#isolated-test-packaging).

Run the following from the repository root on macOS after binding those paths. The [sample preference profile](inspection/macos-packaging-preferences.sb) limits only the test processes. It blocks writes to the user's preferences and the sample company's shared application data while preserving the package identity.

```text
sandbox-exec -f examples/charge-pulse/inspection/macos-packaging-preferences.sb \
  -D "USER_PREFS=$HOME/Library/Preferences" \
  -D "COMPANY_DATA=$HOME/Library/Application Support/DefaultCompany" \
  python3 -B unity-code-skill/scripts/unity_cli_daily.py build "<run>/charge-pulse" \
  --target StandaloneOSX --output "<run>/ChargePulse.app" \
  --log-file "<run>/unity-build.log" --no-tail --no-accelerator --timeout 900 \
  --non-interactive --no-pager --format json
```

Read the bundle identifier and executable from `Contents/Info.plist`, verify the bundle with `codesign --verify --deep --strict`, and confirm the native architecture and first packed scene. Launch that executable under the same preference profile with `-batchmode -logFile <run>/player.log`. Give the Player a run-owned `CFFIXED_USER_HOME` and set `__CFPREFERENCES_AVOID_DAEMON=1` for that invocation. Check that the log reaches engine initialization and first-scene loading and that the process stays alive without crash or exception evidence. This is a headless startup check.

Stop the owned Player, then verify that the `com.DefaultCompany.charge-pulse` and `unity.DefaultCompany.charge-pulse` preference domains outside the run are unchanged. Run Workflow's exact cleanup checkpoint and confirm that the directory and registration are gone before starting the next build.
