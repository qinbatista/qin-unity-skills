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
