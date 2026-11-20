# Unity Game Code Structure

Use for Unity gameplay ownership and data flow. Read the current feature and its local contract. Editor tooling and tests follow their actual owners.

## Ownership

- Gameplay runtime behavior belongs to named `XXController` or `XXManager` roles. Avoid a third helper MonoBehaviour role when one of these already owns the responsibility. Data records, enums, interfaces, and ScriptableObjects are not runtime-behavior roles.
- A Controller owns one GameObject-bound local responsibility, including its bindings and cleanup. Multiple Controllers may share an object when their concerns are independently named and changeable.
- Use one feature Manager when actual callers need shared lifecycle or group coordination. Bind its lifetime to the feature. After handoff each Controller owns its local behavior.
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

Keep direct calls when they already meet the requirement. A public method or event needs a current caller or an actual framework or compatibility contract.

## Continuity

Use the receiving contract for paths, bootstrap and explicit exceptions. Preserve serialized and public compatibility. Keep stable routes in project guidance and verified facts in project memory.
