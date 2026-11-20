# YoFaAssets and standalone Unity Skills

Use the public Skills for portable Unity development decisions. A YoFaAssets-linked game uses its registered owner's current `explain task-entry`, the selected topic, and the receiving game's own contract. Its documented conventions and actual runtime remain authoritative.

| YoFaAssets topic | Public Skill | What the public Skill supplies |
| --- | --- | --- |
| `game-code-logic` | [Unity Code](../unity-code-skill/SKILL.md) | C# structure, frame callbacks, Job completion, [allocation patterns and lease ownership](../unity-code-skill/references/allocation-and-ownership-patterns.md), and the source checker. |
| `yofa-plugin` | [Unity Code](../unity-code-skill/SKILL.md) | [Service integration](../unity-code-skill/references/unity-service-integration.md) covers dependencies, provider ownership, purchases and durable side effects. |
| `unity-project`, `scene-materials`, `texture-compression` and `mapchecker` | [Unity Assets](../unity-assets-skill/SKILL.md) | Placement, GUID-preserving moves, scene and Prefab intake, [source sizing, PPU and native texture comparisons](../unity-assets-skill/references/texture-intake.md). |
| `unity-ui-build`, `ui-screen-adaptation`, `ui-button-test` and `testing-ui-toolkit` | [Unity UI](../unity-ui-skill/SKILL.md) | [Artwork intake](../unity-ui-skill/references/artwork-intake.md), [screen geometry](../unity-ui-skill/references/screen-adaptation.md), localization and [destination-page interaction](../unity-ui-skill/references/layout-and-input.md). |
| `unity-animation` and `skeletal-animation` | [Unity Animation](../unity-animation-skill/SKILL.md) | [Animator and playback](../unity-animation-skill/references/playback-and-animator.md), [motion curves](../unity-animation-skill/references/motion-curves.md), [timed poses and reference review](../unity-animation-skill/references/reference-motion-review.md), rig compatibility and [Spine migration](../unity-animation-skill/references/spine-to-unity-contract.md). |
| `game-effect` and `shader-spritetamer` | [Unity Effects](../unity-effects-skill/SKILL.md) | [Effect stages, progressive formation and layers](../unity-effects-skill/references/staged-effects.md), [rendering and native verification](../unity-effects-skill/references/rendering-and-verification.md). |
| `unity-game-testing` | [Unity Testing](../unity-testing-skill/SKILL.md) | [Runtime and artifact evidence](../unity-testing-skill/references/runtime-and-artifact-evidence.md), including package identity, transition readiness, frame pacing and memory. |

## Choose one primary workflow

- A gameplay system or C# lifecycle change starts with Unity Code. A Prefab move starts with Unity Assets. Use Unity Testing when current runtime evidence is part of the result.
- Use Unity UI for its layout and navigation contract. Use Unity Animation for Clip, Animator or rig playback. Use Unity Effects for a visual effect's layer composition and rendering. Select a second Skill when the requested work actually crosses that boundary.

## Connected workflows

YoFaAssets exposes portable references in `sharedUnitySkill` through nine mapped entry topics. Screen adaptation, button review and Toolkit cases use `explain unity-ui-build` for portable UI methods. Scene and Map Checker workflows use `explain unity-project` or `explain texture-compression` for applicable asset methods. The SpriteTamer shader adapter uses `explain game-effect` for portable rendering methods. Existing topic IDs, aliases and reference paths retain their connected procedures.

YoFaAssets owns the exact folder, PPU and 4K policies, its Unity CLI gate and native adapters, game-specific code contracts and acceptance profiles. Its catalog, planning, stories, tasks, art generation and splitting, animation gallery and GIF fitting, Map Checker exports, resource and database sync, SDK and publishing workflows operate on declared YoFaAssets sources and tools. The receiving game owns its implementation, assets, timers, profiles, fixtures and runtime results.
