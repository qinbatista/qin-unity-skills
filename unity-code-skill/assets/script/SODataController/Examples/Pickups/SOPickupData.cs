using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Game Data/Pickups")]
public sealed class SOPickupData : SODataController
{
    [SerializeField, FormerlySerializedAs("value")] private int _value;
    public override SODataMode? RequiredMode => SODataMode.DirectAccessOnline;
    public int value { get => Read(ref _value); set => Write(ref _value, value); }
}
