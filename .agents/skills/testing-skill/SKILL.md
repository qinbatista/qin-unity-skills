---
name: testing-skill
description: Select and maintain focused tests for the public Unity Skills, catalog, routing, and the standalone C# source checker.
---

# Project testing

Use the smallest maintained case that can establish the change. Run from the repository root and keep disposable fixtures and output in the project task Cache.

| Purpose | Level | Runner and owner |
| --- | --- | --- |
| C# checker finds actual discards and forwarding, distinguishes valid syntax, and filters changed lines | 1 | `unittest`, [test_csharp_rule_guard.py](../../../unity-code-skill/tests/test_csharp_rule_guard.py), Unity Code |
| Release list and discovery routes agree, and each standalone package links its references | 2 | `unittest`, [test_skill_catalog.py](tests/test_skill_catalog.py), repository Testing |
| Charge Pulse sample saved bindings, nonlinear clip timing, Toolkit layout and input, visible release, cancellation and replay | 2 | Native Unity CLI EditMode with the Play transition in [ChargePulseTests.cs](../../../examples/charge-pulse/Assets/Editor/Tests/ChargePulseTests.cs). Use a task-owned example snapshot and the [sample commands](../../../examples/charge-pulse/README.md#run-the-focused-tests). |

```text
macOS/Linux: python3 -B -m unittest discover -s unity-code-skill/tests
Windows PowerShell: py -3 -B -m unittest discover -s unity-code-skill\tests
macOS/Linux: python3 -B -m unittest discover -s .agents/skills/testing-skill/tests
Windows PowerShell: py -3 -B -m unittest discover -s .agents\skills\testing-skill\tests
```

For checker changes, run its CLI on representative source and a tracked diff. For package changes, read back the built or installed package. Run the native example for changes to its behavior or assets.
