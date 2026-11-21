# YoFaAssets and standalone Unity Skills

Public Skills supplement the receiving project's rules. Start with the project's `AGENTS.md` and follow its declared order for project Skills, the registered YoFaAssets `explain task-entry`, and the selected shared topic. Accept results through the project's own runner, fixtures and runtime requirements. See [Unity workflow and precedence](../unity-code-skill/references/unity-cli.md).

| YoFaAssets topic | Public Skill | What the public Skill supplies |
| --- | --- | --- |
| `game-code-logic` | [Unity Code](../unity-code-skill/SKILL.md) | C# structure, frame callbacks, Job completion, [allocation patterns and lease ownership](../unity-code-skill/references/allocation-and-ownership-patterns.md), and the source checker. |
| `yofa-plugin` | [Unity Code](../unity-code-skill/SKILL.md) | [Service integration](../unity-code-skill/references/unity-service-integration.md) covers dependencies, provider ownership, purchases and durable side effects. |
| `unity-project`, `scene-materials`, `texture-compression` and `mapchecker` | [Unity Assets](../unity-assets-skill/SKILL.md) | Feature placement, tooling and GUID-preserving moves. The shared topic supplies the linked layout and checker. Use [texture intake](../unity-assets-skill/references/texture-intake.md) for sizing, PPU and native comparison. |
| `unity-ui-build`, `ui-screen-adaptation`, `ui-button-test` and `testing-ui-toolkit` | [Unity UI](../unity-ui-skill/SKILL.md) | [Artwork intake](../unity-ui-skill/references/artwork-intake.md), [screen geometry](../unity-ui-skill/references/screen-adaptation.md), localization and [destination-page interaction](../unity-ui-skill/references/layout-and-input.md). |
| `unity-animation` and `skeletal-animation` | [Unity Animation](../unity-animation-skill/SKILL.md) | [Animator and playback](../unity-animation-skill/references/playback-and-animator.md), [motion curves](../unity-animation-skill/references/motion-curves.md), [timed poses and reference review](../unity-animation-skill/references/reference-motion-review.md), rig compatibility and [Spine migration](../unity-animation-skill/references/spine-to-unity-contract.md). |
| `game-effect`, `vfx` and `shader` | [Unity Effects](../unity-effects-skill/SKILL.md) | Portable reuse, creation and [native verification](../unity-effects-skill/references/rendering-and-verification.md). YoFaAssets owns reference assets, exact IDs, the shared effect profile and library handoff. The receiving game owns the effect package and preview scenes. |
| `unity-game-testing` | [Unity Testing](../unity-testing-skill/SKILL.md) | [Runtime and artifact evidence](../unity-testing-skill/references/runtime-and-artifact-evidence.md), including package identity, transition readiness, frame pacing and memory. |

## Select supplemental guidance

- Where the project leaves a method open, use Unity Code for C# structure and lifecycle or Unity Assets for Prefab placement and moves. Use Unity Testing for portable evidence methods within the project's acceptance contract.
- Use Unity UI for its layout and navigation contract. Use Unity Animation for Clip, Animator or rig playback. Use Unity Effects for a visual effect's layer composition and rendering. Select a second Skill when the requested work actually crosses that boundary.

## Connected workflows

YoFaAssets exposes portable references in `sharedUnitySkill` through nine mapped entry topics. Screen adaptation, button review and Toolkit cases use `explain unity-ui-build` for portable UI methods. Scene and Map Checker workflows use `explain unity-project` or `explain texture-compression` for applicable asset methods. The SpriteTamer shader adapter uses `explain game-effect` for portable rendering methods. Existing topic IDs, aliases and reference paths retain their connected procedures.

YoFaAssets owns the shared folder, PPU and 4K policies, CLI update timing, native adapters, and common authoring and verification workflows. Linked projects use that source's daily CLI check on the first eligible command. Rule and documentation reviews use source readback within the linked workflow. For UI, `explain ui-split` owns accepted artwork and authored screen compositions. The [Unity handoff](../unity-ui-skill/references/artwork-intake.md#yofaassets-split-to-unity) binds those inputs to the receiving game's native layout and input.

The receiving game owns gameplay and system rules, paths, serialized settings, supported orientation, package defaults, and native test runners, fixtures and assertions. Keep their concrete rules in the game. Apply the shared workflows for their declared scope and the public Skills for portable methods.
