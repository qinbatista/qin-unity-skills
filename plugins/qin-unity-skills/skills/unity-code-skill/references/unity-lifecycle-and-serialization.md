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
- Treat ScriptableObjects as authored/shared configuration unless the project explicitly owns mutable runtime state there. Do not use a mutable ScriptableObject as an accidental save file or hidden singleton.

For an authored `.asset` change in Editor tooling, edit the existing asset with `SerializedObject`, call `ApplyModifiedProperties()`, then `AssetDatabase.SaveAssetIfDirty(asset)` when the tool owns the save. For direct field edits, call `Undo.RecordObject` before the change and `EditorUtility.SetDirty` before saving. Reimport and read the serialized field from the same asset. Keep these APIs in an Editor assembly.

## ScriptableObject local files

For player-owned values, reuse the game's save base or copy [SOLocalData.cs](../assets/SOLocalData.cs) into its shared runtime assembly. This port of SpriteTamer's `SOLocalData` writes UTF-8 JSON under `Application.persistentDataPath`. Keep one live writer per file. Use a stable file name for each save and preserve the names, serialized fields and checksum format of existing saves. The original default file name is the concrete type name with no extension.

```csharp
using UnityEngine;

public sealed class SOAudioPreferences : SOLocalData
{
    [SerializeField] private float _volume;
    public float Volume => _volume;

    public override void SetPathName() => FileName = "audio-preferences.json";
    public override void InitialValue() => _volume = 1f;

    public void SetVolume(float volume)
    {
        float next = Mathf.Clamp01(volume);
        if (_volume == next) return;
        _volume = next;
        RequestAutoSave();
    }
}
```

The [Manager example](unity-game-code-structure-design.md#singleton-managers) creates this object once. In Play, `OnEnable` loads an existing file or calls `InitialValue()` and saves the first-use defaults. Keep `SetPathName()` a managed name assignment because `OnAfterDeserialize` can run on Unity's serialization thread. In Edit mode, loading or importing an asset leaves the player file alone. Editor code can call `LoadLocalData()` explicitly. For isolated Editor tests, set `AI_MANAGER_PERSISTENCE_ROOT` to an absolute task directory before creating the object.

`RequestAutoSave()` writes on an admitted change using unscaled time. Changes during the cooldown stay in memory until the next save. Call `SaveDataAtLocal()` at a required checkpoint, including the game's pause/suspend boundary when appropriate. `SaveWhenOwnerDestroyed()` flushes the last value during Play teardown. A save writes a sibling temporary file and replaces the committed file after serialization succeeds.

Persist value fields and stable IDs. Keep Prefab, scene and other `UnityEngine.Object` references in their authored owner. A type with a separate save schema can override `SerializeLocalData()` and `RestoreLocalData()` together. The paired prepare/restore hooks and before/after load hooks support existing subclasses. The default format preserves `_fileName`, `_timeStamp` and the lowercase SHA-256 of the pretty JSON with `_hashValue` empty. It also reads the original legacy wrapper containing one Base64-encoded JSON string. The next save writes plain JSON. When changing a shipped schema, validate and migrate an actual old file before writing the new layout.

Only an absent file initializes defaults automatically. A failed read or checksum mismatch leaves the file in place, restores the previous serialized values and blocks writes for that object. Retry `LoadLocalData()` after repair, or call `Clean()` when the caller explicitly wants to reset and persist defaults. Verify a first launch, a fresh instance reading a changed value, and a damaged file surviving a failed reload and teardown.

## Parallel work

Take immutable snapshots on the main thread, parallelize pure data work, and apply the results on the main thread. Keep Unity objects, scenes, transforms, physics, Editor APIs, and frame timing on their proper thread and phase. Use the project's existing Tasks or Job System and preserve its ownership boundaries.
