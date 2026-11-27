using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Fighter Position")]
public sealed class SOFighterPositionData : SODataController
{
    [SerializeField] private Vector3 position;
    public Vector3 Position => position;

    protected override void DefineDataKeys(SODataFields fields) =>
        fields.Bind(nameof(position), () => position, value => position = value);

    // The server supplies a position it already accepted from its simulation.
    public Task SetPositionAsync(Vector3 worldPosition, CancellationToken cancellation = default) =>
        CommitChangeAsync(nameof(position), () => position = worldPosition, cancellation);
}
