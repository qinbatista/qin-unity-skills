---
name: unity-animation-skill
description: Author, repair, or review Unity AnimationClips, Animator controllers, transitions, Blend Trees, events, loops, and 2D rig or Spine transfer. Use when the work concerns what moves and how playback behaves.
---

# Unity Animation

Bind the actual object, Prefab or variant, Animator root, Unity version, animation packages and receiving project. In a YoFaAssets-linked game, use its current `unity-animation` or `skeletal-animation` source first.

1. **Define playback.** Record the trigger, initial pose, key times, endpoint, loop or one-shot behavior, interruption and retrigger rules, time source and expected reset. Name the writer for each animated property and the owner of any gameplay action.
2. **Choose the matching structure.** Use a single state for a simple loop. Add explicit transitions and parameters for entry, cancellation or reset where the behavior needs them. Use a Blend Tree for compatible motions driven by a continuous parameter. Use states or timelines for successive phases and layers or masks for genuinely independent channels.
3. **Author observable timing.** Block the poses and contacts before shaping tangents. Design acceleration, hold, follow-through and recovery for continuous visual motion as appropriate. Preserve discrete events, actual gameplay timing and endpoints. Check overshoot, angular continuity, the loop seam and its entry and exit velocities.
4. **Bind side effects to their owner.** Resolve Clip binding paths against the saved Animator root and keep physics or gameplay motion separate where the project gives it an independent owner. Use Animation Events for actual contact or release points. Gameplay actions retain their own request identity and completion semantics across interrupted, skipped or repeated callbacks.
5. **Verify saved assets and playback.** Reopen the exact Prefab, Controller and Clip. Verify overrides, bindings, transition timing, contacts and reset at runtime. Exercise the relevant one-shot finish, repeated loops, interruption and reuse. Inspect moving frames for visual claims and use synchronized source and native playback when authoring to a motion reference.

Use [2D rig and motion transfer](references/rig-transfer.md) for skinning, retargeting, GIF-driven authoring or Spine migration.
