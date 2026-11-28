using UnityEngine;

public sealed class PickupController : MonoBehaviour
{
    [SerializeField] private SOPickupData _pickupData;

    public void OnPickup() => _pickupData.value = 5;
}
