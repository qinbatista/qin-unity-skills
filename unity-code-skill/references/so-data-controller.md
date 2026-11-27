# ScriptableObject storage modes

## Choose one mode per domain

An ordinary ScriptableObject can keep its current base. Inherit [SODataController](../assets/SODataController.cs) to add a storage policy. Its Inspector default is `None`. Choose the mode and a stable storage ID on each authored asset. Make one runtime clone per active profile and share that clone with the domain's gameplay and UI. A profile and storage ID together identify one domain. Give two assets of the same type different IDs when their data is independent.

| Mode | Example SO | Write and restore boundary |
| --- | --- | --- |
| `None` | Character speed and attack tuning for the current run | Consumers share the runtime object. A fresh clone starts from the authored asset. Opening this mode performs zero storage operations. |
| `LocalFile` | Equipped hat and boots | Open reads one whole-domain snapshot. A successful `SaveDataAtLocal()` or `CommitChangeAsync` atomically replaces it. A cold open restores every serialized gameplay value in the snapshot. |
| `DirectAccess` | Offline currency, cleared levels or inventory | Each successful commit durably writes just the named key, checks its previous revision and then publishes the accepted value. Each domain has its own SO. |
| `DirectAccessOnline` | The signed-in player's equipment or progress | Await the provider's conditional write for the changed key. On a fresh open, read the provider's accepted values. See the [Unity Cloud Save adapter](so-unity-cloud-save.md). |
| `RealtimeOnline` | An actor or match observed by several players | Subscribe to a spawned authority. Apply the initial state and ordered server updates, then detach on close. The [Unity Netcode adapter](so-unity-netcode.md) shows how to bind the server and clients. Server restart persistence is a separate gameplay checkpoint. |

Give domains with different lifetimes, authority, access or save cadence their own SO. Keep values that must change atomically in one small serializable record and bind that record as one key. Use stable item and equipment IDs. Put scene objects, runtime references, transient indexes and provider configuration outside the durable data. Unity serializes object references in JSON as session-local instance IDs. [Unity JSON serialization](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/JsonUtility.ToJson.html)

## Copy and call the template

Copy the controller, its [LocalFile partial](../assets/SODataController.LocalFile.cs), [field bindings](../assets/SODataFields.cs), [session](../assets/SOKeySession.cs), [store interfaces](../assets/SODataStore.cs), [local store](../assets/SOFileKeyStore.cs), [file writer](../assets/SOFileStorage.cs) and [envelope](../assets/SOFileEnvelope.cs) into the same runtime assembly. Copy only the provider folder selected by the receiving game. The base and local modes have no service SDK dependency.

Create **Game Data → Currency** from [SOCurrencyData](../assets/SOCurrencyData.cs), select `DirectAccess` and set the storage ID to `currency`. Bind that asset to the game's bootstrap. Run the following operations on the Unity thread.

```csharp
var wallet = UnityEngine.Object.Instantiate(authoredWallet);
await wallet.OpenAsync("slot-a");
await wallet.CreditCoinsAsync(3);
coinsLabel.text = wallet.Coins.ToString();
```

The existing `CreditCoins(int)` still validates and changes its original field. The persisted entry point wraps that method in `CommitChangeAsync(nameof(coins), () => CreditCoins(amount))`. An ordinary C# field assignment has no write notification. Route every durable mutation through that entry point. Keep the callback synchronous, scoped to the named key and free of externally visible effects. Award items or update UI after success. The session serializes overlapping writes to one key. A failed or stale write leaves the last accepted projection visible. On conflict or uncertain acknowledgement, close, reopen and reconcile the action with the stored value before retrying.

`DefineDataKeys` binds each serialized domain field once for the three keyed modes. Pass a stable fourth argument to `Bind` when the field's C# name differs from its storage key. A missing binding fails at open. The default writer also rejects layouts Unity could silently drop, including dictionaries and durable Unity object references. Use a closed, Unity-serializable value type for each binding. Use stable DTOs for lists or composite records; changing a member of a bound record writes that record only. A new wire format needs an explicit migration. A plain SO, `None` and whole-object `LocalFile` need no binding map.

Use [SOOutfitData](../assets/SOOutfitData.cs) for a whole-domain example. Set its asset to `LocalFile`, call `await outfit.OpenAsync("slot-a")`, then `await outfit.WearHatAsync("hat-red")`. The snapshot also retains the boots. Call `CloseData()` when changing profile or ending a session, then destroy the runtime clone you own. The SO's disable hook detaches owned subscriptions. Service providers, login, transport and server instances retain their own lifetimes.

## Disk and provider contracts

- The native filesystem backend targets iOS, Android, macOS, Windows and Linux. It uses a flushed temporary file, atomic replacement and a writer lock. `DirectAccess` stores a separate record per key and runs file I/O off the Unity thread. Whole-domain `LocalFile` writes synchronously at the selected checkpoint. For browser storage, supply an `ISOLocalDataStore` that acknowledges the IndexedDB transaction's completion. The native backend rejects unsupported file targets.
- The envelope contains an MD5 of the clear payload. AES-256-CBC encrypts it with a fresh IV, HMAC-SHA256 authenticates the stored bytes and Base64 encodes them as text. The locally generated key lives inside the storage directory's `.sodata` folder. Back up that directory with the data. This protects against casual edits. A player controlling the device can restore an older valid save or access its local key. Keep paid balances and grants under the game's trusted server transaction policy.
- [SOLocalData](../assets/SOLocalData.cs) is the compatibility base for already shipped subclasses. It selects `LocalFile` and retains their previous path, hooks, Play startup and explicit save calls. It reads supported checked JSON and legacy single-string Base64 saves. A successful later save writes the new envelope. Keep the old file name and validate real old saves before changing the contract. Switching an existing game to new domain IDs, profiles, keyed storage or another service requires an explicit one-time migration.
- Implement `ISODataKeyStore` for Firebase or another cloud service. Return null only for a confirmed missing key, use conditional create/update, return a new revision on acceptance and surface conflicts, authentication and network failures. Use the provider's transaction when the client does not support conditional creation. Implement `ISORealtimeDataStore` for push. Its subscription supplies the initial value, then ordered changes on the Unity thread, with disposal on disconnect. Bind an adapter to one account, environment or match.
