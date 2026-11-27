using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Currency")]
public sealed class SOCurrencyData : SODataController
{
    [SerializeField] private int coins;
    public int Coins => coins;

    protected override void DefineDataKeys(SODataFields fields) =>
        fields.Bind(nameof(coins), () => coins, value => coins = value);

    // Existing gameplay logic and fields can stay intact.
    public void CreditCoins(int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        coins = checked(coins + amount);
    }

    public Task CreditCoinsAsync(int amount, CancellationToken cancellation = default) =>
        CommitChangeAsync(nameof(coins), () => CreditCoins(amount), cancellation);
}
