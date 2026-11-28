# ScriptableObject property storage

## Choose the owner for server data

When connecting a server or handling data it sends, start at the gameplay code that reads or changes the value. Trace its current writer, the save key or network message, and the session that carries it. Make the decision for the requested data domain.

| What the game already has | Where to make the change |
| --- | --- |
| A connection and only its URL, environment or provider is changing | Update the existing configuration or adapter. Keep the same session lifecycle, data owner and gameplay calls. |
| A wallet, inventory, replicated component or other owner that covers this value | Extend that owner's existing command or receive handler, even when it uses plain C# or a different networking stack. Add a field there when it belongs to the same domain. Preserve callers, save keys, wire format and write authority unless the request changes that contract. |
| No owner for this kind of gameplay value | Create a small `SODataController` for the new domain. Use `DirectAccessOnline` for persistent account values or `RealtimeOnline` for live match state. Reuse the game's authentication and transport. Bind the appropriate channel once after sign-in or spawn. [Mode selection](#choose-one-mode-per-domain) · [Provider contracts](#persistence-and-channels) |

For example, a new server balance message belongs in the existing wallet's receive path. Keep grants under the current server command and notify gameplay once when the accepted balance arrives. Replacing a working owner is a separate migration when the requested behavior requires it.

For a new match score with no data owner, the server writes the score SO and a client reads its subscribed copy. Verify a persistent update by reopening the same account and slot. Verify a pushed update at a remote consumer and check that receiving it produces no second gameplay write.

## Choose one mode per domain

For a new domain using the template, inherit `SODataController` and choose one mode for the whole SO in the Inspector or its `RequiredMode` override. Keep wallet, outfit and match state in separate assets. A regular ScriptableObject can still be used independently.

| Mode | What a property assignment does | Source |
| --- | --- | --- |
| None (default) | Changes the shared runtime instance in memory. | [Memory](../assets/script/SODataController/Modes/SONoneMode.cs) |
| LocalFile | Changes memory immediately. The shared five-minute checkpoint writes one encrypted snapshot per dirty domain. | [Snapshot](../assets/script/SODataController/Modes/SOLocalFileMode.cs) |
| DirectAccess | Commits only the changed key to its own encrypted native record before the setter returns. | [Offline keys](../assets/script/SODataController/Modes/SODirectAccessMode.cs) |
| DirectAccessOnline | Changes the local view and queues a conditional write for this field on the signed-in account. | [Cloud mode](../assets/script/SODataController/Modes/SODirectAccessOnlineMode.cs) |
| RealtimeOnline | Publishes an authorized field change and applies ordered remote updates to the local backing field. | [Live mode](../assets/script/SODataController/Modes/SORealtimeOnlineMode.cs) |

## Copy and call the template

