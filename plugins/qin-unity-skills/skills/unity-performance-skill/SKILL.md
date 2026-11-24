---
name: unity-performance-skill
description: Review runtime cost before completing any Unity task. Use for C# hot paths, pooling, asset loading and release, 2D simulation architecture, ECS and Jobs, physics, shader cost, reusable VFX and matched performance measurements.
---

# Unity Performance

**Project rules come first.** Follow the project's rules and registered workflows. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) when permitted and no project or registered shared owner governs Unity CLI.

Use the [completion review](#review-every-unity-task) for every Unity request. For a linked game, use the registered `game-code-logic` and applicable `game-effect` or `unity-game-testing` workflow. Select a method that fits the receiving game and [share reusable improvements](../unity-code-skill/references/contributing.md#submit-a-skill-change).

## Review every Unity task

1. Trace the affected path from its trigger to its consumers and teardown. Include helpers, callbacks, loaded assets and visual work that the change activates. For a documentation, lookup or Editor-only task, identify any effect on the running game. When there is none, the review ends there.
2. Ask what runs each frame, what scales with the number of actors or simultaneous events, what allocates or blocks, and who can release what is cached. Inspect the relevant paths with the operation below. An occasional spawn and a small active loop can already be the right implementation.
3. Fix evidenced waste within the task's edit scope. Start with removing unnecessary work, then consider reusing data, changing cadence or switching the simulation or rendering technology. Preserve gameplay authority, timing, ordering, lifetimes and visible behavior. For an analysis-only request, describe the smallest change and how to test it.
4. Exercise the affected behavior and its peak or teardown case. Report the workload, the decision and the evidence in a sentence. Quantify gains only after a [matched comparison](references/measure-and-verify.md#compare-the-same-workload). With source evidence alone, name the suspected cost and the measurement that would settle it.

## Choose an operation

| When reviewing | Read |
| --- | --- |
| An `Update` loop scans every actor, or managed allocations recur | [Frame work and managed allocations](references/runtime-code.md#frame-work-and-managed-allocations) |
| Frequent spawns, destruction, or a pool that keeps growing | [Spawn and retire](references/runtime-code.md#spawn-and-retire) |
| Loading stalls, Addressables instantiation, or memory left after scene exit | [Load and release content](references/runtime-code.md#load-and-release-content) |
| Many similar objects, followers or characters | [Choose the simulation owner](references/simulation-and-effects.md#choose-the-simulation-owner) |
| Objects affect each other, hit a player or need pickup behavior | [Choose a collision model](references/simulation-and-effects.md#choose-a-collision-model) or [simplify a ground pickup](references/simulation-and-effects.md#simplify-a-ground-pickup) |
| Concurrent explosions, expensive shaders or particle overdraw | [Reuse an impact effect](references/simulation-and-effects.md#reuse-an-impact-effect) or [reduce shader and VFX cost](references/simulation-and-effects.md#reduce-shader-and-vfx-cost) |
| A frame spike has no known cause, or a proposed improvement needs proof | [Compare the same workload](references/measure-and-verify.md#compare-the-same-workload) |
