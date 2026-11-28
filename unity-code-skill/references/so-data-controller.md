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
| DirectAccess | Changes memory and queues the latest value of the changed key for an encrypted native commit. `FlushAsync` confirms durability. | [Offline keys](../assets/script/SODataController/Modes/SODirectAccessMode.cs) |
| DirectAccessOnline | Changes the local view and queues a conditional write for this field on the signed-in account. | [Cloud mode](../assets/script/SODataController/Modes/SODirectAccessOnlineMode.cs) |
| RealtimeOnline | Coalesces pending state, publishes an authorized field change and applies ordered remote updates to the local backing field. | [Live mode](../assets/script/SODataController/Modes/SORealtimeOnlineMode.cs) |
| LocalFileOnline | Uses the shared snapshot checkpoint, then uploads the acknowledged local JSON file through the selected file provider. | [File backup mode](../assets/script/SODataController/Modes/SOLocalFileOnlineMode.cs) |

## Copy and call the template

After [choosing the data owner](#choose-the-owner-for-server-data), copy the installed Skill's `assets/script` into Unity's `Assets` for a new SO domain. The base, six `Modes` and optional `Proviers/` channels live together under `script/SODataController/`. Add `com.unity.nuget.newtonsoft-json` (tested with 3.2.2). Enable the SDK define for each channel the receiver uses. [Copy instructions and breakpoints](../assets/script/SODataController/Usage.md) · [Controller](../assets/script/SODataController/SODataController.cs)

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

Ordinary gameplay uses `pickupData.value = 5;` and `int current = pickupData.value;`. The default offline slot begins loading in the background on first access. Getters continue reading the current backing fields during initialization, and an absolute assignment made during a read is preserved. Online values can be staged before their channel is bound. Bind the signed-in slot or spawned match once in the lifecycle owner, then share the same SO with gameplay. `Initialization` and `IsReady` expose readiness. Await the first `OpenAsync` in the bootstrap before commands that compute a delta from a persisted balance; its `await` leaves the Unity thread available. Existing wallet transaction and server-grant logic remains with its current owner.

File reads, encryption, snapshot reconstruction and native commits run on workers. SDK calls use asynchronous requests. The mode retains the latest unsent state per field, allowing one in-flight write and one newer value. A late acknowledgement cannot overwrite that newer assignment. Initial scalar and string decoding also runs on workers before the backing field is applied on the Unity thread. Authored-default capture and mutable collection snapshot capture still consume CPU on the Unity thread. Keep large mutable domains divided by their actual update boundaries.

Await `data.FlushAsync()` for confirmed persistence and `data.CloseAsync()` before switching account, slot or match. A failed close leaves the current slot available for recovery. `CloseData()` schedules retirement and returns immediately; `Retirement` exposes that work. A reopening of the same storage identity waits asynchronously for retirement. `CloseData(discardLocalChanges: true)` deliberately abandons unsent changes. A terminated process can lose work whose disk receipt has not completed.

When two assets of the same type hold independent data in one slot, give each a distinct Inspector Storage Id. An empty ID uses the type name. Two writers for the same stored record can conflict at commit. A legacy default-slot snapshot uses its original file name as its identity.

## Skip identical values

Equal property assignments return before storage work or change notifications. The online mode also suppresses notifications for an identical incoming payload while accepting its new revision, so the next changed value uses the current write lock. A pending write still receives its original confirmation. Count that acknowledgement separately from extra events caused by repeated assignments. The foundation cases check equal integer assignments in all six modes, a held cloud acknowledgement and a same-payload update over real Netcode peers.

## Batch whole-file saves

LocalFile and LocalFileOnline share the five-minute snapshot window. The first dirty SO starts one shared timer. Assignments replace pending field values in memory. Reverting all edits to the last committed values cancels pending work. At the checkpoint a worker serializes, encrypts and atomically replaces each dirty domain once. Edits arriving during a write stay pending. Idle domains perform no file reads, writes or uploads.

Use `await data.FlushAsync()` for an earlier gameplay checkpoint. A Unity focus-loss or quit callback schedules pending local work without waiting on the main thread. An explicit close can await its receipt. A process killed before a checkpoint can lose the latest in-memory edits. Keep individually durable offline keys on DirectAccess, account fields on DirectAccessOnline and live state on RealtimeOnline.

Use LocalFileOnline for a new whole-file backup domain. Existing file adapters can continue consuming `SnapshotCommitted` once per local commit. That event receives the already serialized JSON and avoids another file read. `DataChanged` notifies the accepted visible value. [File backup contract](local-first-cloud-saves.md#commit-locally-then-synchronize)

On macOS Unity 6000.6.7f1, the same 64 KiB JSON fixture and 128 successive coin assignments produced 128 file replacements (11,205,828 bytes) with the previous writer and one (87,561 bytes) with batching. Another 10,000 identical assignments and reads produced zero extra writes. Reopening returned 128 and preserved the other JSON data. [Reproduce the snapshot and timer tests](https://github.com/qinbatista/qin-unity-skills/blob/master/.agents/skills/testing-skill/SKILL.md#run-the-foundation-fixture)

## Audit storage cost by mode

Trace a gameplay assignment through the actual writer. Count cold opens, changed values, repeated equal values and acknowledgements separately. Check instance identity at the writer and reader. Sharing one open SO reuses its mode and subscriptions. Each deliberately cloned and opened SO has its own state and repeats its startup work.

Run the matched storage cases in macOS Unity 6000.6.7f1 and replay on the target device. The fixture counts foreground setter and focus-callback time separately from background completion. It verifies the reopened values and equal-value suppression. [Inputs and runner](https://github.com/qinbatista/qin-unity-skills/blob/master/.agents/skills/testing-skill/SKILL.md#run-the-foundation-fixture)

| Mode | Runtime work | Cost boundary to verify |
| --- | --- | --- |
| None | Uses one shared memory instance and skips equal assignments. | Capture authored defaults once per open and avoid unnecessary clones. |
| LocalFile | Batches changed fields and rebuilds their JSON snapshot on a worker. | Whole-file byte count and worker memory still scale with snapshot size. Focus loss schedules dirty domains concurrently. |
| DirectAccess | Enqueues the latest key state and commits its encrypted record on a worker. | Distinguish immediate memory updates from disk acknowledgements. Separate keys are independent transactions. |
| DirectAccessOnline | Loads up to four fields concurrently and coalesces unsent states per field. | Bound in-flight SDK mutations, preserve write locks and retain domain-owned transaction receipts. |
| RealtimeOnline | Coalesces replaceable field states before sending and applies live server updates. | Verify authority, one subscription per field, main-thread transport calls and unsubscribe. |
| LocalFileOnline | Uploads the exact logical JSON from a successful local checkpoint. | Keep one upload active per domain, retain its latest pending file, query revisions before downloading, and retry failed backups from the retained local file. |

The same foreground workloads were replayed before and after the asynchronous change on this Mac. Four dirty 2 MiB snapshots took 1,113.294 ms inside the earlier focus callback. The final asynchronous replay took 0.783 ms, while background completion restored all four values to 5. A burst of 128 offline key assignments took 1,278.222 ms previously and 0.365 ms with the queued writer; reopening returned 129. These numbers measure time spent in the caller, with the disk acknowledgement awaited separately. The final native matrix passed 56 cases; the separately configured hosted case passed against Unity Cloud Save Files. The real five-minute checkpoint also passed its timer case.

In the SDK fixture, 128 different assignments in one frame became one pending write containing 128. Repeating 128 another 10,000 times added no save. Two wrapper instances sharing the same SDK client admitted four mutations from eight waiting operations. The native Netcode fixture coalesced 32 positions to one accepted final state, and the remote SO reached x=32. Repeating it 100 times added no notification. The actual shared timer committed two dirty domains after 300.28 seconds and created no file for a reverted domain.

The hosted Unity Files case uploaded a 2 MiB snapshot, read 5 through the independent Files SDK, restored 5 after removing local storage, then changed the remote file to 6. Ten thousand equal assignments preserved the head revision, one current blob remained after replacement, and the test removed its files and anonymous player.

Persistent modes serialize a reassigned list or array to compare its contents. Reassigning the same 1,024-item inventory 128 times took 32 ms total and produced zero writes. The corresponding equal integer assignments took 0.003 ms. Assign a large collection at its mutation boundary. [Assignment implementation](../assets/script/SODataController/SODataController.cs)

Native commits serialize per storage directory. Writers in the same directory share the device key, staging file and native process lock. A DirectAccess key in the normal `keys/` directory can commit while LocalFile replaces a snapshot under `profiles/`. The short registry lock covers lookup and removal only. Its entries expire when the last holder or waiter leaves. [File writer](../assets/script/SODataController/Modes/SOLocalFileMode.cs)

The same 2 MiB snapshot and foreground integer setter ran in three fresh macOS Unity 6000.6.7f1 processes per version. All six runs started the key write before snapshot replacement, then reopened key `3`, snapshot value `5` and all 2 MiB of padding. The table measures the foreground setter in milliseconds. Disk bandwidth and flush latency still depend on the device. [Replay the overlap and same-directory tests](https://github.com/qinbatista/qin-unity-skills/blob/master/.agents/skills/testing-skill/SKILL.md#run-the-foundation-fixture)

| Writer | Key alone, median | Key during checkpoint, three runs | Median during checkpoint |
| --- | ---: | --- | ---: |
| 2.2.1, process-wide lock | 12.05 | 104.14, 103.88, 115.17 | 104.14 |
| 2.2.2, directory lock | 11.84 | 14.35, 17.07, 15.39 | 15.39 |

A separate hosted Unity Cloud Save test wrote `5` on a dedicated player. An independent provider read returned `5`, 10,000 equal assignments preserved the service write lock, and a second Unity process reopened the same player and read `5`. Both test keys and the disposable account were removed. Use the controlled SDK workload above for queue counts and this signed-in replay for service persistence.

## LocalFileOnline

`SOFileData` fixes this mode in its class. Gameplay assigns `data.value = 5` through the same property API. After authentication, the lifecycle owner calls `await data.OpenAsync(playerSlot)` for the default Unity file provider. `await data.OpenAsync(playerSlot, fileProvider: provider)` selects another `ISODataFileStore`. Use a slot containing the current environment and account identity; each SO also has its own storage ID. Place each provider implementation in its own `Proviers` file. [SO example](../assets/script/SODataController/Examples/SOFileData.cs) · [Unity provider](../assets/script/SODataController/Proviers/UnityCloudSaveFileStore.cs)

```text
SOFileData.value = 5 -> memory and dirty snapshot
                     -> checkpoint or FlushAsync
                     -> encrypted atomic local file
                     -> asynchronous file upload
                     -> conditional remote head
                     -> encrypted local acknowledgement
```

The remote payload is the portable UTF-8 JSON from the local commit. The local envelope keeps its device-bound encryption key. A new device downloads the logical file and creates its own encrypted local copy. The provider also supports binary files directly through its byte-array interface.

The Unity wrapper stores immutable blobs using Cloud Save Files and publishes a head using Cloud Save Player Data with a conditional revision. It verifies the downloaded hash, reconciles an upload whose head acknowledgement was lost, and removes obsolete blobs after publishing the new head. This also protects two first-time writers. A checksum or provider failure remains an error. A missing record is the only normal absent result. [Unity Files SDK](https://docs.unity.com/en-us/cloud-save/tutorials/unity-sdk)

The encrypted local acknowledgement records the accepted revision and content hash. When an upload fails, the changed local file remains available after restart. Transient failures retry with backoff while the mode is active. Returning to the foreground requests a revision refresh. An unchanged revision skips the file download, and an unchanged file skips upload and acknowledgement rewrites. New local edits survive a delayed remote read or receipt. Conflicting independently edited snapshots retain the local file and report `SODataConflictException` for the domain owner to resolve. `await data.ResolveFileConflictAsync(SOFileConflictResolution.KeepLocal)` republishes the retained local file against the fresh remote revision. `UseRemote` restores the remote file while preserving edits made after that resolution began. Existing server transaction semantics stay with the receiving game's data owner.

A local or key-mode assignment starts background work and returns. A filesystem acknowledgement proves durability. Awaiting a checkpoint allows UI and gameplay frames to continue. Application termination can interrupt unfinished work, including a quit callback's final job. Use the explicit asynchronous checkpoint at the game's critical lifecycle boundary.

## Run the pickup walkthrough

In the Unity Editor select **Tools → SODataController → Run Pickup Walkthrough**. The seven Console entries show the initial 37, the gameplay call, the outgoing payload, the pending reply, the duplicate assignment, the acknowledgement and a new SO reading 5. The demo uses the actual pickup component and a private simulated channel. Set breakpoints in the property, base, mode and channel using the [included steps and recorded output](../assets/script/SODataController/Usage.md). The [Editor command](../assets/script/SODataController/Examples/Pickups/Editor/SOPickupWalkthrough.cs) creates and removes its own temporary objects.

## Persistence and channels

LocalFile and DirectAccess retain the authenticated `SODATA/1` envelope, atomic replacement, device key and published paths. The envelope uses AES encryption, HMAC authentication, an MD5 payload checksum and Base64 encoding. Back up the device key with its files. A legacy LocalFile domain can override `LegacyFileName` for its original default-slot path. A valid older file keeps its bytes until the next dirty checkpoint. Keep the same slot, storage ID and field keys when adopting an existing game. Keyed payloads retain their existing `{"value":5}` representation. The built-in file writer targets native mobile and desktop filesystems.

For a file provider implement `ISODataFileStore` with revision lookup, byte download and conditional upload. For another persistent key channel implement `ISODataKeyStore` in one file. For a subscription channel implement `ISORealtimeDataStore` with initial state, ordered updates and unsubscribe. Return revisions, reject stale writes and surface errors. The SO exposes `DataChanged` after acceptance, `StorageFailed` on a failed online write, and `FlushAsync()` to await pending receipts. A lost reply requires a fresh read before retry. [Unity Cloud Save](so-unity-cloud-save.md) · [Unity Netcode](so-unity-netcode.md)

## Verify the template

Run the [published foundation suite](https://github.com/qinbatista/qin-unity-skills/blob/master/.agents/skills/testing-skill/SKILL.md#run-the-foundation-fixture) with the optional SDKs disabled, then with `--cloud --netcode`. It exercises the six modes, old saves, failed writes, simulated Cloud Save receipts and two real Netcode peers on loopback. For hosted account storage, write `5` on a dedicated test player, await `FlushAsync()`, and compare an independent provider read with a fresh session on the same slot. Remove the test data after readback.
