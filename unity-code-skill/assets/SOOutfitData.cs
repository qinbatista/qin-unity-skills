using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Outfit")]
public sealed class SOOutfitData : SODataController
{
    [SerializeField] private string hatId = "";
    [SerializeField] private string bootsId = "";
    public string HatId => hatId;
    public string BootsId => bootsId;

    public void WearHat(string id) => hatId = id;
    public void WearBoots(string id) => bootsId = id;
}
