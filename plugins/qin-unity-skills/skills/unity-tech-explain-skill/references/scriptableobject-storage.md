# ScriptableObject storage example

*My wallet SO already has a coin balance. Where should adding three coins happen?*

## Brief explanation

This shared template shows a player-writable pickup balance on a signed-in account. The Inspector displays the wallet's fixed **Storage Mode → Direct Access Online**. Reading `wallet.Coins` shows the current in-memory balance. A game with server-issued currency sends the validated grant to its ledger and displays the balance returned by that service.

## Concrete code

The getter is public and the setter is private. The field alias preserves authored assets and the key binding selects the remote `coins` record. [SOCurrencyData.cs](https://github.com/qinbatista/qin-unity-skills/blob/master/unity-code-skill/assets/SOCurrencyData.cs)

```csharp
// SOCurrencyData.cs
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
```

After sign-in, open the wallet's runtime copy with `await wallet.OpenAsync("slot-a", new UnityCloudSaveKeyStore())`. The pickup calls `wallet.CreditCoins(3)`, which queues a cloud write immediately. After `await wallet.FlushAsync()` succeeds, the UI can present `wallet.Coins` as confirmed. [Unity Cloud Save setup](https://github.com/qinbatista/qin-unity-skills/blob/master/unity-code-skill/references/so-unity-cloud-save.md)

## Detailed explanation and steps

```text
Cloud balance 37 → CreditCoins(3) → memory shows 40 → remote write pending
  ├─ FlushAsync succeeds → cloud confirms 40 → reopen the same account and slot → 40
  └─ acknowledgement is lost → reopen and read the service before retrying the pickup
```

Use the same environment and storage ID on restart. A service rejection restores the last accepted balance in memory. After a timeout, the service might already have committed the write.
