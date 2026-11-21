# Unity Skill code review

Review each row by opening the named method or asset. The native cases run in the small Charge Pulse project. Host cases exercise real parsers, SQLite and media tools. Integration examples require the stated receiving package and runtime.

## Project structure

The [scene](Assets/Scenes/ChargePulse.unity) instantiates the saved [effect Prefab](Assets/Prefabs/ChargePulse/ChargePulse.prefab). The [pulse controller](Assets/Scripts/ChargePulse/ChargePulseController.cs) owns request admission and runtime state. The [HUD controller](Assets/Scripts/ChargePulse/ChargePulseHudController.cs) binds the button and reflects that state. The [Animator](Assets/Prefabs/ChargePulse/ChargePulse.controller) supplies authored timing and the [native test](Assets/Editor/Tests/ChargePulseTests.cs) exercises the saved bindings and input path.

```text
Assets/
  Scenes/ChargePulse.unity       Sample scene and Prefab instance
  Prefabs/ChargePulse/          Feature Prefab, clips, UI, shaders and materials
  Scripts/ChargePulse/          Runtime controllers and Runtime assembly
  Editor/Tests/                 Edit Mode and Play transition checks
inspection/                    Host evidence and its Python tests
integrations/                  Addressables and VFX Graph receiver examples
```

Use the receiving project's existing layout for production work. For a standalone feature, the example shows how to keep runtime, serialized assets and Editor tools with their actual owners. [Gameplay structure](../../unity-code-skill/references/unity-game-code-structure-design.md) · [Asset placement](../../unity-assets-skill/SKILL.md#place-by-owner) · [Native test commands](#run-the-cases)

## Unity Code

