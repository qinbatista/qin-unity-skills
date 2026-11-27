using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Source compatibility for games using the original asynchronous sample API.
public static class SOFighterPositionDataExtensions
{
    public static Task SetPositionAsync(this SOFighterPositionData fighter, Vector3 worldPosition, CancellationToken cancellation = default) =>
        fighter.CommitChangeAsync("position", () => fighter.SetPosition(worldPosition), cancellation);
}
