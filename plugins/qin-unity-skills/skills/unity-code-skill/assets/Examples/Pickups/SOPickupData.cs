using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Pickups")]
public sealed class SOPickupData : SODataController
{
    public override SODataMode? RequiredMode => SODataMode.DirectAccessOnline;
    public int value;
}