Use the [Skill](../../unity-code-skill/SKILL.md), [C# style](../../unity-code-skill/references/unity-csharp-rules.md), [lifecycle](../../unity-code-skill/references/unity-lifecycle-and-serialization.md), [structure](../../unity-code-skill/references/unity-game-code-structure-design.md), [resource lifetime](../../unity-code-skill/references/runtime-resources-and-gc.md), [allocation patterns](../../unity-code-skill/references/allocation-and-ownership-patterns.md) and [service integration](../../unity-code-skill/references/unity-service-integration.md).

| Case | Code to inspect | Review and evidence |
| --- | --- | --- |
| C01 Explicit type and result ownership <a id="c01"></a> | [CSharpRuleGuardTests](../../unity-code-skill/tests/test_csharp_rule_guard.py) | Run the checker on obvious `var`, a discarded Task and a tuple discard. Named await and a switch wildcard are legal alternatives. |
| C02 Empty wrapper, recursion and call layout <a id="c02"></a> | [CSharpRuleGuardTests](../../unity-code-skill/tests/test_csharp_rule_guard.py) | The checker flags unchanged forwarding, obvious self-recursion and avoidable vertical calls. Its good cases retain a real boundary, argument transformation and a legitimate overload. |
| C03 Input, state and callbacks <a id="c03"></a> | [StartPulse and ApplyAnimationStage](Assets/Scripts/ChargePulse/ChargePulseController.cs), [HUD binding](Assets/Scripts/ChargePulse/ChargePulseHudController.cs) | Button admission, ordered Animation Events and a change notification use one state owner. Play Mode rejects a repeated request and an out-of-order event. |
| C04 Continuous frame work <a id="c04"></a> | [ScaleTo and Update](Assets/Scripts/ChargePulse/TimedRevealController.cs), [Code03](Assets/Editor/Tests/CadenceCases.cs) | Actual scale samples move between endpoints. A settled request keeps the callback disabled and the Transform's change flag clear through a frame. |
| C05 Fixed-step physics and late presentation <a id="c05"></a> | [FixedUpdate and LateUpdate](Assets/Scripts/ChargePulse/StepMotionController.cs), [Code09](Assets/Editor/Tests/CadenceCases.cs) | Native physics changes the body and the late marker reads its result. A zero request stops work. A stable pose skips the repeated write. |
| C06 Deadline and cancellation <a id="c06"></a> | [Ui03 scheduled work](Assets/Editor/Tests/UiCases.cs) | A mounted Toolkit scheduler executes the due callback once. Pausing the other scheduled item keeps its callback silent. |
| C07 Job and slot ownership <a id="c07"></a> | [Code04](Assets/Editor/Tests/CodeCases.cs) | One native record contains position and speed. One scheduled Job uses the same slots for both rounds. Read only after `Complete` and dispose once at the outer lifetime. |
| C08 Managed allocation <a id="c08"></a> | [Filter and Code05](Assets/Editor/Tests/CodeCases.cs) | Confirm a quiet idle control and a deliberate allocation with the same recorder. Compare profiler samples and equal results for 64 matched calls with reused capacity and transient allocation. |
| C09 Pool capacity and material ownership <a id="c09"></a> | [Code06](Assets/Editor/Tests/CodeCases.cs) | One owned material is reused. Exact release restores tint and leaves the shared material intact. An active-instance limit rejects the second simultaneous borrow. |
| C10 Async lifecycle <a id="c10"></a> | [Code07](Assets/Editor/Tests/CodeCases.cs) | Complete an older request after cancellation and after the new request publishes. The old request disposes its native array. The latest value remains visible. |
| C11 C# content escaping <a id="c11"></a> | [Code08](Assets/Editor/Tests/CodeCases.cs) | A quote and newline round-trip through Unity's JSON representation. Preserve the actual payload when embedding it in a prompt. |
| C12 Addressables and late arrivals <a id="c12"></a> | [AddressableViewController](integrations/AddressableViewController.cs) | A current instance can become active. A canceled or failed acquisition releases its exact handle. Use the receiver's Addressables package and an initially inactive prefab registered at its key. |
| C13 Durable provider side effect <a id="c13"></a> | [grant_pending_order](inspection/evidence_cases.py), [retry case](inspection/test_evidence_cases.py) | The local diagnostic callback reads committed SQLite state. A failed confirmation retries without a second reward and a conflicting amount is rejected. |
| C14 Authored and runtime data <a id="c14"></a> | [scene authoring](Assets/Editor/ChargePulseSampleAuthoring.cs), [Assets05](Assets/Editor/Tests/AssetCases.cs) | Serialized references are assigned by the asset owner. Private runtime state stays in the controller. Unity restores a real old prefab field name. |

The input, animation and HUD examples use direct calls and a real event subscriber. A factory, command queue, singleton or optional SDK facade needs the real variants, deferred consumer, shared lifecycle or providers that justify it. The generic pattern and SDK rules describe these selection criteria. The sample exercises the selected local flow and diagnostic transaction. Live authentication, SDK transfer, a provider callback and remote-environment selection depend on the receiver.

## Unity Assets

Use the [Skill](../../unity-assets-skill/SKILL.md) and [texture intake](../../unity-assets-skill/references/texture-intake.md).

| Case | Code to inspect | Review and evidence |
| --- | --- | --- |
| A01 Prefab and scene ownership <a id="a01"></a> | [Rebuild](Assets/Editor/ChargePulseSampleAuthoring.cs), [saved bindings](Assets/Editor/Tests/ChargePulseTests.cs) | Save feature assets with the Prefab, reopen a scene and check the real instance, Animator, particle and UI references. |
| A02 File move and serialized consumers <a id="a02"></a> | [Assets01](Assets/Editor/Tests/AssetCases.cs) | Move an actual imported Sprite and reopen the Prefab. Both GUID and subasset file ID survive. Query the new native dependency path. |
| A03 Dynamic loading <a id="a03"></a> | [Assets02](Assets/Editor/Tests/AssetCases.cs), [addressable consumer](integrations/AddressableViewController.cs) | A `Resources` load by the old key fails even with the same GUID. The new key loads the real TextAsset. The separate Addressables example shows the instance consumer. |
| A04 Texture geometry <a id="a04"></a> | [Assets03](Assets/Editor/Tests/AssetCases.cs) | A PNG kept byte-identical imports as an 8 × 4 Sprite. Changing PPU changes both world axes while retaining rect and pivot. |
| A05 Data texture and alpha <a id="a05"></a> | [Assets03 mask import](Assets/Editor/Tests/AssetCases.cs) | Import the aligned mask as linear data and read both transparent and opaque RGBA samples back from Unity. Compare the source and imported geometry independently. |
| A06 Native assembly placement <a id="a06"></a> | [Editor test assembly](Assets/Editor/Tests/ChargePulse.EditorTests.asmdef), [Assets04](Assets/Editor/Tests/AssetCases.cs) | Query the assemblies selected for Player. The runtime is included and this Editor fixture assembly is excluded. |
| A07 Serialized name migration <a id="a07"></a> | [FormerlySerializedAs](Assets/Scripts/ChargePulse/StepMotionController.cs), [Assets05](Assets/Editor/Tests/AssetCases.cs) | Reimport a prefab containing the former field name. The same child Transform binds to the current field. |

The fixture moves and importer changes run on isolated assets. A production removal needs its actual Addressables, build, native, tooling and test consumers. Layout rules for a linked game come from `explain unity-project`.

## Unity UI

Use the [Skill](../../unity-ui-skill/SKILL.md), [artwork intake](../../unity-ui-skill/references/artwork-intake.md), [layout and input](../../unity-ui-skill/references/layout-and-input.md) and [screen adaptation](../../unity-ui-skill/references/screen-adaptation.md).

| Case | Code to inspect | Review and evidence |
| --- | --- | --- |
| U01 Native host and semantic tree <a id="u01"></a> | [UXML](Assets/Prefabs/ChargePulse/ReviewHud.uxml), [USS](Assets/Prefabs/ChargePulse/ReviewHud.uss), [controller](Assets/Scripts/ChargePulse/ReviewScreenController.cs) | The real host keeps the row, list and overlay distinct. Bind actions once on enable and remove the exact handlers on disable. |
| U02 Geometry and hit area <a id="u02"></a> | [Panel settings](Assets/Prefabs/ChargePulse/ChargePulsePanel.asset), [native viewport checks](Assets/Editor/Tests/ChargePulseTests.cs) | 3840 × 2160, 1280 × 720 and 1080 × 1440 keep a uniform scale and an in-bounds button. See Unity's [Expand mode](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/uielements/panelscreenmatchmode/expand). |
| U03 Insets and localization <a id="u03"></a> | [ApplyInsets and SetLanguage](Assets/Scripts/ChargePulse/ReviewScreenController.cs), [Ui03](Assets/Editor/Tests/UiCases.cs) | Convert synthetic display insets once after the panel layout. Open the German version at portrait size. The heading wraps and the hit region stays aligned. |
| U04 Destination, modal input and recovery <a id="u04"></a> | [OpenDetails and CloseDetails](Assets/Scripts/ChargePulse/ReviewScreenController.cs), [Ui03](Assets/Editor/Tests/UiCases.cs) | A native navigation event opens the details and focuses Back. While open the parent controls are disabled. Returning restores focus and actual scroll offset. |
| U05 Existing Canvas and sliced art <a id="u05"></a> | [Ui04](Assets/Editor/Tests/UiCases.cs) | A generated Sprite separates fixed caps from its stretchable center. The uGUI control stays anchored across a resize, renders and consumes a dispatched pointer click once. Its disabled state rejects the same click. |

This original sample uses semantic UXML and USS and generated test pixels. Atlas versions, source cropping, platform cutouts, right-to-left text and physical touch depend on the receiving design and environment. These inputs call for their own saved assets, layout and input cases.

## Unity Animation

Use the [Skill](../../unity-animation-skill/SKILL.md), [Animator](../../unity-animation-skill/references/playback-and-animator.md), [motion curves](../../unity-animation-skill/references/motion-curves.md), [reference motion](../../unity-animation-skill/references/reference-motion-review.md), [rig transfer](../../unity-animation-skill/references/rig-transfer.md) and [Spine contract](../../unity-animation-skill/references/spine-to-unity-contract.md).

| Case | Code to inspect | Review and evidence |
| --- | --- | --- |
| M01 One-shot and exact action timing <a id="m01"></a> | [sampling keys and events](Assets/Editor/ChargePulseSampleAuthoring.cs), [saved clip](Assets/Prefabs/ChargePulse/Pulse.anim) | Read the independent ring and core curves, non-looping clip and six events. A native action reaches release and returns to idle. |
| M02 Cancellation and stale event <a id="m02"></a> | [ApplyAnimationStage](Assets/Scripts/ChargePulse/ChargePulseController.cs), [native pulse case](Assets/Editor/Tests/ChargePulseTests.cs) | Named state transitions reject an out-of-order event. Disable clears particles. Re-enable restores the idle pose and the next trigger succeeds. |
| M03 Loop continuity <a id="m03"></a> | [Animation03](Assets/Editor/Tests/AnimationCases.cs) | A real child Transform is sampled at the start, an interior key and the closing time. Endpoint tangents match and an early sample differs from linear motion. |
| M04 One-dimensional Blend Tree <a id="m04"></a> | [Animation04 Simple1D](Assets/Editor/Tests/AnimationCases.cs) | Reopen a controller and check the threshold, clip and float parameter. |
| M05 Two-dimensional Blend Tree <a id="m05"></a> | [Animation04 SimpleDirectional2D](Assets/Editor/Tests/AnimationCases.cs) | Reopen the tree and check the direction and supporting parameter. |
| M06 Direct Blend Tree, Any State and overrides <a id="m06"></a> | [Animation04 Direct](Assets/Editor/Tests/AnimationCases.cs) | Confirm independent weights and explicit trigger/transition configuration. Change an override while the base clip remains bound. |
| M07 Native blended pose <a id="m07"></a> | [Animation05](Assets/Editor/Tests/AnimationCases.cs) | Evaluate the actual animation mixer at both ends and one intermediate weight. |
| M08 Two-bone native skin <a id="m08"></a> | [Animation06](Assets/Editor/Tests/AnimationCases.cs) | Bake the rest pose and a displaced pose. Fully and partially weighted vertices move by the correct amount and the authored mesh stays intact. |
| M09 GIF timestamps and paired playback <a id="m09"></a> | [reference_frames and pair_motion](inspection/evidence_cases.py), [media case](inspection/test_evidence_cases.py) | Decode an original fixture GIF's three timed frames. Align clocks, render side by side and decode the resulting video. The second input in this case is a host fixture. |
| M10 Spine inventory and parity boundary <a id="m10"></a> | [spine_bone_inventory](inspection/evidence_cases.py), [rig input](inspection/test_evidence_cases.py) | Check parent, target path, source-to-target units and preserved rotation. Report attachment and constraint channels still requiring conversion. |

The native clip and mesh cases cover direct Unity sampling and the existing pulse playback. A bound character GIF or full Spine replacement also needs the receiver's rig, attachments, authoring tools, runtime and matching visual evidence.

## Unity Effects

Use the [Skill](../../unity-effects-skill/SKILL.md), [staged effects](../../unity-effects-skill/references/staged-effects.md) and [rendering and verification](../../unity-effects-skill/references/rendering-and-verification.md).

| Case | Code to inspect | Review and evidence |
| --- | --- | --- |
| E01 Reference and dependency ownership <a id="e01"></a> | [saved materials and Prefab](Assets/Prefabs/ChargePulse/ChargePulse.prefab), [authoring](Assets/Editor/ChargePulseSampleAuthoring.cs) | The original sample owns three native materials and their shaders. For a linked reuse request, resolve the exact `effect-id` in YoFaAssets and review the actual license, pipeline and dependency set. |
| E02 Particle setup and capacity <a id="e02"></a> | [CreatePulse](Assets/Editor/ChargePulseSampleAuthoring.cs) | A Particle System batches the sparks in local space. Count, velocity, sorting, lifetime and stop are explicit. Check live emission and zero leftover particles in Play Mode. |
| E03 Seven phases and replay <a id="e03"></a> | [PulseStage and phase handling](Assets/Scripts/ChargePulse/ChargePulseController.cs), [native pulse case](Assets/Editor/Tests/ChargePulseTests.cs) | Natural playback visits all seven named stages. Ring, core and sparks have separate visible jobs. A C# test confirms native pixels, disable and replay. |
| E04 Independent transparent coverage <a id="e04"></a> | [shader](Assets/Prefabs/ChargePulse/PulseComposite.shader), [HLSL calculation](Assets/Prefabs/ChargePulse/PulseComposite.hlsl), [Effects04](Assets/Editor/Tests/EffectCases.cs) | Full red RGB under zero body alpha contributes nothing. Zero, half and full filament coverage produce distinct native RGBA samples. Check actual shader error state after the pass. |
| E05 Shape progression <a id="e05"></a> | [sweep mask](Assets/Prefabs/ChargePulse/PulseComposite.shader), [Effects05](Assets/Editor/Tests/EffectCases.cs) | Compare early, middle and late arc coverage. Formed pixels retain full intensity, which distinguishes the growing visible area from an overall fade. |
| E06 Retraction from the current value <a id="e06"></a> | [ScaleTo](Assets/Scripts/ChargePulse/TimedRevealController.cs), [Effects06](Assets/Editor/Tests/CadenceCases.cs) | Reverse a partially formed shape with continuity, then observe zero scale and an idle callback owner. The main effect separately tests interruption by disabling. |
| E07 Shader Graph function binding <a id="e07"></a> | [PulseComposite_float](Assets/Prefabs/ChargePulse/PulseComposite.hlsl) | Use File-mode Custom Function, name `PulseComposite`, Float precision, two Vector4 inputs `Body` and `Line` and one Vector4 output `Color`. The built-in pass calls this same function. Verify the Graph material's blend and native RGB/alpha when consuming it. [Unity Custom Function](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.0/manual/Custom-Function-Node.html) |
| E08 VFX Graph command binding <a id="e08"></a> | [GraphEffectController](integrations/GraphEffectController.cs) | The binding compiles against Unity's VFX engine API. Bind a graph with a neutral initial event and one `Burst` spawn event using the `BurstStrength` float. Dispatch the real request, reset for replay and disable at retirement. [Unity Visual Effect API](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/ComponentAPI.html) |
| E09 Material reuse and resource release <a id="e09"></a> | [Code06](Assets/Editor/Tests/CodeCases.cs), [Effects07](Assets/Editor/Tests/EffectCases.cs), [Play Mode capture](Assets/Editor/Tests/ChargePulseTests.cs) | Keep shared material values intact. Prove the exact CPU image retires immediately in Edit Mode and at the frame boundary in Play Mode. The caller retains the borrowed render target. |

The tested graphical path is macOS Metal and the built-in render pipeline. The Graph and Addressables code have receiving-package prerequisites. Device budgets, source eligibility, target pipeline and complete linked-effect acceptance call for the real assets and target tests.

## Unity Testing and repository Skills

Use the [Unity Testing Skill](../../unity-testing-skill/SKILL.md), [runtime evidence](../../unity-testing-skill/references/runtime-and-artifact-evidence.md), [repository testing Skill](../../.agents/skills/testing-skill/SKILL.md) and [project AI index](../../skills/ai-index/SKILL.md).

| Case | Code to inspect | Review and evidence |
| --- | --- | --- |
| T01 Scope to the actual consumer <a id="t01"></a> | [ChargePulseTests](Assets/Editor/Tests/ChargePulseTests.cs), [test assembly](Assets/Editor/Tests/ChargePulse.EditorTests.asmdef) | Edit Mode checks saved binds. A Play transition checks actual input, frames and teardown. Keep these observations distinguishable. |
| T02 Report integrity <a id="t02"></a> | [checked_nunit](inspection/evidence_cases.py) | Require the named case and a passing run. A missing case and failed report are rejected. Bind the report path to the current invocation and source. |
| T03 Artifact identity <a id="t03"></a> | [ipa_identity](inspection/evidence_cases.py), [fixture ZIP](inspection/test_evidence_cases.py) | Read the top-level plist, compare identifier and display name and hash the actual archive. The test uses a structured fixture archive. Device installation and store acceptance have their own evidence. |
| T04 Cold, warm and memory scope <a id="t04"></a> | [Code05 profiler](Assets/Editor/Tests/CodeCases.cs), [first and replayed action](Assets/Editor/Tests/ChargePulseTests.cs) | Calibrate the allocation recorder and bound its measured section. Observe the actual first and repeated trigger separately. Use a device run for device frame-time or memory claims. |
| T05 Asset, UI and media proof <a id="t05"></a> | [UiCases](Assets/Editor/Tests/UiCases.cs), [media readback](inspection/evidence_cases.py) | Verify rendered pixels, focus and real event delivery. Decode a moving artifact and check timestamps instead of treating a filename or still as motion evidence. |
| T06 Routing and release checks <a id="t06"></a> | [catalog tests](../../.agents/skills/testing-skill/tests/test_skill_catalog.py), [C# guard tests](../../unity-code-skill/tests/test_csharp_rule_guard.py) | Match the public Skill list, AI-index routes and owned package links. Read source and generated evidence at the relevant boundary. |
| T07 Scene transition, disposal and reentry <a id="t07"></a> | [SceneReentryReplacesTheEffectAndRetiresThePriorScene](Assets/Editor/Tests/ChargePulseTests.cs) | Load the saved scene twice through its native paths, start the effect, unload it, and check that its former runtime instance is destroyed before the next load. |

For device acceptance, use the authorized physical target, compatible emulator or simulator, or the configured compatible host. Bind the actual package, profile, provider and version for each result. The sample executes no device, package build or live store transaction.

## Findings from this review

| Before | After | Rule audit | Native verification |
| --- | --- | --- | --- |
| Expected the full Sprite bounds to have zero depth. | Measure the two world axes that define the footprint. | Implementation missed the existing [texture rule](../../unity-assets-skill/references/texture-intake.md). | `Assets03` compares X and Y before and after PPU changes. The former Z assertion fails on native bounds. [AssetCases](Assets/Editor/Tests/AssetCases.cs) |
| The allocation API returned zero in both conditions. | Require idle and deliberate-allocation controls on the same recorder before measuring. | The selected probe was wrong. The existing calibration advice needed an explicit acceptance gate. [Allocation gate](../../unity-code-skill/references/runtime-resources-and-gc.md#measure-a-recurring-path) | `Code05` requires both controls and equal outputs on the matched workloads. A silent positive control fails the case. [CodeCases](Assets/Editor/Tests/CodeCases.cs) |
| Position and speed used separate arrays for the same Job slots. | Store both values in one native record. | Implementation missed the existing [one-record rule](../../unity-code-skill/references/unity-game-code-structure-design.md#data). | `Code04` verifies two completed schedules and the one compiled native slot container. [CodeCases](Assets/Editor/Tests/CodeCases.cs) |
| The Shader used `line` as a local name. | Use `filament` and run the actual shader pass. | Implementation used a reserved name. The existing [backend check](../../unity-effects-skill/references/rendering-and-verification.md#observe-the-actual-render-path-and-lifetime) caught it. | `Effects04` checks Metal diagnostics and zero, half and full coverage pixels. [EffectCases](Assets/Editor/Tests/EffectCases.cs) |
| The readback always called `Destroy`. | Retire its owned texture using the destruction API for that runtime. | The portable lifetime guidance needed an explicit mode distinction. [Release rule](../../unity-code-skill/references/runtime-resources-and-gc.md#own-acquisition-and-release) | `Effects07` checks immediate retirement and a live borrowed target. The Play test checks deferred destruction and eventual retirement. [Edit Mode case](Assets/Editor/Tests/EffectCases.cs) · [Play Mode case](Assets/Editor/Tests/ChargePulseTests.cs) |
| The German details view retained English explanatory text. | Localize the detail heading, body and Back action together. | Implementation missed the existing [destination and locale checks](../../unity-ui-skill/references/layout-and-input.md#localized-content). | `Ui03` enters the actual German details page and asserts all three strings. Inspect the rendered destination. [UiCases](Assets/Editor/Tests/UiCases.cs) |
| A settled request still enabled the procedural callback. | Reject the settled work before enabling it. The equality guard skips the identical Transform assignment. | The implementation missed the intent. The frame rule needed a request-time admission check. [Frame rule](../../unity-code-skill/references/unity-lifecycle-and-serialization.md#frame-callbacks) | `Code03` proves interpolation and completion, then checks the disabled callback and clear change flag after a settled request. Review the equality guard for the setter call itself. [CadenceCases](Assets/Editor/Tests/CadenceCases.cs) · [ScaleTo](Assets/Scripts/ChargePulse/TimedRevealController.cs) |

## Run the cases

Run the native suite from the repository root against a task-owned copy of the Unity project, with its original Assets, Packages and ProjectSettings. Follow the [daily Unity CLI gate](../../unity-code-skill/references/unity-cli.md). Use `py -3` in place of `python3` in Windows PowerShell.

```text
python3 -B unity-code-skill/scripts/unity_cli_daily.py test "<task-project>" --non-interactive --no-pager --mode EditMode --filter Qin.UnitySamples.Tests --output "<task-cache>/results.xml" -- -logFile "<task-cache>/unity.log"
```

Keep the graphics device enabled. Set `CHARGE_PULSE_EVIDENCE` to an existing task-owned directory for PNGs and the host test scratch. Run the host suite from the repository root with Python 3 and FFmpeg/FFprobe available. Use `py -3` on Windows.

```text
python3 -B -m unittest discover -s examples/charge-pulse/inspection
```
