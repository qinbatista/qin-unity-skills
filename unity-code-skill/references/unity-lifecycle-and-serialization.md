# Unity C# Lifecycle And Serialization

Use this reference for Unity callbacks, frame timing, async work and serialized bindings.

## Callback ownership

- Use `Awake()` for state and component references owned by the same object. Make initialization idempotent when domain reload, prefab reuse, or explicit reinitialization can repeat it.
- Use `OnEnable()` and `OnDisable()` as a symmetric pair for subscriptions, callbacks, input actions, and enable-scoped resources. Do not subscribe twice or rely on `OnDestroy()` to repair an `OnDisable()` leak.
- Use `Start()` only for work that genuinely depends on another object's completed `Awake()` or on an explicitly initialized service. Do not rely on accidental script execution order; configure the order or expose an explicit initialization dependency when order is contractual.
- Stop owned coroutines, cancel owned asynchronous operations, release native or pooled resources, and detach long-lived callbacks at the lifecycle boundary that acquired them.

## Frame callbacks

Trace the operation through its callees. Before keeping frame work, name its changing input, the required cadence or phase, the visible consequence of a skipped step and its stop condition.

| What makes the result change | Where to run the work |
| --- | --- |
| Initialization, binding or configuration | Resolve stable components and set material values once at initialization or when their owner changes. |
| State change, discrete input or completion | Call from the owning mutation or its existing callback. Read current state on attach and defer only the work that requires a later phase. |
| Action gate, deadline or countdown | Check admission on request. Use the owning clock for deadlines and refresh text when its displayed value changes. Preserve pause and resume. |
| Ordered batch | Prepare ordering when inputs change and advance only due or active entries in its owner. |
| Continuous input, movement, physics or late presentation | Use `Update` for required frame samples, `FixedUpdate` for fixed-step physics and `LateUpdate` for work that consumes this frame's earlier writes. Preserve the project's actual phases. |

Decide admission before enabling a frame callback, coroutine or repeating schedule. An already satisfied request with no required retrigger effect must leave that work idle and skip unchanged writes. Keep loops active only while work requires their cadence. Reuse stable results and avoid repeated allocation in active loops. Stop at completion, disable or retirement.

Verify the initial state, a real change and a settled request that keeps the loop idle. Inspect the settled path for assignments of identical values. Check stop or reentry. For timing or alignment, compare the producing state with the visible output in the same tick or frame and coordinate space.

## Async and main-thread safety

Apply [current-operation and lifetime ownership](https://github.com/qinbatista/qin-codex-skills/blob/master/code-skill/references/state-and-side-effects.md#let-only-the-current-operation-publish), including pooled reuse and late callbacks. The following rules add Unity-specific constraints.

- Await work or retain a named task with lifetime cancellation and observed completion and exceptions. A required `async void` Unity callback handles errors at that callback boundary.
- Resume Unity-object access on the Unity main thread and verify the owning lifecycle is current after an await.

## Serialization and authored data

- Prefer `[SerializeField] private ConcreteType _field;` for Inspector bindings. Do not expose public mutable fields merely for serialization.
- Preserve serialized field compatibility. Use Unity's supported rename migration when an existing serialized name changes; do not silently orphan prefab or scene data.
- Bind shared values and data operations through their domain SO. Start mutable session state from authored defaults and declare the local and online fields explicitly. Apply the [SO design and compatibility checks](scriptableobject-data-contracts.md#design-and-review-a-data-owner).

For an authored `.asset` change in Editor tooling, edit the existing asset with `SerializedObject`, call `ApplyModifiedProperties()`, then `AssetDatabase.SaveAssetIfDirty(asset)` when the tool owns the save. For direct field edits, call `Undo.RecordObject` before the change and `EditorUtility.SetDirty` before saving. Reimport and read the serialized field from the same asset. Keep these APIs in an Editor assembly.

## ScriptableObject local files

Use a private serialized backing field and a public property. The LocalFile mode restores the selected slot on first access and commits the complete domain when a property changes. A dedicated legacy file name keeps earlier default-slot saves readable. See the [storage template](so-data-controller.md#copy-and-call-the-template).

```csharp
using UnityEngine;

public sealed class SOAudioPreferences : SODataController
{
    [SerializeField] private float _volume = 1f;
    public override SODataMode? RequiredMode => SODataMode.LocalFile;
    protected override string LegacyFileName => "audio-preferences.json";
    public float Volume { get => Read(ref _volume); set => Write(ref _volume, Mathf.Clamp01(value)); }
}
```

The [Manager](unity-game-code-structure-design.md#singleton-managers) creates a runtime SO. Assigning `settings.Volume = 0.75f` commits the snapshot before the setter returns.

## Parallel work

Take immutable snapshots on the main thread, parallelize pure data work, and apply the results on the main thread. Keep Unity objects, scenes, transforms, physics, Editor APIs, and frame timing on their proper thread and phase. Use the project's existing Tasks or Job System and preserve its ownership boundaries.
