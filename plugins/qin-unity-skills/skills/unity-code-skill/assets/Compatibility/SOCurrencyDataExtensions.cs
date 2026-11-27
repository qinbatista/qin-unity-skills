using System.Threading;
using System.Threading.Tasks;

// Source compatibility for games using the original asynchronous sample API.
public static class SOCurrencyDataExtensions
{
    public static Task CreditCoinsAsync(this SOCurrencyData wallet, int amount, CancellationToken cancellation = default) =>
        wallet.CommitChangeAsync("coins", () => wallet.CreditCoins(amount), cancellation);
}
