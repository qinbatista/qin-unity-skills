---
name: unity-effects-skill
description: Route Unity VFX tasks by reference reuse, particle or VFX Graph lifecycle, staged formation, shader or material lighting and native rendering cost. Use for creation, repair, optimization and verification.
---

# Unity Effects

**Project rules come first.** Follow the project's rules and registered workflows. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) when permitted and no project or registered shared owner governs Unity CLI.

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

Start with the game’s named reference source, or a motion GIF when the game has no source. Follow the [reference selection and component review](references/staged-effects.md#use-an-existing-effect-or-reference) before authoring.

## Create or revise an effect

1. Break the selected motion into its independently visible effects. State the count and the job, anchor and time window of each. A single effect is a valid result.
2. Build and play each effect alone. Choose a Particle System or VFX Graph for emission and travel, or a shader and bound material for reveal, lighting and distortion. Follow [progressive formation](references/staged-effects.md#progressive-formation-and-layers) and the receiving project's lifecycle. Keep exclusive assets in the owning package and [bind the production renderer](references/rendering-and-verification.md#bind-the-renderer-and-source).
3. Play the complete composition from the real trigger at game speed and scale. Compare it to the reference. Tune an individual effect when its shape or timing is wrong. When the combined gesture still reads incorrectly, return to step 1, revise the count or responsibilities and rebuild the affected effects. Replay the same case until its observable criteria pass.

## Verify the native result

Run the smallest relevant native case on the intended render path. Check compilation and pixels, lifecycle and cleanup, and the intended simultaneous load when a performance claim is in scope. Retest with the same trigger and camera.
