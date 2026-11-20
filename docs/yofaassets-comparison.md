# YoFaAssets and standalone Unity Skills

Use the public Skills for portable Unity development decisions. A YoFaAssets-linked game uses its registered owner's current `explain task-entry`, the selected topic, and the receiving game's own contract. Its documented conventions and actual runtime remain authoritative.

| YoFaAssets topic | Public Skill | What the public Skill supplies |
| --- | --- | --- |
| `game-code-logic` | [Unity Code](../unity-code-skill/SKILL.md) | Retains the existing C# checker and writing profile. Adds architecture selection, Job boundaries, exact resource leases, and measured allocation checks. |
| `yofa-plugin` | [Unity Code](../unity-code-skill/SKILL.md) | Adds reusable SDK dependency, linker, receiver configuration and runtime checks. YoFaAssets continues to own its plugin integration procedure. |
| `unity-project` and `scene-materials` | [Unity Assets](../unity-assets-skill/SKILL.md) | Adapts ownership, reference-preserving moves, and native scene and Prefab intake to the receiving project's layout. |
| `texture-compression` and `mapchecker` | [Unity Assets](../unity-assets-skill/SKILL.md) | Covers texture import, PPU and Sprite geometry, companion-map alignment, and comparison of native candidate encodings. |
| `unity-ui-build`, `ui-screen-adaptation`, and `ui-button-test` | [Unity UI](../unity-ui-skill/SKILL.md) | Adds actual host and artwork binding, semantic layout, proportional fit, safe areas, localization and interaction readback. |
| `unity-animation` and `skeletal-animation` | [Unity Animation](../unity-animation-skill/SKILL.md) | Adds playback and timing decisions, event ownership, rig compatibility, Spine migration and synchronized comparison. |
| `game-effect` | [Unity Effects](../unity-effects-skill/SKILL.md) | Adds runtime effect stages, particle and shader responsibilities, rendered formation, reuse, and measured budget checks. |
| `shader-spritetamer` | [Unity Effects](../unity-effects-skill/SKILL.md) | Covers shared rendering and dependency verification. SpriteTamer's shader properties, material defaults, scene and handoff stay with their current owners. |
| `unity-game-testing` and `testing-ui-toolkit` | [Unity Testing](../unity-testing-skill/SKILL.md) | Adds the native test and evidence boundary. The UI Skill defines layout and interaction criteria, and Testing selects the runtime and runner. |

## Choose one primary workflow

- A gameplay system or C# lifecycle change starts with Unity Code. A Prefab move starts with Unity Assets. Use Unity Testing when current runtime evidence is part of the result.
- Use Unity UI for its layout and navigation contract. Use Unity Animation for Clip, Animator or rig playback. Use Unity Effects for a visual effect's layer composition and rendering. Select a second Skill when the requested work actually crosses that boundary.

YoFaAssets owns its exact folder, PPU and screen-layout policies, native adapters, catalogs, image generation, source recipes, and distribution procedures. Use its live topics for Map Checker generation, atlas splitting or gallery intake, game-specific shader adapters, YoFaPlugin, and publishing. These operations keep their source data, runtime tools, and evidence in their existing owners.
