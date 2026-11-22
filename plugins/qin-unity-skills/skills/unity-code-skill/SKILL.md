---
name: unity-code-skill
description: Start here for mixed or unclassified Unity requests and route to the pack's task menu. For C# use the code routes for gameplay architecture, lifecycle, Jobs, GC, serialization, SDKs and source checks, including ordinary C# outside Unity.
---

# Unity Code

**Project rules come first.** Follow the project's rules and registered workflows. Use the [standalone CLI gate](references/unity-cli.md#standalone-cli) for Unity work when permitted and no project or registered shared owner governs Unity CLI.

For a mixed or unfamiliar Unity task open the [pack task menu](references/global-skill-routing.md#choose-by-task) first. For C# read the selected headings by link or [section reader](scripts/read_topic.py). Audit any Unity image touched via [Assets](../unity-assets-skill/SKILL.md#admit-artwork-and-textures).

For linked gameplay use `explain game-code-logic`, or `explain yofa-plugin` for the provider integration. Bind the owning type, callers and supported C# version. For ordinary C# outside Unity use the style and checker with the project's own runtime.

## Choose an operation

| Operation | Read for this task |
| --- | --- |
| Plan folders or an existing game's feature | [Existing-game architecture](references/unity-cli.md#existing-game-architecture), then [Controller, Manager and data ownership](references/unity-game-code-structure-design.md#ownership) for a code design. |
| Write or review C# | [Style and method boundaries](references/unity-csharp-rules.md#c-style) and the [source check](#c-source-checks). Call the real owner directly. |
| Choose a callback, handle reentry or edit serialized state | [Frame cadence](references/unity-lifecycle-and-serialization.md#frame-callbacks), [async ownership](references/unity-lifecycle-and-serialization.md#async-and-main-thread-safety) or [serialization](references/unity-lifecycle-and-serialization.md#serialization-and-authored-data), according to the change. |
| Schedule Jobs or move Controllers in batches | [Scheduling and reuse](references/runtime-resources-and-gc.md#schedule-data-and-bound-reuse). For centralized movement the Job writes root positions and the Manager publishes one read-only batch after completion when Controllers consume it. [Completed Job batches](references/unity-game-code-structure-design.md#completed-job-batches) |
| Reduce hot-path allocations or retire assets and pools | For GC use [recurring operations](references/allocation-and-ownership-patterns.md#recurring-managed-work) and [calibrated measurement](references/runtime-resources-and-gc.md#measure-a-recurring-path). For leaks use [acquisition and release](references/runtime-resources-and-gc.md#own-acquisition-and-release). |
| Integrate an SDK or handle a purchase | [Provider boundary](references/unity-service-integration.md#stable-boundary), then [dependencies](references/unity-service-integration.md#transfer-into-a-receiving-game) for a transfer or [durable side effects](references/unity-service-integration.md#purchase-and-durable-side-effects) for transactions. |

Preserve serialized fields, public contracts and execution order. Verify the actual entry and affected failure, teardown or ordering case.

## C# source checks

Run the [packaged C# checker](references/unity-csharp-rules.md#run-the-source-checker) on the selected files or added Git lines. Review findings against compiler and runtime behavior.
