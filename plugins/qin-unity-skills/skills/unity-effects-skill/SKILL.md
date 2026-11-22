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

When a design document or image supplies the brief, identify the exact ability and read that input before looking for effects. Use the receiving project's planning route for a registered document, or record the exact file or attachment provided with the request. Take the event and outcome from the game rules and describe the visible requirements from the design. When asked to pick an ability, choose one defined by that source.

For new or visibly revised motion, make and inspect a timed GIF from that brief, or reuse the reviewed GIF for the same design. Mark the trigger, origin, contact and ending. Use those beats to search the game's registered source for an editable effect with the right motion, depth or material response. Adapt its useful parts to the game's artwork. Use primary source code or articles for uncovered techniques. With no usable editable source, build from the reviewed GIF and record the gap. Follow the [source and component procedure](references/staged-effects.md#use-an-existing-effect-or-reference).

## Create or revise an effect

1. Break down the GIF's independently visible jobs. For each one, record its anchor, cause, timing and ending, the relevant reference principle, the receiving-game adaptation and any source assets actually reused.
2. Adapt each numbered effect and play it alone. Apply the principle to compatible source emitters, curves, shader logic or game-owned materials. Check whether the source's axes, timing, camera and lifetime still make sense for this ability. Follow [progressive formation](references/staged-effects.md#progressive-formation-and-layers) and [bind the production renderer](references/rendering-and-verification.md#bind-the-renderer-and-source).
3. Compare the complete effect with the GIF at the same trigger, scale and playback speed. Drive the documented gameplay action and observe its outcome and cue on the same instance. Repair a failing layer and replay. When the layers pass alone and the whole gesture still reads incorrectly, revisit their roles, overlap and order.

## Verify the native result

Run the smallest native case on the intended render path. Check rendered pixels, lifecycle and cleanup. Measure simultaneous load for performance claims. For a fidelity delivery, verify the receiving project's documented replay and comparison commands from fresh task output.
