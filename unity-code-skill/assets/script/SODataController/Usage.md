# Use SODataController in a Unity project

Use this template for gameplay data that has no existing owner. When an existing wallet, inventory or replicated component already owns the value, apply server updates through its current API. An endpoint change belongs in the existing connection settings or adapter.

Copy the Skill's `assets/script` folder into the receiving project's `Assets`. Its runtime entry is `Assets/script/SODataController/SODataController.cs`. Install `com.unity.nuget.newtonsoft-json` (tested with 3.2.2). If the project uses assembly definitions, the runtime assembly needs a reference to `Unity.Newtonsoft.Json`. When moving an existing game script, move its Unity `.meta` with it so existing asset references retain their GUID.

```text
Assets/script/SODataController/
  SODataController.cs
  Modes/                    one implementation for each of the six modes
  Proviers/UnityCloudSaveKeyStore.cs   persistent account data
  Proviers/UnityCloudSaveFileStore.cs  remote file backup
  Proviers/SOUnityNetcodeStore.cs      live match state
  Examples/Pickups/         pickup SO, component and optional Editor walkthrough
```

## Run the pickup walkthrough

In Unity select **Tools → SODataController → Run Pickup Walkthrough**. Inspect the seven `[SO pickup]` lines in the Console. The command creates an isolated SO with a starting value of 37 and a channel that holds one simulated acknowledgement. It runs without credentials, a scene setup or either networking SDK. [Editor command](Examples/Pickups/Editor/SOPickupWalkthrough.cs)

To follow the calls live in VS Code, attach the debugger to the running Unity Editor and set a breakpoint inside `SODataController.Write`. Run the menu, step into `Assign`, then continue to the channel and `Changed` breakpoints below. The getter and setter share a source line, so the base `Write` breakpoint isolates assignments.

| Order | Set a breakpoint here | What to inspect |
| --- | --- | --- |
| 1 | `SODataController.OpenAsync` | The example fixes `DirectAccessOnline`. Opening slot `demo` reads the stored 37. [Controller](SODataController.cs) |
| 2 | `PickupController.OnPickup` → `SOPickupData.value.set` | Gameplay assigns `_pickupData.value = 5`. The property passes its backing field and the incoming 5 to `Write`. [Pickup](Examples/Pickups/PickupController.cs) · [Property](Examples/Pickups/SOPickupData.cs) |
| 3 | `SODataController.Write` → `SOStorageMode.Assign` → `SODirectAccessOnlineMode.SendAsync` | The setter changes the in-memory field. The channel receives scope `demo/SOPickupData`, key `value`, payload `{"value":5}` and expected revision `1`. [Assignment](SODataController.cs) · [Send](Modes/SODirectAccessOnlineMode.cs) |
| 4 | Return to `SOPickupWalkthrough.Run` after the first `OnPickup()` | The getter already returns 5. The callback count is still zero. |
| 5 | `SOStorageMode.Assign` on the second pickup, then `CloseAsync` | Assigning 5 again keeps the write count at one. Closing waits for the pending acknowledgement. |
| 6 | `WalkthroughStore.Confirm` → `SODataController.Changed` | The channel acknowledges the write. The SO invokes `DataChanged("value")` and the slot can close. |
| 7 | `OpenAsync` on the new `SOPickupData` instance | The new instance opens the same slot and reads 5 from the same simulated store. |

<details>
<summary>Recorded Unity Console output</summary>

```text
[SO pickup] 1. Opened DirectAccessOnline, value=37
[SO pickup] 2. Calling PickupController.OnPickup(), which assigns _pickupData.value = 5
[SO pickup] 3. Channel received scope=demo/SOPickupData, key=value, payload={"value":5}, revision=1
[SO pickup] 4. Getter reads 5, confirmations=0
[SO pickup] 5. Assigned 5 again, writes=1, close pending=True
[SO pickup] 6. DataChanged(value), confirmed value=5
[SO pickup] 7. Fresh SO opened the same slot, value=5
```

</details>

## Call it from gameplay

For a LocalFile or LocalFileOnline domain, change the property and read it back immediately. Both file modes share one five-minute checkpoint for dirty whole-file saves. To save earlier, await the SO's `FlushAsync()`. An existing file-backup adapter can subscribe once to `SnapshotCommitted` and use the supplied JSON directly. Identical assignments, reverted changes and idle checkpoints generate no backup event. [Snapshot lifecycle and measured file writes](../../../references/so-data-controller.md#batch-whole-file-saves)

