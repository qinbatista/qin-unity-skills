---
name: unity-animation-skill
description: Route Unity motion tasks by Clip or Animator playback, transitions, Blend Trees, events, loops, easing, source-footage review, 2D rig reuse and Spine migration.
---

# Unity Animation

**Project rules come first.** Follow the project's rules and registered workflows. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) when permitted and no project or registered shared owner governs Unity CLI.

Read the selected headings by link or [section reader](../unity-code-skill/scripts/read_topic.py). Audit any Unity image touched via [Assets](../unity-assets-skill/SKILL.md#admit-artwork-and-textures).

For linked work select `explain unity-animation` for native authoring or `explain skeletal-animation` for rigs and Spine. Bind the actual Prefab or variant, Animator root and playback contract.

## Choose an operation

| Operation | Read for this task |
| --- | --- |
| Author or repair a Clip, loop or one-shot | [Playback contract](references/playback-and-animator.md#playback-contract). Set the clock, sole property writers, handoff, interruption and reset. Block key poses and contacts first. |
| Repair a transition, Blend Tree or event | [States and transitions](references/playback-and-animator.md#states-transitions-and-multiple-parts) for weights or transitions, [contacts and cancellation](references/playback-and-animator.md#contacts-side-effects-and-cancellation) for side effects. |
| Tune easing, tangents or synchronized VFX values | [Design each channel](references/motion-curves.md#design-and-inspect-each-channel) and [verify interior motion and seams](references/motion-curves.md#verify-the-resulting-motion). |
| Match reference footage or repair a sliding foot | [Timed key poses](references/reference-motion-review.md#timed-key-poses-and-loop-closure), [contact and stride](references/reference-motion-review.md#foot-contact-and-stride-alignment) for gait, then [matched playback](references/reference-motion-review.md#matched-native-playback). |
| Reuse a 2D rig or migrate Spine | [Select the transfer mode](references/rig-transfer.md#establish-the-mode). For inventory use the [source schema](references/spine-to-unity-contract.md#source-inventory-schema). For migration also [map the source](references/spine-to-unity-contract.md#mapping-table), verify the [rig signature](references/spine-to-unity-contract.md#conversion-and-rig-signature) and [prove parity](references/spine-to-unity-contract.md#parity-and-reuse). |

For a saved change, reopen the real bindings and play the affected finish, seam, interruption or reuse. Rig changes also require [saved rig acceptance](references/rig-transfer.md#accept-the-saved-rig). Identify delivered assets and editable values.
