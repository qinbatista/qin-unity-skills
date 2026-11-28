---
name: unity-code-skill
description: Route mixed Unity requests and changes to this Skill pack. For C# use the code routes for gameplay architecture, lifecycle, Jobs, GC, serialization, local and cloud saves, SDKs and source checks, including ordinary C# outside Unity.
---

# Unity Code

**Project rules come first.** Follow the project's rules and registered workflows. Use the [standalone CLI gate](references/unity-cli.md#standalone-cli) for Unity work when permitted and no project or registered shared owner governs Unity CLI.

Use [Unity Tech Explain](../unity-tech-explain-skill/SKILL.md) when explaining how a Unity Skill, feature or code path works.

Treat the Skill's methods as adaptable defaults. Use the approach that best fits the task and [share reusable improvements](references/contributing.md#submit-a-skill-change). Before finishing Unity work, [review the affected runtime costs](../unity-performance-skill/SKILL.md#review-every-unity-task).

For a mixed or unfamiliar Unity task open the [pack task menu](references/global-skill-routing.md#choose-by-task) first. For C# read the selected headings by link or [section reader](scripts/read_topic.py). Audit any Unity image touched via [Assets](../unity-assets-skill/SKILL.md#admit-artwork-and-textures).

For linked gameplay use `explain game-code-logic`, or `explain yofa-plugin` for the provider integration. Bind the owning type, callers and supported C# version. For ordinary C# outside Unity use the style and checker with the project's own runtime.

## Choose an operation

| Operation | Read for this task |
| --- | --- |
| Fix or extend this Skill pack, including findings from a local test | [Submit a Skill change](references/contributing.md#submit-a-skill-change). |
| Plan folders or an existing game's feature | [Existing-game architecture](references/unity-cli.md#existing-game-architecture), then [Controller, Manager and data ownership](references/unity-game-code-structure-design.md#ownership) for a code design. |
| Design or audit ScriptableObject fields, shared state, keyed records or local and online schema changes | Follow the [ten data ownership and compatibility checks](references/scriptableobject-data-contracts.md#design-and-review-a-data-owner) and the [spell and wallet examples](references/scriptableobject-data-contracts.md#spell-catalog-and-player-wallet). |
| Route a C# property setter to local storage, a persistent account or a realtime match | Choose a [storage mode](references/so-data-controller.md#choose-one-mode-per-domain) and [wire the property and channel](references/so-data-controller.md#copy-and-call-the-template). |
| Define a scene Singleton or maintain a legacy ScriptableObject save | Use the [Singleton definition and Manager example](references/unity-game-code-structure-design.md#singleton-managers) and the [local save base and data example](references/unity-lifecycle-and-serialization.md#scriptableobject-local-files). Both include portable C# source. |
| Save ScriptableObject data for an offline-capable game with optional cloud sync | [Local-first ScriptableObject cloud saves](references/local-first-cloud-saves.md#implement-a-local-first-save) pairs the local JSON with a replaceable cloud adapter. |
| Write or review C# | [Style and method boundaries](references/unity-csharp-rules.md#c-style) and the [source check](#c-source-checks). Call the real owner directly. |
| Choose a callback, handle reentry or edit serialized state | [Frame cadence](references/unity-lifecycle-and-serialization.md#frame-callbacks), [async ownership](references/unity-lifecycle-and-serialization.md#async-and-main-thread-safety) or [serialization](references/unity-lifecycle-and-serialization.md#serialization-and-authored-data), according to the change. |
| Schedule Jobs or move Controllers in batches | [Scheduling and reuse](references/runtime-resources-and-gc.md#schedule-data-and-bound-reuse). For centralized movement the Job writes root positions and the Manager publishes one read-only batch after completion when Controllers consume it. [Completed Job batches](references/unity-game-code-structure-design.md#completed-job-batches) |
| Reduce hot-path allocations or retire assets and pools | For GC use [recurring operations](references/allocation-and-ownership-patterns.md#recurring-managed-work) and [calibrated measurement](references/runtime-resources-and-gc.md#measure-a-recurring-path). For leaks use [acquisition and release](references/runtime-resources-and-gc.md#own-acquisition-and-release). |
| Integrate an SDK or handle a purchase | [Provider boundary](references/unity-service-integration.md#stable-boundary), then [dependencies](references/unity-service-integration.md#transfer-into-a-receiving-game) for a transfer or [durable side effects](references/unity-service-integration.md#purchase-and-durable-side-effects) for transactions. |

Preserve serialized fields, public contracts and execution order. Verify the actual entry and affected failure, teardown or ordering case.

## C# source checks

Run the [packaged C# checker](references/unity-csharp-rules.md#run-the-source-checker) on the selected files or added Git lines. Review findings against compiler and runtime behavior.
