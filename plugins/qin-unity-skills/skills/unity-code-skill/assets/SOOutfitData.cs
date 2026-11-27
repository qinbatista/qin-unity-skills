using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Outfit")]
public sealed class SOOutfitData : SODataController
{
    [SerializeField] private string hatId = "";
    [SerializeField] private string bootsId = "";
    public string HatId => hatId;
    public string BootsId => bootsId;

    protected override void DefineDataKeys(SODataFields fields)
    {
        fields.Bind(nameof(hatId), () => hatId, value => hatId = value);
        fields.Bind(nameof(bootsId), () => bootsId, value => bootsId = value);
    }

    public Task WearHatAsync(string id) => CommitChangeAsync(nameof(hatId), () => hatId = id);
    public Task WearBootsAsync(string id) => CommitChangeAsync(nameof(bootsId), () => bootsId = id);
}
