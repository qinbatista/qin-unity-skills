# Runtime code and asset lifetime

## Frame work and managed allocations

Trace each changed callback through its callees. Count callbacks per frame, elements visited per call, and the work per element at peak population. A single linear pass over moving actors can be exactly the work required. A pass that sorts every actor's neighbors every frame can dominate CPU time even when it allocates zero bytes.

| Evidence in the affected path | Change to try | Behavior to check |
| --- | --- | --- |
| Stable data gets rediscovered, sorted or formatted every tick | Bind at setup, rebuild on membership or input changes, and retire the cache with its owner. For an ammo counter, draw the current value when the HUD opens and refresh on ammo or display changes. | Attach after a change, disable and reopen, replace the source, and compare the displayed value. |
| Each active object visits the whole population | For a proximity query, narrow candidates by cell or layer, then do the exact range test. For continuous movement, update only the active set at its required cadence. | Cross a cell boundary, spawn and remove mid-tick, and check target order and ties. Count visited candidates alongside CPU time. |
| Allocation stacks point to a captured predicate, boxing, string construction, array-returning getter, or a fresh list | Remove the measured allocation. For example, fill a caller-owned result buffer with adequate capacity and consume only its valid range. A reentrant caller needs independent scratch. | Check empty, typical and overflow cases, repeat after warmup, and compare ordered results and allocated bytes per call. |

Keep input edges, fixed-step simulation, and presentation work in their required phases. Budget or stagger optional work only within its allowed response delay. For precise patterns and allocation controls, use [Unity Code's frame callbacks](../../unity-code-skill/references/unity-lifecycle-and-serialization.md#frame-callbacks), [recurring operations](../../unity-code-skill/references/allocation-and-ownership-patterns.md#recurring-managed-work), and [calibrated measurement](../../unity-code-skill/references/runtime-resources-and-gc.md#measure-a-recurring-path). [Unity GC guidance](https://docs.unity3d.com/2022.3/Documentation/Manual/performance-garbage-collection-best-practices.html)

## Spawn and retire

Use a persistent instance for an object that lives for the encounter. For repeated gameplay spawns, count peak simultaneous users and creation/destruction spikes before choosing a pool. Size prewarming for the observed burst and the target's memory budget. Count active objects, prewarmed spares and their nested resources. Unity's `ObjectPool<T>.Get()` creates on empty, and `maxSize` limits how many returned objects the pool keeps. A fixed-capacity design therefore needs admission before `Get()`. [Unity ObjectPool API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Pool.ObjectPool_1.html)

For a projectile, bind the new owner, target and velocity before activation can invoke gameplay callbacks. On return, stop timers, detach listeners, reset hit state and clear trails. Tie delayed completions to the specific use of that slot. Exercise a burst at capacity and one above, immediate reuse while an old callback is pending, and scene exit with outstanding projectiles. Verify the actual exhaustion rule, hit count, construction count and retained memory. Admission failures must remain visible to the owner of required gameplay.

## Load and release content

Start from the project's asset policy. A serialized reference is simple for a small asset always needed with its owner. A catalog of optional rooms, enemies or skins can retain their entire dependency graph at startup. Keep lightweight selection data in the catalog and load the selected content through the project's provider when needed. For independent residency or delivery, use the project's provider and its Editor-compatible package version. Author Addressable entries and groups in the content pipeline. At runtime, load the published key or asset reference and reuse its content across spawns. Inspect built content for unintended dependencies through prefabs, materials, atlases and bundles.

Choose a loading window before the asset's gameplay deadline. Coalesce repeated requests for the same content while preserving each consumer's lifetime. Time download, deserialization, instantiation and activation separately when first use hitches. Activation and prefab construction can still consume main-thread time after an asynchronous load. Compare cold entry, the first action, warm reuse and re-entry. Look for the same bundle unloading and reloading between adjacent uses. [Addressables memory management](https://docs.unity3d.com/Packages/com.unity.addressables@2.11/manual/memory-assets.html)

| Acquisition | Retirement |
| --- | --- |
| The pool calls `LoadAssetAsync` once and clones the prefab with `Object.Instantiate`. | Keep the load handle while any clone, including an idle pooled clone, can use its dependencies. Destroy the clones and release the asset handle once they have retired. |
| The feature calls `Addressables.InstantiateAsync` for each instance. | Keep each successful instance handle until its final retirement. Call `ReleaseInstance(handle)` once per instance. Returning it to a live pool keeps its lease. |

Exercise duplicate requests, failure, retry and a load completing after scene exit or owner replacement. Only the current request may publish. Release each acquired operation according to the installed provider's contract, including failure and automatic-release behavior. Check operation counts and live dependencies after teardown, then inspect actual resident memory separately. [Addressables instantiation API](https://docs.unity3d.com/Packages/com.unity.addressables@2.11/api/UnityEngine.AddressableAssets.Addressables.InstantiateAsync.html) · [Unity Code's acquisition and release rules](../../unity-code-skill/references/runtime-resources-and-gc.md#own-acquisition-and-release)