Create a **Game Data/Pickups** asset and assign it to the pickup component. Share that instance with the session owner. After sign-in the owner calls `await pickupData.OpenAsync(slot, channel)` once. Gameplay can then write `pickupData.value = 5` and read `pickupData.value`. Before changing slots await `pickupData.CloseAsync()`. A regular offline SO starts loading its default slot on first access. Reads continue using its current fields. Initialize a balance or other delta-based domain in the bootstrap before accepting dependent commands. Every `await` here leaves the Unity thread available. [Working pickup](Examples/Pickups/PickupController.cs) · [Account wallet](Examples/SOCurrencyData.cs)

- **Unity Cloud Save**. Link the project to UGS, install `com.unity.services.cloudsave` (tested with 3.4.2) and enable `SO_DATA_UNITY_CLOUD_SAVE`. With an asmdef reference `Unity.Services.CloudSave`, `Unity.Services.Authentication` and `Unity.Services.Core` from the runtime assembly. Once authenticated, `await pickupData.OpenAsync(slot)` selects the included channel. Its `SavePlayerItemsAsync` calls the SDK. [Unity Cloud Save channel](Proviers/UnityCloudSaveKeyStore.cs)
- **Unity file backup**. Use LocalFileOnline and the same UGS package and define. Once authenticated, `await fileData.OpenAsync(playerSlot)` selects Unity Cloud Save Files. Include the environment and player identity in `playerSlot`. A different `ISODataFileStore` is selected with the named `fileProvider` parameter. [File mode](Modes/SOLocalFileOnlineMode.cs) · [Unity file provider](Proviers/UnityCloudSaveFileStore.cs)
- **Unity Netcode**. Install `com.unity.netcode.gameobjects` (tested with 2.14.0), configure Unity Transport and enable `SO_DATA_UNITY_NETCODE`. The channel assembly references `Unity.Netcode.Runtime` and `Unity.Collections`. Configure the scope on a registered server-owned NetworkObject before spawn. Each peer calls `await data.OpenAsync(slot, store)` on its spawned replica. A server assignment publishes a change to observers. [Unity Netcode channel](Proviers/SOUnityNetcodeStore.cs)
- **Another service**. Put one adapter under `Proviers/`. Implement `ISODataKeyStore` for account records or `ISORealtimeDataStore` for subscriptions and pass the adapter to the same `OpenAsync(slot, channel)` entry. [Account contract](Modes/SODirectAccessOnlineMode.cs) · [Subscription contract](Modes/SORealtimeOnlineMode.cs)

## Use an online file

The supplied asset fixes LocalFileOnline, and gameplay still uses its ordinary property.

```csharp
// SOFileData.cs
public sealed class SOFileData : SODataController
{
    [SerializeField] private int _value;
    public override SODataMode? RequiredMode => SODataMode.LocalFileOnline;
    public int value { get => Read(ref _value); set => Write(ref _value, value); }
}
```

Bind this SO once after authentication using `OpenAsync(playerSlot)`. Assign `fileData.value = 5` in gameplay. The shared checkpoint first commits the local encrypted JSON, then starts uploading through the file provider. `FlushAsync` requests an earlier checkpoint and returns the combined disk and remote acknowledgement. UI can keep reading and assigning values while this task is pending. The next pending file replaces older unsent state.

```text
value = 5 -> memory -> local encrypted file -> provider upload -> remote head
value = 6 while uploading -> memory stays 6 -> next local commit -> next upload
same value again -> no new dirty value, file upload or DataChanged event
```

`RefreshAsync` queries the remote revision. When it is unchanged, the file payload stays cached. A clean local copy can restore an updated remote snapshot. Edits made after that refresh began are retained. An offline backup remains pending in the local file and its acknowledgement record, so reopening can resume it. A genuine competing snapshot revision raises `SODataConflictException` and retains the local file. The lifecycle owner can select `KeepLocal` or `UseRemote` through `ResolveFileConflictAsync`; both paths retain edits arriving during the resolution.

Implement `ISODataFileStore` in one `Proviers/<Provider>FileStore.cs` for another service. Its methods return the current revision, download the acknowledged bytes and conditionally upload a complete file. Pass that adapter through `OpenAsync(playerSlot, fileProvider: provider)`. Keep project identifiers, environments and credentials in the receiving project's configuration.

## Asynchronous lifecycle

Assignments update memory and enqueue background work. `Initialization` exposes the initial load, `IsReady` indicates its completion, and `DataChanged` updates consumers after hydration or the accepted visible value. A late reply cannot replace a newer local assignment. Equal values and equal pushed payloads generate no extra change event.

Use `await CloseAsync()` at a slot or account transition to confirm pending work. `CloseData()` begins retirement immediately and exposes it as `Retirement`; reopening the same identity awaits it asynchronously. All native disk operations run on workers. DirectAccess now confirms durability through its task, and coalesces pending states. A process terminated before its disk acknowledgement can lose uncommitted changes. Keep transactional currency grants and inventory receipts with their existing domain owner.
