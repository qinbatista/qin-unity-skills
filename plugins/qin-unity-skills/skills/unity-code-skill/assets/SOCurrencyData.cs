using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Currency")]
public sealed class SOCurrencyData : SODataController
{
    [SerializeField] private int coins;

    public int Coins
    {
        get => coins;
        private set => coins = value;
    }

    public void CreditCoins(int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        Coins = checked(Coins + amount);
    }
}
