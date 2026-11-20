# Unity runtime resources and GC

Use this reference when changing Jobs, native data, Addressables, materials, pooled objects, recurring allocations, or teardown. Bind the installed package versions and the feature's actual lifecycle and runtime owner.

Read [allocation and ownership patterns](allocation-and-ownership-patterns.md) for the affected operation and its regression case. Use the frame-callback section of [lifecycle and serialization](unity-lifecycle-and-serialization.md#frame-callbacks) to decide whether recurring work is needed at its current cadence.

## Own acquisition and release

- Identify the exact acquisition, owner, borrowers and retirement point for a handle, instance, material, native allocation, subscription or task. Release each owned acquisition once through its matching API. Retire borrowers before releasing the backing resource, or give a long-lived consumer its own lease.
- Retire registrations and request ownership before operations or callbacks can reenter. Stop new work and complete or cancel scheduled work at the appropriate boundary. Preserve pending cleanup for late results and unwind acquired resources if initialization fails.
- Bind asynchronous output to the current request and lifetime. Check identity before publishing results, readiness or failure, including after callbacks that may reenter. An obsolete result can release its own resource. Keep the newer request and its resources intact.
- Keep on-demand content behind lightweight identifiers when readiness allows it. Inspect serialized Prefabs, data assets, materials and bundles for hard references that can keep the content resident. Hold the backing lease until its last dependent object retires.

## Schedule data and bound reuse

- Snapshot required Unity inputs on the main thread. Use one Job/data owner for work that shares its slot, schedule and write-back boundary. Complete outstanding work before reading, rebuilding or disposing native storage.
- Size pools and scratch capacity to actual concurrent use. Define admission and exhaustion. On return, clear per-use timers, targets, listeners, callbacks and visible state. Keep shared dependencies leased until the last borrower retires.

## Measure a recurring path

Trace the actual hot path to the allocating operation. Reuse capacity only with a current consumer and a matching release boundary. Measure the same workload before and after, separating first use from warm repeats and managed allocations from native objects and retained memory. Verify cleanup and reentry when they can invalidate the result.
