using System;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Game Data/Currency")]
public sealed class SOCurrencyData : SODataController
{
    [SerializeField, FormerlySerializedAs("coins")] private int _coins;
    public override SODataMode? RequiredMode => SODataMode.DirectAccessOnline;
    public int Coins { get => _coins; private set => _coins = value; }

    protected override void DefineDataKeys(SODataFields fields) => fields.Bind(nameof(_coins), () => Coins, value => Coins = value, "coins");

    public void CreditCoins(int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        RequireOpenStorage();
        Coins = checked(Coins + amount);
    }
}
