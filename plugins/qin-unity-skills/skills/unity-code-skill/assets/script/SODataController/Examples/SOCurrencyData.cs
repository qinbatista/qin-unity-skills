using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Game Data/Currency")]
public sealed class SOCurrencyData : SODataController
{
    [SerializeField, FormerlySerializedAs("coins")] private int _coins;
    public override SODataMode? RequiredMode => SODataMode.DirectAccessOnline;
    public int Coins { get => Read(ref _coins); set => Write(ref _coins, value); }
}
