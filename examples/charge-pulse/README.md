# Charge Pulse

A Unity example that traces one button action through the six Skills. Open this project folder in Unity 6000.6.7f1, open [ChargePulse.unity](Assets/Scenes/ChargePulse.unity), enter Play Mode, and press **Charge**. The center gathers, the ring expands with a burst of sparks, and the effect returns to its original pose. The button becomes available again after the effect has recycled.

The [case-by-case code review](CASEBOOK.md) links the runnable Skill cases, negative controls, findings and integration examples. It includes the native and host test commands.

| Skill | Example | Decision demonstrated |
| --- | --- | --- |
| [Unity Code](../../unity-code-skill/SKILL.md) | [ChargePulseController.cs](Assets/Scripts/ChargePulse/ChargePulseController.cs) | The controller admits one request at a time. Button callbacks and authored animation events advance the state and publish changes to the HUD. |
| [Unity Assets](../../unity-assets-skill/SKILL.md) | [ChargePulse.prefab](Assets/Prefabs/ChargePulse/ChargePulse.prefab) | The scene instantiates a saved Prefab. The effect owns its clips, materials and UI assets. Unity saves the serialized bindings and tracks their identities through the accompanying `.meta` files. |
| [Unity UI](../../unity-ui-skill/SKILL.md) | [UXML](Assets/Prefabs/ChargePulse/ChargePulseHud.uxml), [USS](Assets/Prefabs/ChargePulse/ChargePulseHud.uss), [HUD controller](Assets/Scripts/ChargePulse/ChargePulseHudController.cs) | UI Toolkit owns layout and input. The 3840 × 2160 design scales uniformly through PanelSettings. State text and input availability follow the controller. |
| [Unity Animation](../../unity-animation-skill/SKILL.md) | [Pulse.anim](Assets/Prefabs/ChargePulse/Pulse.anim), [ChargePulse.controller](Assets/Prefabs/ChargePulse/ChargePulse.controller) | A one-shot clip controls ring and core scale independently. Timed events advance the pulse and return to the idle pose. |
| [Unity Effects](../../unity-effects-skill/SKILL.md) | [PulseGlow.shader](Assets/Prefabs/ChargePulse/PulseGlow.shader), [Particle System authoring](Assets/Editor/ChargePulseSampleAuthoring.cs) | The shader supplies an outer rim and core glow with separate materials. The controller starts a bounded spark burst at release and clears it at retirement. |
| [Unity Testing](../../unity-testing-skill/SKILL.md) | [ChargePulseTests.cs](Assets/Editor/Tests/ChargePulseTests.cs) | One Editor case checks the saved asset bindings and curve timing. A second case enters Play Mode, submits the UI Toolkit button, reads rendered pixels, and checks duplicate admission, cancellation and replay. |

## Run the focused tests

Use a task-owned directory for the XML and Editor log. The runtime case uses the built-in render pipeline and needs a graphics device. It exercises scripted UI Toolkit navigation input and checks full-viewport layouts at 3840 × 2160, 1280 × 720 and 1080 × 1440.

```text
unity test examples/charge-pulse --mode EditMode --filter ChargePulseTests --output <task-cache>/results.xml -- -logFile <task-cache>/unity.log
```

Set `CHARGE_PULSE_EVIDENCE` to an allocated task directory to capture native HUD and effect PNGs. The test assembly and authoring command live under `Assets/Editor`. The authoring command recreates the sample scene and refreshes the existing effect assets in place.

```text
unity run examples/charge-pulse --log-file <task-cache>/authoring.log -- -executeMethod Qin.UnitySamples.ChargePulseSampleAuthoring.Rebuild
```
