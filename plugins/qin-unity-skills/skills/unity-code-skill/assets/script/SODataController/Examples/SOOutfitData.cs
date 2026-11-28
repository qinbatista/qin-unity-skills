using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Outfit")]
public sealed class SOOutfitData : SODataController
{
    [SerializeField] private string hatId = "";
    [SerializeField] private string bootsId = "";
    public string HatId { get => Read(ref hatId); set => Write(ref hatId, value); }
    public string BootsId { get => Read(ref bootsId); set => Write(ref bootsId, value); }
}
