---
name: unity-performance-skill
description: Review runtime cost on every Unity task, including requests that omit performance. Select, create and run behavioral optimization cases. Use for C# hot paths, pooling, asset loading and release, 2D simulation architecture, ECS and Jobs, physics, shader cost, reusable VFX and matched performance measurements.
---

# Unity Performance

**Project rules come first.** Follow the project's rules and registered workflows. Use the [standalone CLI gate](../unity-code-skill/references/unity-cli.md#standalone-cli) when permitted and no project or registered shared owner governs Unity CLI.

Use [Unity Tech Explain](../unity-tech-explain-skill/SKILL.md) when explaining how a Unity Skill, feature or code path works.

Use the [completion review](#review-every-unity-task) for every Unity request. For a linked game, use the registered `game-code-logic` and applicable `game-effect` or `unity-game-testing` workflow. Select a method that fits the receiving game and [share reusable improvements](../unity-code-skill/references/contributing.md#submit-a-skill-change).

## Review every Unity task

1. Trace the change from its entry through the helpers, callbacks, data, presentation and teardown it activates. Check runtime cost even when the request mentions only a feature, bug or visual change. For a documentation or Editor-only task, establish whether it changes the running game. If the runtime path is untouched, record that and finish.
2. Consider each cost that can arise on that path: per-frame or fixed-step work, peak population, allocations and object churn, load and release, shared simulation data, contacts, presentation and GPU work. Include costs introduced by the proposed fix, such as retained pool memory or a Job completion wait. Select each applicable family in the [casebook](references/performance-cases.md#select-cases-from-the-change). Trace additional project-specific costs in the same way.
3. Give each applicable cost a case: input and load, existing behavior, proposed change, an observable cost, and a failure or lifecycle boundary. Reuse a maintained case when it proves that contract. For an uncovered path, create a case through the receiving project's actual entry. Exercise normal play, a representative peak, and applicable cancellation, reentry or exhaustion. Run each case, inspect its actual output, and repair failures within the requested edit scope.
4. Fix evidenced waste with the smallest change that preserves gameplay authority, timing, ordering and appearance. A small bounded loop or an occasional spawn may already fit. For a read-only review, report the finding and use permitted simulations. If the real target is unavailable, report which simulations passed and exactly which target behavior or cost still needs proof.
5. Record each case with its workload, decision, result and evidence. Use a [matched comparison](references/measure-and-verify.md#compare-the-same-workload) for a speed or memory claim. The [installed replay](references/performance-cases.md#run-the-installed-casebook) exercises portable examples. Apply the same method to the receiving game's implementation before claiming its behavior or performance. Before publishing an optimization, run a representative example and ship the inputs, measured result and a way to replay it. For a visual change, show the source art and native output together, as in the [woodland examples](https://github.com/qinbatista/qin-unity-skills/tree/master/examples/image-optimization).

## Choose an operation

| When reviewing | Read |
| --- | --- |
| An `Update` loop scans every actor, or managed allocations recur | [Frame work and managed allocations](references/runtime-code.md#frame-work-and-managed-allocations) |
| Frequent spawns, destruction, or a pool that keeps growing | [Spawn and retire](references/runtime-code.md#spawn-and-retire) |
| Loading stalls, Addressables instantiation, or memory left after scene exit | [Load and release content](references/runtime-code.md#load-and-release-content) |
| Many similar objects, followers or characters | [Choose the simulation owner](references/simulation-and-effects.md#choose-the-simulation-owner) |
| Objects affect each other, hit a player or need pickup behavior | [Choose a collision model](references/simulation-and-effects.md#choose-a-collision-model) or [simplify a ground pickup](references/simulation-and-effects.md#simplify-a-ground-pickup) |
| Concurrent explosions, expensive shaders or particle overdraw | [Reuse an impact effect](references/simulation-and-effects.md#reuse-an-impact-effect) or [reduce shader and VFX cost](references/simulation-and-effects.md#reduce-shader-and-vfx-cost) |
| Repeated world art, resizable UI or a large sprite import | [Replay the image optimization examples](references/performance-cases.md#replay-the-image-optimization-examples). |
| A frame spike has no known cause, or a proposed improvement needs proof | [Compare the same workload](references/measure-and-verify.md#compare-the-same-workload) |
