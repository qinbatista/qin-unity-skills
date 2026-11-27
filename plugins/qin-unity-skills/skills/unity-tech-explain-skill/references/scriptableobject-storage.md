# ScriptableObject storage example

*My wallet SO already has a coin balance. Where should adding three coins happen?*

## Brief explanation

In this shared template, `wallet.Coins` reads the balance and `wallet.CreditCoins(3)` adds a pickup. The wallet class fixes its mode to `DirectAccess`. Its Inspector shows **Storage Mode → Direct Access** as read-only.

## Concrete code

The getter is public and the setter is private. The field alias and key binding preserve existing assets and the saved `coins` record. [SOCurrencyData.cs](https://github.com/qinbatista/qin-unity-skills/blob/master/unity-code-skill/assets/SOCurrencyData.cs)

```csharp
// SOCurrencyData.cs
public sealed class SOCurrencyData : SODataController
{
    [SerializeField, FormerlySerializedAs("coins")] private int _coins;
    public override SODataMode? RequiredMode => SODataMode.DirectAccess;
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

At profile entry open the wallet's runtime copy with `await wallet.OpenAsync("slot-a")`. The pickup calls `wallet.CreditCoins(3)` and the UI reads `wallet.Coins`. Crediting a closed wallet throws before the balance changes.

## Detailed explanation and steps

```text
Slot holds 37 → CreditCoins(3) → the setter writes _coins = 40 → the local coins key is committed
Restart → open the same slot and storage ID → wallet.Coins returns 40
```

The controller observes the backing-field assignment inside the setter. With native DirectAccess the record is on disk when the credit method returns.
