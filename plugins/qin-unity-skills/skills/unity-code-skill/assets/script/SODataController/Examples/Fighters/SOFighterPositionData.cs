using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Fighter Position")]
public sealed class SOFighterPositionData : SODataController
{
    [SerializeField] private Vector3 position;
    public override SODataMode? RequiredMode => SODataMode.RealtimeOnline;
    public Vector3 Position { get => Read(ref position); set => Write(ref position, value); }
}
