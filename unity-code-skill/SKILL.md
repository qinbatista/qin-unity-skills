---
name: unity-code-skill
description: Write, fix, refactor, or review Unity C# code, including gameplay ownership, lifecycle, serialization, Jobs, pooled resources, managed allocations, and SDK integration. The common C# style and source checker also apply to C# work outside Unity when compatible with its runtime.
---

# Unity Code

Bind the owning type, its actual callers and the C# version. In a YoFaAssets-linked game, apply the registered owner's `explain task-entry` and `explain game-code-logic` and the game's system contract first.

- Call the real owner directly. Keep a method boundary for a distinct responsibility, current reuse or a required contract. Apply the [C# style](references/unity-csharp-rules.md).
- Run discrete decisions at their request, state change or completion. Keep `Update`, `FixedUpdate` and `LateUpdate` for work that needs that cadence or phase. Apply the [lifecycle and frame rules](references/unity-lifecycle-and-serialization.md#frame-callbacks).
- Keep one authoritative state. Apply [runtime resources and GC](references/runtime-resources-and-gc.md) when changing Jobs, leases, pools, allocations or teardown.
- Use [game code structure](references/unity-game-code-structure-design.md) for Controller, Manager and data ownership, and [service integration](references/unity-service-integration.md) for SDKs and providers.

Preserve serialized fields, public contracts and execution order. Verify the real entry and the affected failure, teardown or ordering case.

## C# source checks

From the receiving project, resolve `<skill-folder>` to the loaded Unity Code Skill. Use `--diff-from HEAD` for added Git lines or omit it for a whole-file check.

```text
macOS/Linux: python3 -B "<skill-folder>/scripts/csharp_rule_guard.py" --diff-from HEAD <changed-files>
Windows PowerShell: py -3 -B "<skill-folder>\scripts\csharp_rule_guard.py" --diff-from HEAD <changed-files>
```

The checker returns JSON and exits with status 1 for findings. Treat them as source review cues. Use the project's compiler and runtime for overload resolution, ownership and behavior.
