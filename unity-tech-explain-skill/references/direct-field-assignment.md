# Direct ScriptableObject field assignment

## Brief explanation

The shared pickup template has a public integer called `value`. Drag the same pickup data asset onto the pickup component and into the storage profile's Domains list. Once the signed-in slot is ready, assigning five immediately changes the visible number and queues that field for Unity Cloud Save.

## Concrete code

```csharp
// SOPickupData.cs
public sealed class SOPickupData : SODataController
{
    public override SODataMode? RequiredMode => SODataMode.DirectAccessOnline;
    public int value;
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

The account flow passes the selected slot to the profile once after sign-in. The controller shown above is the entire gameplay caller. [Pickup source files](https://github.com/qinbatista/qin-unity-skills/tree/master/unity-code-skill/assets/Examples/Pickups) · [Storage profile](https://github.com/qinbatista/qin-unity-skills/blob/master/unity-code-skill/assets/SODataProfile.cs)

## Detailed explanation and steps

```text
OnPickup → value = 5 → SODataController observes "value"
  → Cloud Save receives {"value":5} for the field key and its previous write lock
  → service confirms → re-enter the same account and slot → value reads 5
```

For an existing record the SDK request contains one item. The SO reports confirmation through DataChanged and errors through StorageFailed. On an orderly slot exit the profile waits for outstanding replies. [Unity Cloud Save adapter](https://github.com/qinbatista/qin-unity-skills/blob/master/unity-code-skill/assets/Providers/UnityCloudSave/UnityCloudSaveKeyStore.cs)
