---
name: unity-animation-skill
description: Author, repair, or review Unity AnimationClips, Animator controllers, transitions, Blend Trees, events, loops, and 2D rig or Spine transfer. Use when the work concerns what moves and how playback behaves.
---

# Unity Animation

**Project rules come first.** Follow the target project's `AGENTS.md`, Skill order and registered workflows. Use this Skill for remaining decisions. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) only where the project permits it and no project or registered shared owner governs Unity CLI.

For linked work, use `explain unity-animation` for native authoring or `explain skeletal-animation` for rigs and Spine. Bind the actual Prefab or variant, Animator root and playback contract.

1. Set trigger, clock, property writers, loop or finish, interruption and reset through [playback and Animator](references/playback-and-animator.md). Use the existing timeline for authored motion and real handoff events.
2. Block key poses and contacts first. Shape each changing visual channel through the [motion-curve rules](references/motion-curves.md), including related VFX and shader values.
3. For source-bound motion, use [reference motion review](references/reference-motion-review.md) for key poses, seams and synchronized playback. For a grounded gait, apply the [foot contact and stride checks](references/reference-motion-review.md#foot-contact-and-stride-alignment) for signed knee bends, whole-body turns and planted feet before replacing the saved action. Select [rig transfer](references/rig-transfer.md) or the [Spine migration contract](references/spine-to-unity-contract.md) for actual rig work.
4. Reopen and play the saved bindings through the real entry. Check the affected finish, seam, interruption or reuse. Identify delivered assets and editable values.
