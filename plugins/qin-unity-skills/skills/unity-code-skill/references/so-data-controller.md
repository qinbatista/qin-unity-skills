# ScriptableObject storage modes

## Choose one mode per domain

For an [SODataController](../assets/SODataController.cs) subclass, keep its fields, methods and normal gameplay assignments. Under **Game Data → Storage Profile**, create **Unity Cloud Save** for UGS or **Local or Custom** for another backend. Drag the same SO references used by gameplay into its Domains list. Each domain retains its own mode and stable storage ID. A game-owned SO with another base can use a separate **Game Data → Storage Controller** and the existing `AttachAsync` API.

A domain with a required storage policy overrides `RequiredMode`. Its Inspector displays the effective mode read-only. A separate controller must match that mode to attach. Ordinary domains inherit the editable `None` default.

| Mode | Example | When a value is saved and restored |
| --- | --- | --- |
| `None` (default) | Speed or attack tuning | Consumers share one runtime instance. A fresh clone starts from the authored asset. There is no storage or subscription. |
| `LocalFile` | Outfit | A managed field assignment writes one encrypted whole-domain snapshot. Opening the same profile restores the outfit together. |
| `DirectAccess` | Offline currency or completed levels | A managed assignment to a field writes just that key. The built-in native store flushes and conditionally replaces the record before the assignment returns. Reopening loads accepted values. |
| `DirectAccessOnline` | Account currency and signed-in progress | Assigning a field queues that key immediately. The SO publishes `DataChanged` on confirmation. Re-entering the account and slot reads the remote record. [Unity Cloud Save adapter](so-unity-cloud-save.md) |
| `RealtimeOnline` | Actors in a match | The authority publishes a changed key. Subscribed clients receive the initial state and subsequent updates. A late join reads the current match. [Unity Netcode adapter](so-unity-netcode.md) |

Split currency, equipment and tuning when their lifetime, durability or authority differs. Group values that must change atomically in one small serializable record and assign the complete record. Give independent assets of the same type different stable IDs. Back up realtime match state separately if it must survive server restart.

## Copy and call the template

- Copy [the controller](../assets/SODataController.cs), its [LocalFile partial](../assets/SODataController.LocalFile.cs), [field discovery](../assets/SODataFields.cs), [key session](../assets/SOKeySession.cs), [checkpoints](../assets/SODataCheckpoints.cs), [store contracts](../assets/SODataStore.cs), [native key store](../assets/SOFileKeyStore.cs), [file writer](../assets/SOFileStorage.cs) and [envelope](../assets/SOFileEnvelope.cs) into one runtime assembly.
- Copy the [storage mode drawer](../assets/Editor/SOStorageModeDrawer.cs) into an Editor-only assembly that references the controller.
- Copy the complete [WriteHooks](../assets/WriteHooks/SOData.Runtime.asmdef) and [CodeGen](../assets/CodeGen/Unity.SOData.CodeGen.asmdef) directories with their asmdefs intact. Add `com.unity.nuget.mono-cecil` 1.11.6 in Package Manager and reference `SOData.Runtime` from the controller's asmdef. The Editor compiler instruments source under Assets, including default Assembly-CSharp, separate data assemblies and their callers. A package-owned source asmdef can opt in by referencing `SOData.Runtime`. The compile step generates the field hooks and closed codecs used by IL2CPP. Plain gameplay source needs no storage references.
- Copy the [storage profile](../assets/SODataProfile.cs) beside the controller and the optional service adapter that the game uses. Run storage operations on Unity's main thread.

After authentication, the account owner retains the profile and awaits `profile.EnterAsync(slot)` before enabling gameplay. The profile opens the referenced data instances in place. The [Unity Cloud Save profile](../assets/Providers/UnityCloudSave/SOUnityCloudSaveProfile.cs) creates its adapter on the first online domain. Custom providers can be passed once for a profile. For match data pass the active realtime provider. Before signing out, switching slots or unloading those assets, stop gameplay and await `profile.LeaveAsync()`. The profile waits for pending writes and restores the original fields. A failed receipt reaches the lifecycle caller. A forced app termination can interrupt a pending network write.