After [choosing the data owner](#choose-the-owner-for-server-data), copy the installed Skill's `assets/script` into Unity's `Assets` for a new SO domain. The base, five `Modes` and optional `Proviers/` channels live together under `script/SODataController/`. Add `com.unity.nuget.newtonsoft-json` (tested with 3.2.2). Enable the SDK define for each channel the receiver uses. [Copy instructions and breakpoints](../assets/script/SODataController/Usage.md) · [Controller](../assets/script/SODataController/SODataController.cs)

Give each stored member a private serialized backing field and a C# property. The normal assignment calls the setter, which calls the selected mode. The base matches the property name to the backing field's key, trimming leading underscores and accepting the PascalCase property equivalent. For different names pass the explicit key to `Write`. Use `[SODataKey("old-key")]` to preserve a published key. Keep a renamed Unity backing field compatible with old authored assets using `FormerlySerializedAs`. Unity serializes the backing fields. Make the changes through their properties. To save a collection or nested record, assign the updated value back through the property.

```csharp
// SOPickupData.cs
public sealed class SOPickupData : SODataController
{
    [SerializeField, FormerlySerializedAs("value")] private int _value;
    public override SODataMode? RequiredMode => SODataMode.DirectAccessOnline;
    public int value { get => Read(ref _value); set => Write(ref _value, value); }
}
```

Ordinary gameplay uses `pickupData.value = 5;` and `int current = pickupData.value;`. The default offline slot opens on first access. For a named slot, call `await data.OpenAsync(slot)` before gameplay. After authentication or network spawn open an online SO once with `await data.OpenAsync(slot, channel)`. Share that instance with its writers and readers. Each open SO owns one mode. Getters read the backing field in memory. Keep separate instances for independent accounts, slots or peers.

Await `data.CloseAsync()` before switching account, slot or match. It flushes changes and restores authored defaults. A failed LocalFile close leaves the slot open for retry. To abandon conflicting local edits deliberately, call `data.CloseData(discardLocalChanges: true)`, then reopen the slot. Synchronous `CloseData()` commits pending LocalFile changes and detaches subscriptions.

When two assets of the same type hold independent data in one slot, give each a distinct Inspector Storage Id. An empty ID uses the type name. Two writers for the same stored record can conflict at commit. A legacy default-slot snapshot uses its original file name as its identity.

## Batch whole-file saves

Only LocalFile uses the five-minute window. The first dirty SO starts one shared timer. Assignments replace pending field values in memory. Reverting all edits to the last committed values cancels pending work. At the checkpoint a worker serializes, encrypts and atomically replaces each dirty domain once. Edits arriving during a write stay pending. Idle domains perform no file reads, writes or uploads.

Use `await data.FlushAsync()` for an earlier gameplay checkpoint. A Unity focus-loss or quit callback and a normal SO close also flush pending local data. A process killed before a checkpoint can lose the latest in-memory edits. Keep individually durable offline keys on DirectAccess, account fields on DirectAccessOnline and live state on RealtimeOnline.

An optional whole-file cloud backup subscribes to `SnapshotCommitted` once per domain and passes the JSON string supplied by that event directly to its existing adapter. This reuses the exact serialized content and avoids another file read. The event fires once after the atomic write, even if several fields changed. `DataChanged` still confirms individual fields. Track the backup receipt per account and slot. [File backup contract](local-first-cloud-saves.md#commit-locally-then-synchronize)

On macOS Unity 6000.6.7f1, the same 64 KiB JSON fixture and 128 successive coin assignments produced 128 file replacements (11,205,828 bytes) with the previous writer and one (87,561 bytes) with batching. Another 10,000 identical assignments and reads produced zero extra writes. Reopening returned 128 and preserved the other JSON data. [Reproduce the snapshot and timer tests](https://github.com/qinbatista/qin-unity-skills/blob/master/.agents/skills/testing-skill/SKILL.md#run-the-foundation-fixture)

## Audit storage cost by mode

Trace a gameplay assignment through the actual writer. Count cold opens, changed values, repeated equal values and acknowledgements separately. Check instance identity at the writer and reader. Sharing one open SO reuses its mode and subscriptions. Each deliberately cloned and opened SO has its own state and repeats its startup work.

The following counts come from the 2.2.0 template in macOS Unity 6000.6.7f1. Timings are single desktop observations. Replay on the target device and storage. [Inputs and test runner](https://github.com/qinbatista/qin-unity-skills/blob/master/.agents/skills/testing-skill/SKILL.md#run-the-foundation-fixture)

| Mode | Measured workload | What can become expensive |
| --- | --- | --- |
| None | One shared 64 KiB SO, 128 scalar changes, then 10,000 equal assignments. Opening serialized the SO once. The loop created zero SOs, serialized the SO zero times and wrote zero files. | The first access captures all authored defaults for reset. Repeatedly opening large instances repeats that work. |
| LocalFile | 128 changes to a 64 KiB snapshot produced one 87,561-byte replacement. 10,000 equal assignments produced zero more. The real timer committed two dirty domains after 300.17 s and produced no file for a reverted domain. | A dirty save rebuilds the entire JSON on a worker. Opening reads it synchronously. Focus loss or synchronous close can wait for pending writes. Split independently changing data. |
| DirectAccess | 128 changes produced 128 durable receipts before the setter returned, updating one record in 1.25 s total. 10,000 equal assignments added zero writes. Cold reopen returned 128. | Each change checks the old record and commits the key on the calling thread. Use a setter per critical offline transaction and measure calls per frame. |
| DirectAccessOnline | Opening two fields made two sequential reads. With the first receipt held, 128 distinct assignments to an existing key submitted one save immediately and 127 after release. 10,000 equal assignments added zero saves. These are Unity Cloud Save SDK calls against a controlled service. | A slow provider builds a per-key queue while gameplay sees the latest value. Preserve currency transactions. For replaceable state, define which intermediate values the domain can discard. |
| RealtimeOnline | A real Netcode 2.14.0 server assigned 32 positions in one frame. The remote SO received 32 updates over UDP loopback and finished at x=32. Repeating that position 100 times produced zero new confirmations. | Each open field adds a subscription. Multiple field updates can share a packet. Sample continuous movement at the match tick where gameplay needs only the latest state. Measure bytes on the target transport. |

Persistent modes serialize a reassigned list or array to compare its contents. Reassigning the same 1,024-item inventory 128 times took 32 ms total and produced zero writes. The corresponding equal integer assignments took 0.003 ms. Assign a large collection at its mutation boundary. [Assignment implementation](../assets/script/SODataController/SODataController.cs)

Native commits serialize per storage directory. Writers in the same directory share the device key, staging file and native process lock. A DirectAccess key in the normal `keys/` directory can commit while LocalFile replaces a snapshot under `profiles/`. The short registry lock covers lookup and removal only. Its entries expire when the last holder or waiter leaves. [File writer](../assets/script/SODataController/Modes/SOLocalFileMode.cs)

The same 2 MiB snapshot and foreground integer setter ran in three fresh macOS Unity 6000.6.7f1 processes per version. All six runs started the key write before snapshot replacement, then reopened key `3`, snapshot value `5` and all 2 MiB of padding. The table measures the foreground setter in milliseconds. Disk bandwidth and flush latency still depend on the device. [Replay the overlap and same-directory tests](https://github.com/qinbatista/qin-unity-skills/blob/master/.agents/skills/testing-skill/SKILL.md#run-the-foundation-fixture)

| Writer | Key alone, median | Key during checkpoint, three runs | Median during checkpoint |
| --- | ---: | --- | ---: |
| 2.2.1, process-wide lock | 12.05 | 104.14, 103.88, 115.17 | 104.14 |
| 2.2.2, directory lock | 11.84 | 14.35, 17.07, 15.39 | 15.39 |

A separate hosted Unity Cloud Save test wrote `5` on a dedicated player. An independent provider read returned `5`, 10,000 equal assignments preserved the service write lock, and a second Unity process reopened the same player and read `5`. Both test keys and the disposable account were removed. Use the controlled SDK workload above for queue counts and this signed-in replay for service persistence.

## Run the pickup walkthrough

In the Unity Editor select **Tools → SODataController → Run Pickup Walkthrough**. The seven Console entries show the initial 37, the gameplay call, the outgoing payload, the pending reply, the duplicate assignment, the acknowledgement and a new SO reading 5. The demo uses the actual pickup component and a private simulated channel. Set breakpoints in the property, base, mode and channel using the [included steps and recorded output](../assets/script/SODataController/Usage.md). The [Editor command](../assets/script/SODataController/Examples/Pickups/Editor/SOPickupWalkthrough.cs) creates and removes its own temporary objects.

## Persistence and channels

LocalFile and DirectAccess retain the authenticated `SODATA/1` envelope, atomic replacement, device key and published paths. The envelope uses AES encryption, HMAC authentication, an MD5 payload checksum and Base64 encoding. Back up the device key with its files. A legacy LocalFile domain can override `LegacyFileName` for its original default-slot path. A valid older file keeps its bytes until the next dirty checkpoint. Keep the same slot, storage ID and field keys when adopting an existing game. Keyed payloads retain their existing `{"value":5}` representation. The built-in file writer targets native mobile and desktop filesystems.

For another persistent channel implement `ISODataKeyStore` in one file. For a subscription channel implement `ISORealtimeDataStore` with initial state, ordered updates and unsubscribe. Return revisions, reject stale writes and surface errors. The SO exposes `DataChanged` after acceptance, `StorageFailed` on a failed online write, and `FlushAsync()` to await pending receipts. A lost reply requires a fresh read before retry. [Unity Cloud Save](so-unity-cloud-save.md) · [Unity Netcode](so-unity-netcode.md)

## Verify the template

Run the [published foundation suite](https://github.com/qinbatista/qin-unity-skills/blob/master/.agents/skills/testing-skill/SKILL.md#run-the-foundation-fixture) with the optional SDKs disabled, then with `--cloud --netcode`. It exercises the five modes, old saves, failed writes, simulated Cloud Save receipts and two real Netcode peers on loopback. For hosted account storage, write `5` on a dedicated test player, await `FlushAsync()`, and compare an independent provider read with a fresh session on the same slot. Remove the test data after readback.
