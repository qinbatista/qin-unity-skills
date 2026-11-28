# Unity performance

[Unity Performance](../../../unity-performance-skill/SKILL.md) owns the [completion review](../../../unity-performance-skill/SKILL.md#review-every-unity-task) linked by every Unity domain and the pack task menu. Every runtime-impacting request uses this review to select and run cases, including costs the user omitted.

- [Runtime code](../../../unity-performance-skill/references/runtime-code.md) owns the choices for frame work, GC, pooling and streamed content. Unity Code supplies the implementation-level lifetime rules.
- [SO storage costs](../../../unity-code-skill/references/so-data-controller.md#audit-storage-cost-by-mode) covers shared instances, file contention, serialized cloud writes and replicated updates. Use the receiving game's current writer for its own measurements.
- [Simulation and effects](../../../unity-performance-skill/references/simulation-and-effects.md) covers 2D actor scale, gameplay authority, collision requirements, Jobs, ECS and reusable visual effects.
- [Measurement](../../../unity-performance-skill/references/measure-and-verify.md) pairs a behavior check with a matched workload. The receiving game owns its budgets and captures.

The [casebook](../../../unity-performance-skill/references/performance-cases.md) maps paths to the [portable replay CLI](../../../unity-performance-skill/scripts/replay_cases.py). The Skill packages its case inventory, Editor tests and graphics inputs beside the CLI. The [verified replay](../../../examples/performance-cases/Verification.json) binds the results to the fixture hash and Unity package versions. The [installed image replay](../../../unity-performance-skill/references/performance-cases.md#replay-the-image-optimization-examples) owns the native fixtures and source art. It exercises Toolkit, Canvas, world sizing, texture import and packing in fresh receivers. The [gallery](../../../examples/image-optimization/README.md) owns the captured output. Publish representative native evidence with each optimization case. The receiving game owns regressions for any additional costs discovered along its actual entry.

Verify the installed routes with the [repository testing Skill](../../../.agents/skills/testing-skill/SKILL.md). The Charge Pulse code cases and Job State Batches scenes exercise the runtime behaviors used as examples.

For a feature-level example, trace repeat water hits from the [performance casebook](../../../unity-performance-skill/references/performance-cases.md#select-cases-from-the-change) to the [RockImpact resource case](../../../unity-effects-skill/references/fluid-mixer.md#verify-resource-reuse-across-casts). The [two-receiver results](../../../examples/performance-cases/WaterReuse.json) record the observed cost and solver behavior.
