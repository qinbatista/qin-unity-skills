using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Fighter Position")]
public sealed class SOFighterPositionData : SODataController
{
    [SerializeField] private Vector3 position;
    public Vector3 Position => position;

    public void SetPosition(Vector3 worldPosition) => position = worldPosition;
}
