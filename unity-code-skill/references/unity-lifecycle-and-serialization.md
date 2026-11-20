# Unity C# Lifecycle And Serialization

Load this category only when the Unity C# task materially touches MonoBehaviour callbacks, scene or prefab lifetime, components, events, coroutines, async work, physics timing, or serialized data. The mandatory common writing rules remain in [`unity-csharp-rules.md`](unity-csharp-rules.md).

## Callback ownership

- Use `Awake()` for state and component references owned by the same object. Make initialization idempotent when domain reload, prefab reuse, or explicit reinitialization can repeat it.
- Use `OnEnable()` and `OnDisable()` as a symmetric pair for subscriptions, callbacks, input actions, and enable-scoped resources. Do not subscribe twice or rely on `OnDestroy()` to repair an `OnDisable()` leak.
- Use `Start()` only for work that genuinely depends on another object's completed `Awake()` or on an explicitly initialized service. Do not rely on accidental script execution order; configure the order or expose an explicit initialization dependency when order is contractual.
- Stop owned coroutines, cancel owned asynchronous operations, release native or pooled resources, and detach long-lived callbacks at the lifecycle boundary that acquired them.

## Frame callbacks

Trace each affected operation through its callees. Keep an operation on a frame or physics step only when its inputs or clock must be sampled at that cadence or a consumer needs its result in that phase. Identify the existing owner, what would break if the operation skipped a step, and when the work ends.

| What makes the result change | Where to run the work |
| --- | --- |
| Initialization, binding, or a configuration change | Apply stable references and material settings at initialization or when the binding or configuration changes. |
| A state change, discrete input, or operation completion | Run the decision or refresh at the owner's mutation or through its existing callback. Mark pending work when the consumer needs to apply it once in a later phase. Read current state when a consumer attaches or reopens. |
| An action gate, deadline, or countdown | Check the gate when the action is requested. Use the owning clock for work due at a deadline and refresh countdown text when its displayed value changes. Preserve pause and resume behavior. |
| An ordered batch of work | Prepare candidates and ordering when the batch starts or its inputs change. Advance only due or active entries in the existing batch owner. |
| Continuous input, movement, physics, or presentation that depends on frame order | Use the project's actual phase. Use `Update()` for input or motion that needs a fresh per-frame sample, `FixedUpdate()` for Unity-driven fixed-step physics, and `LateUpdate()` when presentation needs the result of earlier frame writers. Preserve explicit simulation and presentation phases owned by the project. |

Apply the same timing test to helpers, coroutines, and scheduled polling. Restrict necessary loops to active work and skip repeated calculations and writes when the inputs and result are unchanged. Use the existing owner to stop work at completion, disable, or retirement and preserve the initial update and restart behavior.

Verify the real entry at initialization, through an actual change, and across unchanged frames and stop or reentry. Check that trigger-driven work stays idle between changes and the visible result updates at the required time. For timing or alignment changes, compare the producing state and its visible output in the same tick or rendered frame and coordinate space, including the affected transition.

## Async and main-thread safety

Apply [current-operation and lifetime ownership](https://github.com/qinbatista/qin-codex-skills/blob/master/code-skill/references/state-and-side-effects.md#let-only-the-current-operation-publish), including pooled reuse and late callbacks. The following rules add Unity-specific constraints.

- Do not write `_ = SomeTask()` or use `async void` for background ownership. Unity event callbacks are the narrow `async void` exception; catch and surface their failures at that boundary.
- Await work when the caller owns completion. Otherwise store the `Task` in a named field, attach cancellation to the owning object's lifetime, and observe completion and exceptions.
- Resume Unity-object access on the Unity main thread. After an await, confirm the owning lifecycle is still active before touching destroyed objects or scene state.

## Serialization and authored data

- Prefer `[SerializeField] private ConcreteType _field;` for Inspector bindings. Do not expose public mutable fields merely for serialization.
- Preserve serialized field compatibility. Use Unity's supported rename migration when an existing serialized name changes; do not silently orphan prefab or scene data.
- Treat ScriptableObjects as authored/shared configuration unless the project explicitly owns mutable runtime state there. Do not use a mutable ScriptableObject as an accidental save file or hidden singleton.
- Cache stable component references outside per-frame callbacks. Do not repeat `GetComponent`, allocation, reflection, LINQ enumeration, or string construction every frame when the same value can be retained safely.

## Parallel work

Take immutable snapshots on the main thread, parallelize pure data work, and apply the results on the main thread. Keep Unity objects, scenes, transforms, physics, Editor APIs, and frame timing on their proper thread and phase. Use the project's existing Tasks or Job System and preserve its ownership boundaries.
