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

Use this default when Controllers consume state from a gameplay Job. Apply it in gameplay designs, implementations and examples. Follow the receiving project's established movement backend, event and slot contract.

1. For centralized movement, the movement Job owns the trajectory calculation and the position write. For transform-driven GameObjects, bind the root Transforms via `TransformAccessArray` and set `TransformAccess.position` in `IJobParallelForTransform.Execute`. Keep managed physics API calls on their supported thread with the declared physics owner when the game's movement backend requires them. Keep any such operation outside the completed-state event.
2. The Manager owns native storage, membership and Job lifetime. At the required cadence, sample shared inputs, schedule the Job and call `Complete()`. When Controllers consume the resulting state, publish the existing list once through a typed event such as `Action<NativeArray<EnemyJobData>.ReadOnly>`. Subscribers borrow the read-only view for the synchronous callback.
3. Each Controller reads its assigned slot and uses the state for its own animation, presentation or local behavior. Keep position writes in the movement owner. Pair subscriptions with the Controller's active or declared lifetime and detach from the same publisher. Keep the slot mapping and backing storage stable throughout dispatch and apply structural changes afterward.

Keep per-entity movement, sampling and dispatch loops out of the Manager's frame callbacks and their helpers for this pattern, including `ForEach` and LINQ passes. Each Controller reads one slot. Keep required one-time group construction and teardown with the owner. Verify that roots continue moving with the presentation subscription disabled and that the completed event still drives the Controller's intended state when it is subscribed.

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
