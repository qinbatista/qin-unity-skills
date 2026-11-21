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

Five English-language fixtures mount their own UXML screens and apply the same geometry policy to the live tree as the viewport changes. All use a 4K design baseline with quarter-resolution USS units. The Canvas dialog uses the same content and an independent native layout for comparison.

[View the Unity-rendered portrait and landscape comparison](ui-layout-preview.jpg).

| Layout | Portrait | Landscape | Native check |
| --- | --- | --- | --- |
| [Expedition HUD](Assets/Editor/Tests/ResponsiveLayouts/Hud.uxml) | Map and Pack remain at the top while the title clears the center cutout. | The header follows the clear side margins in each rotation. | Obstruction clearance, equal top offsets, projected size and one response per scripted action. |
| [Inventory](Assets/Editor/Tests/ResponsiveLayouts/Inventory.uxml) | Two columns, three rows. | Three columns, two rows. | A single [item template](Assets/Editor/Tests/ResponsiveLayouts/Item.uxml) creates six instances that survive resize. Icon and label slots stay aligned and bounded. |
| [Settings](Assets/Editor/Tests/ResponsiveLayouts/Settings.uxml) | Display above Audio. | Display and Audio side by side. | Peer frame alignment, caption and value containment, and enough room for the longest supplied label. |
| [Confirmation](Assets/Editor/Tests/ResponsiveLayouts/Dialog.uxml) | Centered dialog with both actions on one line. | Same dialog and actions centered in the usable region. | [Canvas](Assets/Editor/Tests/ResponsiveLayouts/CanvasConfirmation.cs) and Toolkit compare projected bounds and return the same choice through native handlers. |
| [Trail journal](Assets/Editor/Tests/ResponsiveLayouts/Quests.uxml) | Tall list between header and footer. | Wider, shorter scrolling region. | The final item scrolls into the content viewport and the header and footer stay fixed. |

[ResponsiveLayoutCases.cs](Assets/Editor/Tests/ResponsiveLayouts/ResponsiveLayoutCases.cs) runs each layout at the 2160 × 3840 and 3840 × 2160 reference sizes, at 540 × 1170 and 1080 × 2340 with synthetic center cutouts, and at 1170 × 540 with synthetic left and right obstructions. The iPhone Duo outer display (1398 × 2034) and inner display (1878 × 2670) each replay in portrait and landscape using their rectangular resolutions. Device insets, physical folding and touch need their own device run. Input here uses scripted events in Unity Play Mode. [Apple iPhone Duo display specifications](https://www.apple.com/iphone-duo/specs/)

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