```csharp
// PickupController.cs
public sealed class PickupController : MonoBehaviour
{
    [SerializeField] private SOPickupData _pickupData;

    public void OnPickup() => _pickupData.value = 5;
}
```

The [pickup SO](../assets/Examples/Pickups/SOPickupData.cs) fixes its mode to DirectAccessOnline. Its [complete caller](../assets/Examples/Pickups/PickupController.cs) only assigns the field. The [currency SO](../assets/SOCurrencyData.cs) exposes `wallet.CreditCoins(3)` and owns the validation and addition. Server-issued currency uses the game's validated grant operation.

For an SO deriving directly from `ScriptableObject`, attach a separate controller with `await storage.AttachAsync(target, slot)` at profile entry. At orderly exit use `await storage.CloseAsync()` before destroying the owned runtime clones. The immediate `CloseData()` remains available for forced teardown.

The remote `coins` record is scoped to its provider, account, environment, slot and storage ID. Local balances created by the 1.8 offline template stay on disk. Bringing those balances to an account requires an explicit game-owned migration that reconciles any existing server balance.

## Assignment and acknowledgement

The [compiler pass](../assets/CodeGen/SODataFieldWrites.cs) adds a call to [the write observer](../assets/WriteHooks/SODataWriteHooks.cs) after managed writes to serialized SO fields. The observer dispatches only for attached instances. The controller discovers public and private serialized fields through their inheritance chain, validates their Unity wire type, then tracks their stored revisions independently. Existing explicit key aliases and the old acknowledged extension calls remain available for integrations that use them.

- A direct field assignment or ordinary method that assigns that field is observable at the assignment, including a write in another participating source assembly. Without any active owners the observer takes a fast guard. Attaching `None` reads and writes nothing.
- An in-place change such as `items.Add(id)`, `position.x = value` or an array element edit can bypass that instruction. A shared Play checkpoint compares mutable fields every 0.25 seconds of unscaled time. Call `await storage.FlushAsync()` in the inventory or scene owner if an action needs an immediate boundary. Scalar changes through reflection, ref parameters or precompiled writers also need an explicit checkpoint for immediate durability. Pause, quit and a healthy native session close check all fields. A hard exit before a checkpoint can lose an indirect edit.
- Native file writes block the calling thread until the file transaction completes. Reserve that cadence for small important values. Each cloud key has its own ordered write queue. Conditional-write errors restore its last accepted value and reach `StorageFailed` and the next `FlushAsync`. Reopen and reconcile after a conflict or uncertain acknowledgement. Use a server-side operation ID for rewards that must be granted exactly once.
- Store stable item or equipment IDs and Unity-serializable value data. Define a conversion before changing an existing key or wire format. Unity object references serialize as session-local instance IDs. [Unity JSON serialization](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/JsonUtility.ToJson.html)

## Disk and provider contracts

- The native filesystem backend targets iOS, Android, macOS, Windows and Linux. It uses a flushed temporary file, an atomic replacement and a writer lock. For browser storage, provide an `ISOLocalDataStore` and await `FlushAsync` for its IndexedDB acknowledgement.
- The envelope checks the clear payload with MD5, encrypts it with AES-256-CBC and a fresh IV, authenticates the stored bytes with HMAC-SHA256 and encodes them in Base64. The local key lives in the storage directory's `.sodata` folder. Back up the key alongside the files. Keep economically sensitive grants under server authority.
- [SOLocalData](../assets/SOLocalData.cs) preserves existing LocalFile paths, lifecycle, explicit saves and legacy reads. Existing inherited currency keys and their JSON values retain their previous format. Validate real old records and migrate explicitly when changing a domain ID, field name, file representation or service.
- Implement `ISODataKeyStore` for another persistent cloud service. Return null for confirmed absence, conditionally create or update, return the accepted revision and surface failures. An `ISORealtimeDataStore` subscription sends its initial state and ordered changes on the Unity thread and detaches on close. Mark temporary match backends with `ISOMatchSessionStore`. The online account mode rejects both a local store and a match-only store.
