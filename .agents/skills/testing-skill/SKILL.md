---
name: testing-skill
description: Select and maintain focused tests for the public Unity Skills, catalog, routing, and the standalone C# source checker.
---

# Project testing

Use the smallest maintained case that can establish the change. Run from the repository root and keep disposable fixtures and output in the project task Cache.

| Purpose | Level | Runner and owner |
| --- | --- | --- |
| C# checker finds actual discards and forwarding, distinguishes valid syntax, and filters changed lines | 1 | `unittest`, [test_csharp_rule_guard.py](../../../unity-code-skill/tests/test_csharp_rule_guard.py), Unity Code |
| Release and discovery routes agree, screen profile data and reader match the published package, and Skill entries resolve project precedence, architecture and standalone CLI guidance | 2 | `unittest`, [test_skill_catalog.py](tests/test_skill_catalog.py), repository Testing |
| Installed task menus and section lookup preserve required context, resolve operation anchors and return the half-speed gait example with its foot-contact acceptance | 2 | Python `unittest`, [test_topic_routing.py](tests/test_topic_routing.py), against the packaged reader. |
| Legacy game checkout roots keep their canonical memory owners in the published catalog | 2 | Python `unittest`, [test_project_memory_owners.py](tests/test_project_memory_owners.py). |
| Charge Pulse sample saved bindings, nonlinear clip timing, Toolkit layout and input, visible release, cancellation and replay | 2 | Native Unity CLI EditMode with the Play transition in [ChargePulseTests.cs](../../../examples/charge-pulse/Assets/Editor/Tests/ChargePulseTests.cs). Use a task-owned example snapshot and the [sample commands](../../../examples/charge-pulse/README.md#run-the-focused-tests). |
| Five Job State Batches scenes exercise movement with the presentation Controller detached, one completed state publication and scene-specific results after reattachment | 2 | Native Unity CLI EditMode with Play transitions in [CompletedBatchCases.cs](../../../examples/job-state-batches/Assets/Editor/Tools/JobStateBatches/Tests/CompletedBatchCases.cs). Use the [standalone sample command](../../../examples/job-state-batches/README.md#run-the-five-cases). |
| Charge Pulse macOS package identity, headless startup, preference isolation and exact cleanup | 2 | Unity CLI macOS build and the packaged executable through the [sample preference profile](../../../examples/charge-pulse/inspection/macos-packaging-preferences.sb). Use the [packaged smoke procedure](../../../examples/charge-pulse/README.md#run-a-macos-package-smoke-test). |
| Five responsive screens, live locale changes, per-locale text fit, synthetic cutouts, template lifetime, scroll containment and Canvas dialog parity | 2 | Native Unity CLI EditMode with Play transitions in [ResponsiveLayoutCases.cs](../../../examples/charge-pulse/Assets/Editor/Tests/ResponsiveLayouts/ResponsiveLayoutCases.cs). Run every locale in the fixture table at the existing viewport profiles and inspect native frames. See the [responsive UI cases](../../../examples/charge-pulse/README.md#responsive-ui-cases). |
| Casebook native callback admission, calibrated GC, Edit and Play resource retirement, Job, asset, UI, animation and shader cases | 2 | Unity CLI EditMode including Play transitions. Select `Qin.UnitySamples.Tests` or the affected case listed in the [review](../../../examples/charge-pulse/CASEBOOK.md#findings-from-this-review). |
| Casebook receipt, archive, local transaction and media/rig cases | 2 | Python `unittest` in [inspection](../../../examples/charge-pulse/inspection/test_evidence_cases.py). Pass the task run directory with `CHARGE_PULSE_EVIDENCE`. |

```text
macOS/Linux: python3 -B -m unittest discover -s unity-code-skill/tests
Windows PowerShell: py -3 -B -m unittest discover -s unity-code-skill\tests
macOS/Linux: python3 -B -m unittest discover -s .agents/skills/testing-skill/tests
Windows PowerShell: py -3 -B -m unittest discover -s .agents\skills\testing-skill\tests
```

Run the [Codex C# writing regression](references/codex-writing-regression.md) for Level 2 Skill-behavior verification. Its four independent prompts exercise frame admission, texture ownership, Job slots and a warmed filter against the existing Charge Pulse assertions.

For checker changes, run its CLI on representative source and a tracked diff. For package changes, read back the built or installed package. Run the native example for changes to its behavior or assets.
