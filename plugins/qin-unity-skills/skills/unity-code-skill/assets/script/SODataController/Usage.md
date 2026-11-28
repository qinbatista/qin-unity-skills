# Use SODataController in a Unity project

Copy the Skill's `assets/script` folder into the receiving project's `Assets`. Its runtime entry is `Assets/script/SODataController/SODataController.cs`. Install `com.unity.nuget.newtonsoft-json` (tested with 3.2.2). If the project uses assembly definitions, the runtime assembly needs a reference to `Unity.Newtonsoft.Json`. When moving an existing game script, move its Unity `.meta` with it so existing asset references retain their GUID.

```text
Assets/script/SODataController/
  SODataController.cs
  Modes/                    one implementation for each of the five modes
  Proviers/UnityCloudSaveKeyStore.cs   persistent account data
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

Create a **Game Data/Pickups** asset and assign it to the pickup component. Share that instance with the session owner. After sign-in the owner calls `await pickupData.OpenAsync(slot, channel)` once. Gameplay can then write `pickupData.value = 5` and read `pickupData.value`. Before changing slots await `pickupData.CloseAsync()`. A regular offline SO opens its default slot on first access. [Working pickup](Examples/Pickups/PickupController.cs) · [Account wallet](Examples/SOCurrencyData.cs)

- **Unity Cloud Save**. Link the project to UGS, install `com.unity.services.cloudsave` (tested with 3.4.2) and enable `SO_DATA_UNITY_CLOUD_SAVE`. With an asmdef reference `Unity.Services.CloudSave`, `Unity.Services.Authentication` and `Unity.Services.Core` from the runtime assembly. Once authenticated, `await pickupData.OpenAsync(slot)` selects the included channel. Its `SavePlayerItemsAsync` calls the SDK. [Unity Cloud Save channel](Proviers/UnityCloudSaveKeyStore.cs)
- **Unity Netcode**. Install `com.unity.netcode.gameobjects` (tested with 2.14.0), configure Unity Transport and enable `SO_DATA_UNITY_NETCODE`. The channel assembly references `Unity.Netcode.Runtime` and `Unity.Collections`. Configure the scope on a registered server-owned NetworkObject before spawn. Each peer calls `await data.OpenAsync(slot, store)` on its spawned replica. A server assignment publishes a change to observers. [Unity Netcode channel](Proviers/SOUnityNetcodeStore.cs)
- **Another service**. Put one adapter under `Proviers/`. Implement `ISODataKeyStore` for account records or `ISORealtimeDataStore` for subscriptions and pass the adapter to the same `OpenAsync(slot, channel)` entry. [Account contract](Modes/SODirectAccessOnlineMode.cs) · [Subscription contract](Modes/SORealtimeOnlineMode.cs)
