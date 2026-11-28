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

The account owner awaits `pickupData.OpenAsync(slot, channel)` once after sign-in. Source for the [SO property](../../unity-code-skill/assets/Examples/Pickups/SOPickupData.cs) and the [pickup component](../../unity-code-skill/assets/Examples/Pickups/PickupController.cs).

## Detailed explanation and steps

```text
PickupController.OnPickup → value = 5 → SOPickupData.value setter → SODataController.Write
  → DirectAccessOnline → Cloud Save receives {"value":5} → acknowledgement
  → fresh SO opens the same account and slot → value reads 5
```

A breakpoint in the property setter shows the assigned integer. The provider receives only this field. [Mode](../../unity-code-skill/assets/Modes/SODirectAccessOnlineMode.cs) · [Unity channel](../../unity-code-skill/assets/Providers/UnityCloudSave/UnityCloudSaveKeyStore.cs)
