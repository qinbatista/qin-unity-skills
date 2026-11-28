using UnityEngine;

// A local wallet from an earlier game version. Its previous file and field keys remain readable.
[CreateAssetMenu(menuName = "Game Data/Local Wallet")]
public sealed class SOPlayerWallet : SODataController
{
    [SerializeField] private int coin;
    [SerializeField] private int money;
    public override SODataMode? RequiredMode => SODataMode.LocalFile;
    protected override string LegacyFileName => "wallet.json";
    public int Coin { get => Read(ref coin); set => Write(ref coin, value); }
    public int Money { get => Read(ref money); set => Write(ref money, value); }
}
