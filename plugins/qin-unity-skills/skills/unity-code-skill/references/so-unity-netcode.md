# RealtimeOnline with Unity Netcode

Use a server-owned NetworkObject for one match data domain. Its [SOUnityNetcodeStore](../assets/script/SODataController/Proviers/SOUnityNetcodeStore.cs) pushes changed keys to the observing clients over the existing Netcode connection. It keeps the latest value in the running match. A further join to that match receives the current snapshot. A new server process starts a fresh match.

## Connect the template

- Install `com.unity.netcode.gameobjects` in the receiver and configure a client/server `NetworkManager` with Unity Transport. The native fixture targets Unity 6000.6.7f1 and NGO 2.14.0. Enable `SO_DATA_UNITY_NETCODE` in the runtime assembly that contains `Proviers/SOUnityNetcodeStore.cs`, including when upgrading a previously copied adapter. That assembly must reference the SO contracts, `Unity.Netcode.Runtime` and `Unity.Collections`.
- Put `NetworkObject` and `SOUnityNetcodeStore` on the network prefab. Register it in the NetworkManager's prefab list on every peer. On the dedicated server or host, select the actual match/profile and SO domain before spawning. For a fixed scope, the prefab has an Inspector default.

```csharp
// state is a runtime clone in RealtimeOnline mode. networkPrefab is a registered NetworkObject.
NetworkObject instance = Instantiate(networkPrefab);
SOUnityNetcodeStore store = instance.GetComponent<SOUnityNetcodeStore>();
store.SetScopeBeforeSpawn(state.GetStorageScope(profile));
instance.Spawn();
await state.OpenAsync(profile, store);
```

- On an observing client, obtain the store on the locally spawned replica of that object. Bind the client's runtime SO using the same `profile` and storage ID. A sibling `NetworkBehaviour.OnNetworkPostSpawn` can start this binding after every component on the prefab has spawned. Await `OpenAsync` before consuming the SO. The provider returns the current value or absence during subscription and then delivers changes on Unity's main thread.
- Apply each validated gameplay change on the server. For the [fighter example](../assets/script/SODataController/Examples/Fighters/SOFighterPositionData.cs), `state.Position = worldPosition` publishes the new position. Await `state.FlushAsync()` at the acknowledgement boundary. A client attempting a raw write receives `UnauthorizedAccessException`. Send gameplay inputs through the receiving game's authenticated command path and validate sender, actor ownership and game rules on the server. Restrict the object's observers using the game's visibility policy when the state is private.
- Call `state.CloseData()` at session end or from the owning consumer's `OnNetworkDespawn`. Reopen the new replica after reconnect. Every observer of this particular object can read every replicated field in it. Keep domains with different visibility on different objects.

```text
Server accepts command → SO commits one key → server NetworkList changes
                                              ├─ current observer → delta → client SO
                                              └─ late observer → snapshot → client SO
```

## Fields and simulation

The [fighter position example](../assets/script/SODataController/Examples/Fighters/SOFighterPositionData.cs) contains one serialized `Vector3`. Create one runtime instance per observed fighter; include both the match ID and the server-assigned actor ID in the profile. After the server accepts a move, assign `fighter.Position = new Vector3(4.5f, 0, -2)`. The opponent's client receives the new `fighter.Position` and its `DataChanged` event for `position`. A local marker can read the SO in that callback.

The adapter admits 64 keys per object. UTF-8 capacity is 61 bytes per key and 509 bytes per serialized value or scope. The server checks the last revision on every write. NGO sends changes on network ticks and the SO accepts pushed values without a per-frame poll. A successful server write acknowledges the server's memory update. Each connection has its own delivery and latency.

For discrete state, use a few small SO fields per actor or match domain. For continuous movement and rotations, bind the authoritative transform to NGO's `NetworkTransform` for its interpolation and delivery controls. Prediction, combat validation and lag compensation belong to the receiving game's simulation. [Unity's Netcode network variables](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.14/manual/basics/networkvariable.html) and [NetworkTransform](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.14/manual/components/helper/networktransform.html).
