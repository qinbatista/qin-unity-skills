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

Follow the [reference search and reuse steps](references/staged-effects.md#use-an-existing-effect-or-reference) for the actual ID, license, dependencies and native result.

## Create or revise an effect

1. Use a supported Particle System or VFX Graph for emission, travel and tails. Use a shader and its bound material for coverage, reveal, lighting or distortion. Combine them when their contributions are distinct.
2. Use [progressive formation and layers](references/staged-effects.md#progressive-formation-and-layers) for one trigger, progressive shape, nonlinear timing and cleanup. For linked effects, apply the profile from the current shared topic. Static materials stay static.
3. Keep exclusive assets and dedicated preview scenes with the effect, Editor-only test code with its native owner, and shared assets in their existing location. Use [renderer binding](references/rendering-and-verification.md#bind-the-renderer-and-source) to build and bind the selected VFX and shader.

## Verify the native result

Run the smallest relevant native case on the intended render path. Check compilation and pixels, lifecycle and cleanup, and the intended simultaneous load when a performance claim is in scope. Retest with the same trigger and camera.
