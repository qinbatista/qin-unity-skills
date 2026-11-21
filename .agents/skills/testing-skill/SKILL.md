---
name: testing-skill
description: Select and maintain focused tests for the public Unity Skills, catalog, routing, and the standalone C# source checker.
---

# Project testing

Use the smallest maintained case that can establish the change. Run from the repository root and keep disposable fixtures and output in the project task Cache.

| Purpose | Level | Runner and owner |
| --- | --- | --- |
| C# checker finds actual discards and forwarding, distinguishes valid syntax, and filters changed lines | 1 | `unittest`, [test_csharp_rule_guard.py](../../../unity-code-skill/tests/test_csharp_rule_guard.py), Unity Code |
| Release and discovery routes agree, and each Skill links the required CLI gate and its references | 2 | `unittest`, [test_skill_catalog.py](tests/test_skill_catalog.py), repository Testing |
| Charge Pulse sample saved bindings, nonlinear clip timing, Toolkit layout and input, visible release, cancellation and replay | 2 | Native Unity CLI EditMode with the Play transition in [ChargePulseTests.cs](../../../examples/charge-pulse/Assets/Editor/Tests/ChargePulseTests.cs). Use a task-owned example snapshot and the [sample commands](../../../examples/charge-pulse/README.md#run-the-focused-tests). |
| Casebook native callback admission, calibrated GC, Edit and Play resource retirement, Job, asset, UI, animation and shader cases | 2 | Unity CLI EditMode including Play transitions. Select `Qin.UnitySamples.Tests` or the affected case listed in the [review](../../../examples/charge-pulse/CASEBOOK.md#findings-from-this-review). |
| Casebook receipt, archive, local transaction and media/rig cases | 2 | Python `unittest` in [inspection](../../../examples/charge-pulse/inspection/test_evidence_cases.py). Pass the task run directory with `CHARGE_PULSE_EVIDENCE`. |

```text
macOS/Linux: python3 -B -m unittest discover -s unity-code-skill/tests
Windows PowerShell: py -3 -B -m unittest discover -s unity-code-skill\tests
macOS/Linux: python3 -B -m unittest discover -s .agents/skills/testing-skill/tests
Windows PowerShell: py -3 -B -m unittest discover -s .agents\skills\testing-skill\tests
```

For checker changes, run its CLI on representative source and a tracked diff. For package changes, read back the built or installed package. Run the native example for changes to its behavior or assets.
