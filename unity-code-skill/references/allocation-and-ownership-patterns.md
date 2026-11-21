# Allocation and ownership patterns

Read the actual caller, affected workload, installed API and current phase before selecting a pattern. Keep input order, timing, ownership and visible output intact. Treat startup, first-use growth, reuse, teardown and external calls as separate lifecycles.

## Recurring managed work

| Source of work | Use the existing owner's appropriate boundary |
| --- | --- |
| Repeated coroutine waits | Reuse a stateless fixed-step wait. Cache an operation's constant wait when its timing contract permits, and rebuild it for a changed duration. Preserve randomized waits, realtime semantics and the first yield phase. |
| Starting comparable transitions | Claim the current transition before a first yield or reentrant callback. Compare identity before expensive equality where valid. Apply the original synchronous first step and exact endpoint once. Continue only for an unfinished transition. |
| Interpolated arrays or array-returning getters | Capture stable inputs and reuse correctly sized output capacity for an operation. Use independent mutable buffers for simultaneous transitions. Verify endpoints, key counts and live redirection. |
| Mutable scratch and collection copies | Reuse owned capacity. If functions can reenter or mutate the source, give each call a separate rented snapshot and return it in guaranteed cleanup. Keep the lease until a retaining receiver finishes. Preserve ordered contents, null and empty behavior. |
| Captured search lambdas, boxing and `params` expansion | Use compatible typed APIs or a clear direct search within the existing owner. Verify overload selection, equality, enum width and signedness, defaults and special floating-point values where they affect results. |
| Repeated string keys, formatting or text sets | Prepare stable keys at their owner and use the same values on registration, publication and retirement. Refresh formatted text when its input or visible phase changes. Exercise every contributing input and the actual text. |
| Binding and object discovery | Cache stable component queries and references at the ownership boundary. Keep a binding to the same publisher when only its slot changes. On owner replacement, detach the exact prior registration and bind once to the successor. |
| Per-request or per-frame managed growth | Allocate only capacity with a real workload bound. Preserve valid growth or exhaustion semantics and observe the first allocation as well as warmed use. Count retained capacity independently. |

For stable tag equality use the engine's typed tag check on the same target. Cache a stable ECS query with its owning World and retire it with that World. Preserve the matching entity set and budget any temporary native query results. Value construction can be allocation-free in managed code while a captured, boxed or native representation still has a distinct cost.

## Resource release and ordering

- Read the actual acquisition and consumer. A Material obtained as a renderer-specific instance has native lifetime. Cache and destroy exactly that owned instance. Preserve shared assets and other live renderers. Compare first use and teardown after the owning resource's actual retirement point.
- For Addressables, a copied valid handle can refer to an operation after this owner's lease has already ended. Track the exact acquisition or registration. Remove it before a reentrant release or callback. Cover owner-first and framework-first completion where the installed package can release or destroy the operation independently.
- For cancellation, reentrant callbacks and partial construction, use [acquisition and release](runtime-resources-and-gc.md#own-acquisition-and-release). Check built content when on-demand dependencies or packaging affect the claim.

## Measure the claimed result

Use the [recurring-path check](runtime-resources-and-gc.md#measure-a-recurring-path). Attribute a sampled iterator step to that step. Use actual frames for frame-cost claims.
