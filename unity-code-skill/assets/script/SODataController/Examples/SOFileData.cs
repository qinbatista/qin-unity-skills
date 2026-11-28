using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Online File")]
public sealed class SOFileData : SODataController
{
    [SerializeField] private int _value;
    public override SODataMode? RequiredMode => SODataMode.LocalFileOnline;
    public int value { get => Read(ref _value); set => Write(ref _value, value); }
}
