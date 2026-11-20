---
name: unity-animation-skill
description: Author, repair, or review Unity AnimationClips, Animator controllers, transitions, Blend Trees, events, loops, and 2D rig or Spine transfer. Use when the work concerns what moves and how playback behaves.
---

# Unity Animation

Bind the actual object, Prefab or variant, Animator root, Unity version, animation packages and receiving project. In a YoFaAssets-linked game, use its current `unity-animation` or `skeletal-animation` workflow first for the native authoring and delivery procedure.

1. **Define playback.** Record the trigger, initial pose, key times, endpoint, loop or one-shot behavior, interruption and retrigger rules, time source and expected reset. Name the writer for each animated property and the owner of any gameplay action.
2. **Choose the matching structure.** Use [playback and Animator](references/playback-and-animator.md) for states, transitions, Blend Trees, events, binding ownership and native readback.
3. **Author observable timing.** Block poses and contacts before shaping [motion curves](references/motion-curves.md). Review the response of every continuous visual channel, including VFX and shader channels tied to the action.
4. **Fit and review motion.** Use [reference motion review](references/reference-motion-review.md) for timed key poses, loop closure, GIF fidelity and synchronized playback. Use [rig transfer](references/rig-transfer.md) for skinning and retargeting, and the [Spine migration contract](references/spine-to-unity-contract.md) for source inventory and parity.
5. **Verify the saved result.** Reopen the exact Prefab, Controller and Clip, then run the actual entry. Exercise the relevant one-shot finish, loop seam, interruption and reset. Deliver the actual asset paths and the timing or bindings the user can adjust.
