# ScriptableObject storage modes

## Choose one mode per domain

Keep the game's data class, its fields, base class, methods and callers. Create **Game Data → Storage Controller** in the Inspector and give the controller one mode and a stable storage ID. The game's profile bootstrap attaches it to the runtime SO that gameplay already uses. Existing subclasses of [SODataController](../assets/SODataController.cs) can keep their own Inspector settings and call `OpenAsync` on their runtime clone.

A domain with a required storage policy overrides `RequiredMode`. Its Inspector displays the effective mode read-only. A separate controller must match that mode to attach. Ordinary domains inherit the editable `None` default.

| Mode | Example | When a value is saved and restored |
| --- | --- | --- |
| `None` (default) | Speed or attack tuning | Consumers share one runtime instance. A fresh clone starts from the authored asset. There is no storage or subscription. |
| `LocalFile` | Outfit | A managed field assignment writes one encrypted whole-domain snapshot. Opening the same profile restores the outfit together. |
| `DirectAccess` | Offline currency or completed levels | A managed assignment to a field writes just that key. The built-in native store flushes and conditionally replaces the record before the assignment returns. Reopening loads accepted values. |
| `DirectAccessOnline` | Signed-in progress | Assigning a field queues that key immediately. Gameplay sees the new local value while the request is pending. Await `FlushAsync` on the controller before confirming success. A fresh open reads the cloud. [Unity Cloud Save adapter](so-unity-cloud-save.md) |
| `RealtimeOnline` | Actors in a match | The authority publishes a changed key. Subscribed clients receive the initial state and subsequent updates. A late join reads the current match. [Unity Netcode adapter](so-unity-netcode.md) |

Split currency, equipment and tuning when their lifetime, durability or authority differs. Group values that must change atomically in one small serializable record and assign the complete record. Give independent assets of the same type different stable IDs. Back up realtime match state separately if it must survive server restart.

## Copy and call the template

- Copy [the controller](../assets/SODataController.cs), its [LocalFile partial](../assets/SODataController.LocalFile.cs), [field discovery](../assets/SODataFields.cs), [key session](../assets/SOKeySession.cs), [checkpoints](../assets/SODataCheckpoints.cs), [store contracts](../assets/SODataStore.cs), [native key store](../assets/SOFileKeyStore.cs), [file writer](../assets/SOFileStorage.cs) and [envelope](../assets/SOFileEnvelope.cs) into one runtime assembly.
- Copy the [storage mode drawer](../assets/Editor/SOStorageModeDrawer.cs) into an Editor-only assembly that references the controller.
- Copy the complete [WriteHooks](../assets/WriteHooks/SOData.Runtime.asmdef) and [CodeGen](../assets/CodeGen/Unity.SOData.CodeGen.asmdef) directories with their asmdefs intact. Add `com.unity.nuget.mono-cecil` 1.11.6 in Package Manager and reference `SOData.Runtime` from the controller's asmdef. The Editor compiler instruments source under Assets, including default Assembly-CSharp, separate data assemblies and their callers. A package-owned source asmdef can opt in by referencing `SOData.Runtime`. The compile step generates the field hooks and closed codecs used by IL2CPP. Plain gameplay source needs no storage references.
- Copy one optional service adapter if the game uses it. The local modes require no cloud account or SDK. Bind both authored assets in the bootstrap and make one runtime instance of each for the active profile. Run their storage operations on Unity's main thread.

The packaged [SOCurrencyData](../assets/SOCurrencyData.cs) fixes `RequiredMode` to `DirectAccess` for offline currency. Give the asset a stable storage ID. The bootstrap opens its runtime instance and shares it with the pickup and UI. `CreditCoins` requires an open profile, validates the amount and updates the private setter. The field keeps its original asset data and the disk key `coins`. For a game whose existing wallet uses a different backend, retain that policy in the game's own class or migrate the existing balances before adopting the offline template.

```csharp
SOCurrencyData wallet = UnityEngine.Object.Instantiate(authoredWallet);
await wallet.OpenAsync("slot-a");  // Once at profile entry.
wallet.CreditCoins(3);            // At pickup. UI reads wallet.Coins.
```

For an existing SO that derives directly from `ScriptableObject`, instantiate a separate controller policy and call `await storage.AttachAsync(wallet, "slot-a")` at profile entry. Keep that SO's own methods, setters or public fields. [SOOutfitData](../assets/SOOutfitData.cs) likewise keeps ordinary `WearHat` and `WearBoots` methods. When leaving a profile, close the controller to detach and restore the clone's pre-open defaults, then destroy the owned runtime clones. For an online controller first await `FlushAsync` if pending changes must be acknowledged.

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
- Implement `ISODataKeyStore` for another cloud service. Return null for confirmed absence, conditionally create or update, return the accepted revision and surface failures. An `ISORealtimeDataStore` subscription sends its initial state and ordered changes on the Unity thread and detaches on close. Scope each adapter to one account, environment or match.
