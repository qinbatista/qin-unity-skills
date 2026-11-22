---
name: unity-effects-skill
description: Route Unity VFX tasks by reference reuse, particle or VFX Graph lifecycle, staged formation, shader or material lighting and native rendering cost. Use for creation, repair, optimization and verification.
---

# Unity Effects

**Project rules come first.** Follow the project's rules and registered workflows. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) when permitted and no project or registered shared owner governs Unity CLI.

Treat the Skill's methods as adaptable defaults. Use the approach that best fits the task and [share reusable improvements](../unity-code-skill/references/contributing.md#submit-a-skill-change).

Read the selected headings by link or [section reader](../unity-code-skill/scripts/read_topic.py). Audit any Unity image touched via [Assets](../unity-assets-skill/SKILL.md#admit-artwork-and-textures).

For linked work select `explain game-effect` for references, the effect profile and library handoff. Bind the effect or material, trigger, camera, scale, renderer and target. For a change [verify the native result](#verify-the-native-result).

## Choose an operation

| Operation | Read for this task |
| --- | --- |
| Find or adapt a reference | [Use an existing effect or reference](#use-an-existing-effect-or-reference). |
| Build emission, travel, formation or tails | [Create or revise an effect](#create-or-revise-an-effect). Use [lifecycle](references/staged-effects.md#native-lifecycle) for start/stop or [formation and layers](references/staged-effects.md#progressive-formation-and-layers) for changing shape. |
| Fix material lighting, transparent emission, VFX binding or sorting | [Renderer and source](references/rendering-and-verification.md#bind-the-renderer-and-source). For common HLSL and Shader Graph math use [shared calculations](references/rendering-and-verification.md#share-rendering-calculations). |
| Reduce overdraw or verify appearance, lifetime or GPU cost | [Actual render path and lifetime](references/rendering-and-verification.md#observe-the-actual-render-path-and-lifetime). Recheck at the same trigger, camera and simultaneous load. |

## Use an existing effect or reference

When a design document or image supplies the brief, identify the exact ability and read that input before looking for effects. Use the receiving project's planning route to bind the version and page or image. Take the event and outcome from the game rules and describe the visible requirements from the design. When asked to pick an ability, choose one defined by that source.

Start with editable assets from the game’s named source. Open the exact Prefab, graph, material, mesh, texture or code and check that it can drive the effect in the receiving project. Adapt the usable source in place or in the game’s effect package. Use a motion GIF for a recorded gap in the registered library, or as the default when the game has no registered source. Follow the [source and component procedure](references/staged-effects.md#use-an-existing-effect-or-reference).

## Create or revise an effect

1. Inspect the editable source and its behavior. Break the selected motion into its independently visible effects. State the count and the job, anchor, time window and source asset for each. A single effect is a valid result.
2. Adapt and play each effect alone. Retain compatible source emitters, curves, shader logic and materials, and build only a contribution the usable source does not supply. Follow [progressive formation](references/staged-effects.md#progressive-formation-and-layers) and the receiving project's lifecycle. Keep exclusive assets in the owning package and [bind the production renderer](references/rendering-and-verification.md#bind-the-renderer-and-source).
3. Play the complete composition from the documented game trigger at game speed and scale. Check the ability's observable game result and compare its visuals to the selected reference. Tune an individual effect when its shape or timing is wrong. When the combined gesture still reads incorrectly, return to step 1, revise the count or responsibilities and rebuild the affected effects. Replay the same case until its observable criteria pass.

## Verify the native result

Run the smallest relevant native case on the intended render path. Check compilation and pixels, lifecycle and cleanup, and the intended simultaneous load when a performance claim is in scope. Retest with the same trigger and camera.
