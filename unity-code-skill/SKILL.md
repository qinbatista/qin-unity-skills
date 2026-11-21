---
name: unity-code-skill
description: Explain or design Unity gameplay architecture and write, fix, refactor, or review Unity C# code, including runtime ownership, lifecycle, serialization, Jobs, pools, managed allocations, and SDK integration. The common C# style and source checker also apply outside Unity when compatible with its runtime.
---

# Unity Code

**Project rules come first.** Start with the target project's `AGENTS.md` and follow its order for applicable project Skills and registered shared workflows. This global Skill fills gaps. Use the [standalone CLI gate](references/unity-cli.md#standalone-cli) only where the project permits it and no project or registered shared owner governs Unity CLI.

Apply the [existing-game architecture workflow](references/unity-cli.md#existing-game-architecture) to questions and examples as well as implementation plans.

For linked game code, select `explain game-code-logic` through the registered workflow. Bind the owning type, actual callers and C# version through the project's system contract.

- Call the real owner directly. Keep a method boundary for a distinct responsibility, current reuse or a required contract. Apply the [C# style](references/unity-csharp-rules.md).
- Run discrete decisions at their request, state change or completion. Keep `Update`, `FixedUpdate` and `LateUpdate` for work that needs that cadence or phase. Apply the [lifecycle and frame rules](references/unity-lifecycle-and-serialization.md#frame-callbacks).
- Keep one authoritative state. Apply [runtime resources and GC](references/runtime-resources-and-gc.md) when changing Jobs, leases, pools, allocations or teardown.
- When Controllers consume the results of a gameplay Job, default to [one completed-batch event](references/unity-game-code-structure-design.md#completed-job-batches). Publish the existing result list as a read-only view after completion. Each Controller reads its own entry and applies it.
- Use [game code structure](references/unity-game-code-structure-design.md) for Controller, Manager and data ownership, and [service integration](references/unity-service-integration.md) for SDKs and providers.

Preserve serialized fields, public contracts and execution order. Verify the real entry and the affected failure, teardown or ordering case.

## C# source checks

From the receiving project, resolve `<skill-folder>` to the loaded Unity Code Skill. Use `--diff-from HEAD` for added Git lines or omit it for a whole-file check.

```text
macOS/Linux: python3 -B "<skill-folder>/scripts/csharp_rule_guard.py" --diff-from HEAD <changed-files>
Windows PowerShell: py -3 -B "<skill-folder>\scripts\csharp_rule_guard.py" --diff-from HEAD <changed-files>
```

The checker returns JSON and exits with status 1 for findings. Treat them as source review cues. Use the project's compiler and runtime for overload resolution, ownership and behavior.
