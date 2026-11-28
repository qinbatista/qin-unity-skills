# Unity Cloud Save for DirectAccessOnline

The optional [UnityCloudSaveKeyStore](../assets/Providers/UnityCloudSave/UnityCloudSaveKeyStore.cs) stores one discovered field per Cloud Save Player Data key. It uses the signed-in player in the initialized Unity Services environment. Use this adapter for player-writable data. Currency issued by a purchase, leaderboard rewards and other server-authoritative changes belong behind a validated server operation.

## Connect a game

- Link the receiving project to its own Unity Project ID in Project Settings, select its UGS environment at initialization, enable Cloud Save and sign in through the game's Authentication provider. The template contains no project ID or credentials. Linked accounts can restore on another device with the same Unity Player ID and environment. [Unity setup](https://docs.unity.com/en-us/cloud-save/get-started)
- Install `com.unity.services.cloudsave` 3.4.2. Copy both files from [the Unity provider directory](../assets/Providers/UnityCloudSave/SOUnityCloudSaveProfile.cs) into an assembly that sees the storage controller and profile. Reference `Unity.Services.CloudSave`, `Unity.Services.Authentication` and `Unity.Services.Core` and add the scripting define `SO_DATA_UNITY_CLOUD_SAVE` to that assembly. An asmdef version define for package `com.unity.services.cloudsave` with expression `3.4.2` can supply it automatically. Create the profile under **Game Data → Storage Profile → Unity Cloud Save**.
- Assign a stable **Storage Id** to each online data asset and put it in the profile's Domains list. Pass the same asset to gameplay. After sign-in the account flow awaits `profile.EnterAsync(slot)` before enabling input. The profile constructs the Unity adapter here. For a custom cloud service pass its provider to `EnterAsync` once.
- A [pickup](../assets/Examples/Pickups/PickupController.cs) assigns `_pickupData.value = 5;`. A wallet pickup calls `wallet.CreditCoins(3);`. The SO submits the changed field immediately and publishes its confirmation. The account flow awaits `profile.LeaveAsync()` before releasing the signed-in session.

On restart, entering the same account, environment and slot restores committed values. `UnityCloudSaveKeyStore.GetCloudKey(wallet.GetStorageScope("slot-a"), "coins")` returns the wallet's exact Dashboard key.

## Write and recover

Updates send a single named key and the last acknowledged write lock. Creating a field first reads a reserved per-player fence, then checks whether the field exists. One atomic Default-access batch advances the fence using its write lock and creates the field. Two clients that see the same missing field cannot both insert it. Concurrent fence initialization only changes a random nonce. Keep the adapter as the sole writer of its reserved keys. Distinct first-time inserts can also contend on the fence. [Write locks](https://docs.unity.com/en-us/cloud-save/concepts/write-locks) and [Cloud Save atomic batch API](https://docs.unity.com/en-us/oas-cloud-save-data-client/1.0.0)

A conflict leaves the winning value on the service. On a timeout, session change or lost acknowledgement, close and reopen for the intended account, read the authoritative field, then let gameplay reconcile the original intent. The server may already have committed an unacknowledged write. A repeated reward needs a server-side operation ID and a deduplicating transaction when exactly-once delivery is required. Exercise cross-device restoration on a test-owned slot after linking the receiver.
