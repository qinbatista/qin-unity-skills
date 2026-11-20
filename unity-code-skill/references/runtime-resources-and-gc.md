# Unity runtime resources and GC

Use this reference when changing Jobs, native data, Addressables, materials, pooled objects, recurring allocations, or teardown. Bind the installed package versions and the feature's actual lifecycle and runtime owner.

## Own acquisition and release

- Identify the exact acquisition, owner, borrowers and retirement point for a handle, instance, material, native allocation, subscription or task. Release each owned acquisition once through its matching API. Retire borrowers before releasing the backing resource, or give a long-lived consumer its own lease.
- Retire registrations and request ownership before operations or callbacks can reenter. Stop new work and complete or cancel scheduled work at the appropriate boundary. Preserve pending cleanup for late results and unwind acquired resources if initialization fails.
- Bind asynchronous output to the current request and lifetime. Check identity before publishing results, readiness or failure, including after callbacks that may reenter. An obsolete result can release its own resource. Keep the newer request and its resources intact.
- Keep on-demand content behind lightweight identifiers when readiness allows it. Inspect serialized Prefabs, data assets, materials and bundles for hard references that can keep the content resident. Hold the backing lease until its last dependent object retires.

## Schedule data and bound reuse

- Snapshot required Unity object inputs on the main thread. Put parallel calculations in the existing Job or data owner and apply results through the declared consumer. Complete scheduled dependencies before reading, rebuilding or disposing owned native storage. Retire borrowers with their producer or use the project's explicit rebind protocol.
- Keep pool capacity, admission, exhaustion and reset explicit. Clear per-use timers, targets, subscriptions and asynchronous state during return or acquisition. Bound capacity based on actual concurrent use and preserve the lifetime of shared dependencies.

## Measure a recurring path

1. Identify the method and trigger, then follow its calls to the operation that allocates. Inspect likely sources such as closure capture, boxing, temporary collections, array-returning APIs and repeated text formatting.
2. Reuse buffers, delegates or cached results at their real owner when doing so preserves ordering, invalidation, cancellation and reentrancy. Give simultaneous operations independent mutable scratch. Keep startup, first use and steady reuse as distinct measurements.
3. Run the same workload and runtime before and after the change. Verify the relevant failure and lifetime case, then report the measured metric and scope. Managed allocations, native memory, retained resources, frame time and package size require their own evidence.
