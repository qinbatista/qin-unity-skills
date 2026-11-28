# ScriptableObject property assignment

## Brief explanation

The pickup component uses the same SO instance opened by the account flow. Assigning five enters the C# property setter on that SO.

## Concrete code

```csharp
// SOPickupData.cs
public sealed class SOPickupData : SODataController
{
    [SerializeField, FormerlySerializedAs("value")] private int _value;
    public override SODataMode? RequiredMode => SODataMode.DirectAccessOnline;
    public int value { get => Read(ref _value); set => Write(ref _value, value); }
}
```

```csharp
// PickupController.cs
public sealed class PickupController : MonoBehaviour
{
    [SerializeField] private SOPickupData _pickupData;

    public void OnPickup() => _pickupData.value = 5;
}
```

After sign-in, the account owner awaits `pickupData.OpenAsync(slot, channel)` once. The included Editor walkthrough passes a private simulated channel for this run. Both execute the same pickup component and SO setter. [SO property](../../unity-code-skill/assets/script/SODataController/Examples/Pickups/SOPickupData.cs) · [Pickup component](../../unity-code-skill/assets/script/SODataController/Examples/Pickups/PickupController.cs)

## Detailed explanation and steps

After copying the Skill's `assets/script` into Unity's `Assets`, select **Tools → SODataController → Run Pickup Walkthrough**. Follow the seven Console entries or step into these calls in VS Code. [Editor command](../../unity-code-skill/assets/script/SODataController/Examples/Pickups/Editor/SOPickupWalkthrough.cs)

1. `OpenAsync` chooses DirectAccessOnline and loads 37 from the example slot.
2. `PickupController.OnPickup` assigns 5. C# invokes `SOPickupData.value.set`, which passes `ref _value` and 5 to `SODataController.Write`.
3. `SOStorageMode.Assign` changes the backing field. The mode's `Save` queues `SendAsync`. Its channel receives key `value`, payload `{"value":5}` and revision `1`. [Dispatch](../../unity-code-skill/assets/script/SODataController/SODataController.cs) · [Send](../../unity-code-skill/assets/script/SODataController/Modes/SODirectAccessOnlineMode.cs)
4. The gameplay getter already returns 5. The pending acknowledgement keeps the callback count at zero.
5. The same assignment runs again. `Assign` compares the serialized value, skips the duplicate and leaves one outgoing write. `CloseAsync` is still waiting.
6. The demo releases the receipt. `SendAsync` accepts the new revision and calls `Owner.Changed("value")`. The SO raises `DataChanged("value")`.
7. A new SO opens the same slot on the same store. Its getter reads 5.

In a receiver configured for Unity Cloud Save, the channel call at step 3 enters `UnityCloudSaveKeyStore.WriteAsync` and ultimately calls `_playerData.SaveAsync`. Its returned write lock supplies the acknowledged revision at step 6. [Unity channel](../../unity-code-skill/assets/script/SODataController/Proviers/UnityCloudSaveKeyStore.cs)
