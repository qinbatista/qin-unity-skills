# qin-unity-skills

One [Unity task menu](unity-code-skill/references/global-skill-routing.md#choose-by-task) routes to eight Skills. Start there or invoke `$unity-code-skill` for mixed or unfamiliar Unity work. A known domain can start at its own Skill. Each operation links to its rules and verification. The [section reader](unity-code-skill/references/global-skill-routing.md#read-one-section) can return just those headings. Add the [Codex Skill pack](https://github.com/qinbatista/qin-codex-skills) for general task execution and verification.

[Unity Tech Explain](unity-tech-explain-skill/SKILL.md) gives Unity questions a three-part answer with a short principle, code and usage at the owning file and method, and numbered steps with a diagram. It applies across domains, including explanations of a Skill itself. See the [ScriptableObject storage walkthrough](unity-tech-explain-skill/references/scriptableobject-storage.md) and [global Codex activation](unity-code-skill/references/global-skill-routing.md#require-routing-across-projects).

Every Unity task ends with a [runtime cost review](unity-performance-skill/SKILL.md#review-every-unity-task). For example, a coin can follow the player within pickup range while the CPU owns its reward. A burst of decorative sparks can share an effect emitter. Unity Performance explains how to choose those designs and test the busiest frame, overlapping events and teardown. The installed [optimization casebook](unity-performance-skill/references/performance-cases.md) includes a native replay command. Use it to select cases and add receiving-game regressions for costs uncovered during ordinary feature work. The [verified Unity replay](examples/performance-cases/Verification.json) records the Editor, packages, fixture hash and individual case outcomes.

For 2D water flow and water spells, [choose a water simulation](unity-effects-skill/references/fluid-mixer.md#choose-the-water-simulation). The [water matrix](unity-effects-skill/references/water-scenario-matrix.md) runs 37 native subjects across 34 composer profiles, including gates and reservoirs, overhead currents, interactive casts and input extremes. The [named template library](unity-effects-skill/references/water-template-library.md) authors each recorded subject and preserves both five-metre rain editions. The [matrix receipt](examples/fluid-mixer/ScenarioMatrix.json) binds the 37 movies to both native capture trees and records the authoring sweep. For a top-view moving vessel, [fit the solved surface to the bowl rim](unity-effects-skill/references/bowl-projection.md) and validate the dry exterior in the real gameplay camera. The [16 original recipes](unity-effects-skill/references/water-recipes-2d.md) include their inputs, contacts and game-camera steps. The [native runner](unity-effects-skill/references/fluid-mixer.md#reproduce-twice-from-the-installed-skill) exports each subject as a full-rate film. The [side-view replays](examples/fluid-mixer/SideView.json) cover the seven starting forms, a hit-triggered curtain and a continuously fed waterfall. [Author a channel prefab](unity-effects-skill/references/channel-water.md#author-another-water-body) for rivers, shoreline advance and return, or water sloshing inside a basin. [Generate rain, a ledge drip or a fountain from a parameter brief](unity-effects-skill/references/fluid-mixer.md#author-a-water-effect-from-parameters). The [generation receipt](examples/fluid-mixer/SideWaterGeneration.json) records the native physics, authored asset identities and films. The two native receivers verify depth-invariant framing and match the solver states across the renderer change.

For side-view rain over a riverbed, [replay the five-metre cloudburst](unity-effects-skill/references/wide-cloudburst.md). For rain seen from above, [stage a shower on a woodland pool](unity-effects-skill/references/rain-on-water.md). The [woodland film](unity-effects-skill/assets/rain-study-preview.mp4) and its [wave measurements](examples/fluid-mixer/RainStudy.json) follow an isolated first drop through a dense shower and settling water.

For phase-animated material studies, the [2D water, sky and spell catalog](unity-effects-skill/references/flow-sky-2d.md) pairs 24 editable surfaces with an XY prefab, an optional URP Shader Graph and a per-subject graphics replay. Watch the [chaptered film](unity-effects-skill/assets/flow-sky-2d.mp4) or use the [numbered frame index](examples/flow-sky-2d/Subjects.jpg) and [native results](examples/flow-sky-2d/Replay.json).

For particle effects, [design one VFX Graph](unity-effects-skill/references/graph-and-shader.md#design-one-vfx-graph) from its particle budget, forces, contacts and output. [Import five editable examples or watch their three independent creations](examples/particle-graphs/README.md). The rain graph includes falling drops and contact-triggered spray in one component.

## Install the global Skill pack

Paste this into an AI assistant with a local terminal.

```text
Install qin-unity-skills on this computer using the Python 3 installer at
https://raw.githubusercontent.com/qinbatista/qin-codex-skills/master/management-skill/scripts/install_skill_pack.py
Run its install qin-unity-skills action and verify the eight SKILL.md files
under ~/.agents/skills/qin-unity-skills/. Report the installed Skill names.
```

For manual setup, save [install_skill_pack.py](https://raw.githubusercontent.com/qinbatista/qin-codex-skills/master/management-skill/scripts/install_skill_pack.py) and run it from the download directory.

| Host | Command |
| --- | --- |
| macOS or Linux | `python3 -B install_skill_pack.py install qin-unity-skills` |
| Windows PowerShell | `py -3 -B install_skill_pack.py install qin-unity-skills` |

Check `~/.agents/skills/qin-unity-skills/<skill>/SKILL.md` and select `$unity-code-skill` in Codex. `install_skill_pack.py update qin-unity-skills` replaces the whole directory from the published package. `install_skill_pack.py uninstall qin-unity-skills` removes the pack. [Shared installer guide](https://github.com/qinbatista/qin-codex-skills/blob/master/management-skill/SKILL.md#install) · [Codex Skill discovery](https://learn.chatgpt.com/docs/build-skills#where-codex-loads-local-skills)

For automatic selection across projects, use the [installed Unity routing guide](unity-code-skill/references/global-skill-routing.md#require-routing-across-projects).

To extend the pack with another operation or domain, follow the [catalog maintenance guide](skills/ai-index/modules/catalog.md#extend-the-task-menu).

## Contribute changes

Use this shared Skill library as a starting point and choose the best approach for the task within your project's requirements. Everyone is welcome to improve the shared guidance. The verified repository owner lands changes directly on `master` and removes any temporary work branch after delivery. Contributors submit a fresh branch in a pull request against `master`. Explain what the existing guidance leaves unresolved, why the new approach is worth sharing, and how you verified it. [Contribution workflow](unity-code-skill/references/contributing.md#submit-a-skill-change) · [GitHub Issues](https://github.com/qinbatista/qin-unity-skills/issues)

## Examples

The [illustrated image optimization replay](examples/image-optimization/README.md) shows one ground tile across two platforms, oak trees with adjustable trunks, compact UI artwork at several sizes, a real target import and a packed Sprite Atlas. Open the Unity captures beside the source pieces, or [reproduce every image case from the installed pack](unity-performance-skill/references/performance-cases.md#replay-the-image-optimization-examples).

The [Charge Pulse sample](examples/charge-pulse/README.md) follows one button press through gameplay code, UI, animation and an effect. Start with the [project structure](examples/charge-pulse/CASEBOOK.md#project-structure) or open any of the 52 cases.

> Read this Unity project's rules and use the matching Skills to repair a Charge button that admits a second request while its animation is playing. Verify the actual input and replay path.

In Charge Pulse, follow the button binding to the controller that admits the request. The Play Mode case `NativeButtonPlaysTheEffectAndRecyclesForAnotherPulse` checks duplicate requests during playback and submits the button again after recycling. With both packs installed, the general Skills cover task scope, scratch and delivery, and the Unity Skills cover the controller, UI, animation and native test. [Native test](examples/charge-pulse/Assets/Editor/Tests/ChargePulseTests.cs) · [Four repeatable Codex writing prompts](.agents/skills/testing-skill/references/codex-writing-regression.md)

The [Job State Batches sample](examples/job-state-batches/README.md) contains five Unity scenes and native tests for Job-owned movement, completed-state events and Controller-owned presentation.

For a reusable gameplay foundation, the [Singleton Manager](unity-code-skill/references/unity-game-code-structure-design.md#singleton-managers) owns one runtime [ScriptableObject with a local save file](unity-code-skill/references/unity-lifecycle-and-serialization.md#scriptableobject-local-files). Both references include the C# base and a working consumer.

The [ScriptableObject data contracts](unity-code-skill/references/scriptableobject-data-contracts.md#design-and-review-a-data-owner) cover keyed spell records, shared session state, versioned local and online wallet formats, and native replay of older assets and saves.

| Skill | What to inspect |
| --- | --- |
| [Unity Code](unity-code-skill/SKILL.md) | [C# style and runtime ownership](examples/charge-pulse/CASEBOOK.md#unity-code) · [Charge controller](examples/charge-pulse/Assets/Scripts/ChargePulse/ChargePulseController.cs) |
| [Unity Assets](unity-assets-skill/SKILL.md) | [Prefab placement, asset moves and imports](examples/charge-pulse/CASEBOOK.md#unity-assets) · [1,000 PPU, tiled ground and adjustable trees](unity-assets-skill/references/texture-intake.md#build-resizable-world-sprites) |
| [Unity UI](unity-ui-skill/SKILL.md) | [Resizable buttons and panels](unity-ui-skill/references/artwork-intake.md#resize-buttons-and-panels) · [Screen layout configuration](unity-ui-skill/assets/screen-layouts.json) · [Five responsive layouts](examples/charge-pulse/README.md#responsive-ui-cases) · [Toolkit and Canvas input and localization](examples/charge-pulse/CASEBOOK.md#unity-ui) |
| [Unity Animation](unity-animation-skill/SKILL.md) | [Clips, transitions, Blend Trees and rigs](examples/charge-pulse/CASEBOOK.md#unity-animation) |
| [Unity Effects](unity-effects-skill/SKILL.md) | [Particles, shaders, Graph bindings and replay](examples/charge-pulse/CASEBOOK.md#unity-effects) |
| [Unity Performance](unity-performance-skill/SKILL.md) | [C# hot paths, pooling and asset lifetimes](unity-performance-skill/references/runtime-code.md) · [Simulation, physics and visual effects in 2D](unity-performance-skill/references/simulation-and-effects.md) · [Before-and-after measurement](unity-performance-skill/references/measure-and-verify.md) |
| [Unity Testing](unity-testing-skill/SKILL.md) | [Native tests and evidence checks](examples/charge-pulse/CASEBOOK.md#unity-testing-and-repository-skills) · [Run the cases](examples/charge-pulse/CASEBOOK.md#run-the-cases) |
| [Unity Tech Explain](unity-tech-explain-skill/SKILL.md) | [ScriptableObject storage from reward to restart](unity-tech-explain-skill/references/scriptableobject-storage.md) |

<details>
<summary>All 52 cases by name</summary>

- **Code** [C01 Explicit type and result ownership](examples/charge-pulse/CASEBOOK.md#c01) · [C02 Empty wrapper, recursion and call layout](examples/charge-pulse/CASEBOOK.md#c02) · [C03 Input, state and callbacks](examples/charge-pulse/CASEBOOK.md#c03) · [C04 Continuous frame work](examples/charge-pulse/CASEBOOK.md#c04) · [C05 Fixed-step physics and late presentation](examples/charge-pulse/CASEBOOK.md#c05) · [C06 Deadline and cancellation](examples/charge-pulse/CASEBOOK.md#c06) · [C07 Job and slot ownership](examples/charge-pulse/CASEBOOK.md#c07) · [C08 Managed allocation](examples/charge-pulse/CASEBOOK.md#c08) · [C09 Pool capacity and material ownership](examples/charge-pulse/CASEBOOK.md#c09) · [C10 Async lifecycle](examples/charge-pulse/CASEBOOK.md#c10) · [C11 C# content escaping](examples/charge-pulse/CASEBOOK.md#c11) · [C12 Addressables and late arrivals](examples/charge-pulse/CASEBOOK.md#c12) · [C13 Durable provider side effect](examples/charge-pulse/CASEBOOK.md#c13) · [C14 Authored and runtime data](examples/charge-pulse/CASEBOOK.md#c14)
- **Assets** [A01 Prefab and scene ownership](examples/charge-pulse/CASEBOOK.md#a01) · [A02 File move and serialized consumers](examples/charge-pulse/CASEBOOK.md#a02) · [A03 Dynamic loading](examples/charge-pulse/CASEBOOK.md#a03) · [A04 Texture geometry](examples/charge-pulse/CASEBOOK.md#a04) · [A05 Data texture and alpha](examples/charge-pulse/CASEBOOK.md#a05) · [A06 Native assembly placement](examples/charge-pulse/CASEBOOK.md#a06) · [A07 Serialized name migration](examples/charge-pulse/CASEBOOK.md#a07)
- **UI** [U01 Native host and semantic tree](examples/charge-pulse/CASEBOOK.md#u01) · [U02 Geometry and hit area](examples/charge-pulse/CASEBOOK.md#u02) · [U03 Insets and localization](examples/charge-pulse/CASEBOOK.md#u03) · [U04 Destination, modal input and recovery](examples/charge-pulse/CASEBOOK.md#u04) · [U05 Existing Canvas and sliced art](examples/charge-pulse/CASEBOOK.md#u05)
- **Animation** [M01 One-shot and exact action timing](examples/charge-pulse/CASEBOOK.md#m01) · [M02 Cancellation and stale event](examples/charge-pulse/CASEBOOK.md#m02) · [M03 Loop continuity](examples/charge-pulse/CASEBOOK.md#m03) · [M04 One-dimensional Blend Tree](examples/charge-pulse/CASEBOOK.md#m04) · [M05 Two-dimensional Blend Tree](examples/charge-pulse/CASEBOOK.md#m05) · [M06 Direct Blend Tree, Any State and overrides](examples/charge-pulse/CASEBOOK.md#m06) · [M07 Native blended pose](examples/charge-pulse/CASEBOOK.md#m07) · [M08 Two-bone native skin](examples/charge-pulse/CASEBOOK.md#m08) · [M09 GIF timestamps and paired playback](examples/charge-pulse/CASEBOOK.md#m09) · [M10 Spine inventory and parity boundary](examples/charge-pulse/CASEBOOK.md#m10)
- **Effects** [E01 Reference and dependency ownership](examples/charge-pulse/CASEBOOK.md#e01) · [E02 Particle setup and capacity](examples/charge-pulse/CASEBOOK.md#e02) · [E03 Seven phases and replay](examples/charge-pulse/CASEBOOK.md#e03) · [E04 Independent transparent coverage](examples/charge-pulse/CASEBOOK.md#e04) · [E05 Shape progression](examples/charge-pulse/CASEBOOK.md#e05) · [E06 Retraction from the current value](examples/charge-pulse/CASEBOOK.md#e06) · [E07 Shader Graph function binding](examples/charge-pulse/CASEBOOK.md#e07) · [E08 VFX Graph command binding](examples/charge-pulse/CASEBOOK.md#e08) · [E09 Material reuse and resource release](examples/charge-pulse/CASEBOOK.md#e09)
- **Testing** [T01 Scope to the actual consumer](examples/charge-pulse/CASEBOOK.md#t01) · [T02 Report integrity](examples/charge-pulse/CASEBOOK.md#t02) · [T03 Artifact identity](examples/charge-pulse/CASEBOOK.md#t03) · [T04 Cold, warm and memory scope](examples/charge-pulse/CASEBOOK.md#t04) · [T05 Asset, UI and media proof](examples/charge-pulse/CASEBOOK.md#t05) · [T06 Routing and release checks](examples/charge-pulse/CASEBOOK.md#t06) · [T07 Scene transition, disposal and reentry](examples/charge-pulse/CASEBOOK.md#t07)

</details>

To use the cases on another Unity project, start with its rules and registered workflows. The public Skills supply portable methods. Follow the [Unity CLI workflow](unity-code-skill/references/unity-cli.md) for background execution and standalone CLI updates. For a YoFaAssets-linked game, see the [shared workflow map](docs/yofaassets-comparison.md).

The [project AI index](skills/ai-index/SKILL.md) and [test map](.agents/skills/testing-skill/SKILL.md) cover this repository. GitHub Actions keeps the native marketplace files current on `master`. The grouped installer reads the seven-Skill package from the `master` archive.
