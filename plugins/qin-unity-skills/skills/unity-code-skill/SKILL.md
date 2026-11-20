---
name: unity-code-skill
description: Write, fix, refactor, or review Unity C# code, including gameplay ownership, lifecycle, serialization, Jobs, pooled resources, managed allocations, and SDK integration. The common C# style and source checker also apply to C# work outside Unity when compatible with its runtime.
---

# Unity Code

Locate the actual project and its code owner before changing files. For a YoFaAssets-linked project, read the registered owner's current `explain task-entry` and relevant topics, including `explain game-code-logic` for game code. Apply the shared source and the receiving game's system contract first. Use that project's native CLI workflow for Unity control and verification.

1. Read the [common C# style](references/unity-csharp-rules.md). Identify the affected type or method, its callers, and the actual project and language versions.
2. Select the references that match the work. Apply the [frame callback rules](references/unity-lifecycle-and-serialization.md#frame-callbacks) when adding, changing, or reviewing work reached from `Update()`, `FixedUpdate()`, or `LateUpdate()`. Preserve public and serialized contracts, execution order, cancellation, and the owning lifecycle.
3. Verify the affected behavior while working. Use the real consumer and cover a relevant failure, teardown, or ordering case when it can invalidate the change. Keep the check scoped to the requested result.

| Work | Reference |
| --- | --- |
| Gameplay ownership, data flow, patterns | [Game code structure](references/unity-game-code-structure-design.md) |
| Frame timing, events, async, thread affinity, serialization, component lifetime | [Lifecycle and serialization](references/unity-lifecycle-and-serialization.md) |
| Jobs and native data, Addressables leases, pools, resource release, or recurring allocations | [Runtime resources and GC](references/runtime-resources-and-gc.md) |
| Initialization, cloud APIs, SDKs, optional providers | [Service integration](references/unity-service-integration.md) |

## C# source checks

Run the source checker from the receiving project with Python 3 and Git available. Resolve `<skill-folder>` to the loaded Unity Code Skill. In a Git checkout, `--diff-from HEAD` filters findings to lines added since the selected revision. Omit the flag to check the supplied files in full. The checker scans C# source and skips comments and supported literal forms. Unity or the receiving toolchain owns compilation.

```text
macOS/Linux: python3 -B "<skill-folder>/scripts/csharp_rule_guard.py" --diff-from HEAD <changed-files>
Windows PowerShell: py -3 -B "<skill-folder>\scripts\csharp_rule_guard.py" --diff-from HEAD <changed-files>
```

The checker accepts `.cs` paths, emits JSON, and exits with status 1 when it reports a violation. Use the game's compilation or focused runtime case when the claim depends on execution.
