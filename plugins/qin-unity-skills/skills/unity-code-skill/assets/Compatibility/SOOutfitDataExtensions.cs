using System.Threading.Tasks;

// Source compatibility for games using the original asynchronous sample API.
public static class SOOutfitDataExtensions
{
    public static Task WearHatAsync(this SOOutfitData outfit, string id) =>
        outfit.CommitChangeAsync("hatId", () => outfit.WearHat(id));

    public static Task WearBootsAsync(this SOOutfitData outfit, string id) =>
        outfit.CommitChangeAsync("bootsId", () => outfit.WearBoots(id));
}
