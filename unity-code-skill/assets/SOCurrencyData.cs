using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Currency")]
public sealed class SOCurrencyData : SODataController
{
    [SerializeField] private int coins;
    public int Coins => coins;

    public void CreditCoins(int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        coins = checked(coins + amount);
    }
}
