# Unity Game Code Structure

Apply these rules to gameplay implementation, architecture proposals and code examples. Use the receiving project's current feature contract and its declared exceptions. Editor tooling and tests follow their own owners.

## Ownership

- Assign each gameplay runtime responsibility to a named `XXController` or `XXManager` unless the project contract declares an exception. Name the sole writer of each gameplay state and identify its real callers. Enums, records, interfaces, ScriptableObjects and Jobs keep their declared data or computation roles.
- A Controller owns one GameObject-bound responsibility, its bindings and cleanup. Keep a self-contained gameplay component's rules, tick and reset with that Controller. Add separate Controllers for independently owned responsibilities with real consumers.
- Use a feature Manager when actual consumers need shared lifecycle or group coordination. Keep each Controller's local presentation and bindings with that Controller. Input and UI send requests to the state owner and read its results.
- Batch repeated pure decoration inside VFX. Use gameplay objects for independent identity or interaction, Jobs for needed parallel computation, and pools for actual bounded reuse. See [runtime resources and GC](runtime-resources-and-gc.md).

## Data

- Managers serialize only the feature's ScriptableObject configuration references. Controllers keep their own bindings and consume each tunable from its existing authored owner.
- Keep fields with the same identity, writer and lifetime in one record. Keep local runtime state with its owner. Create an unmanaged Job snapshot only for an actual execution boundary, using only the fields that Job reads or writes.
- Keep immutable constants, protocol keys, shader names and enums in code. Store authored configuration in its existing asset owner.

## Completed Job batches

Use this default in gameplay designs, implementations and examples when Controllers act on one Job's batch of results. Follow the receiving project's established event and slot contract.

1. The Manager owns the native container and Job lifetime. At the required cadence, sample shared input, schedule the Job, call `Complete()` and publish once through a typed event such as `Action<NativeArray<EnemyJobData>.ReadOnly>`. Pass the existing result collection as a read-only view that subscribers use synchronously during the callback.
2. Each Controller subscribes for its active or declared lifetime and detaches from the same publisher at retirement. It reads its assigned slot directly and applies the result to its own component. Keep per-entity sampling, result traversal and dispatch loops out of the Manager's frame callbacks and their helpers in this pattern, including `ForEach` and LINQ passes. Each receiver performs only its own indexed lookup.
3. Keep the collection, slot mapping and backing storage stable until dispatch returns. The Manager applies any structural changes afterward. Reuse the existing view and stable subscriptions across ticks to avoid copying the list or allocating a relay collection. A completion event still calls each subscriber.

Keep required per-object input sampling at the existing Unity-to-Job boundary. When physics owns the current position, let each Controller submit the raw position of its own Rigidbody to its assigned slot before scheduling. Keep one-time construction and teardown traversal with the owner that creates and releases the group.

## Patterns

Use patterns only for a demonstrated coordination or lifecycle need:

| Pattern | Decision trigger |
| --- | --- |
| Factory | Multiple product variants need a shared construction policy |
| Object Pool | Bounded reuse is needed or repeated creation/destruction is measured |
| State | Transitions outgrow a small enum/switch or existing Animator graph |
| Command | Queueing, replay, undo, serialization, or deferred execution |
| Observer | One publisher has multiple independent consumers |
| Prototype | Copy semantics beyond prefab/Addressables instantiation |
| Singleton | A real feature-wide lifetime and coordination owner |

Keep direct calls when they already meet the requirement. A public method or event needs a current caller or an actual framework or compatibility contract. A new assembly definition needs a concrete dependency, isolation or Editor/test boundary.

## Continuity

Use the receiving contract for paths, bootstrap and explicit exceptions. Preserve serialized and public compatibility. Keep stable routes in project guidance and verified facts in project memory.
