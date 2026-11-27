# ScriptableObject storage example

*My wallet SO already has a coin balance. Where should adding three coins happen?*

## Brief explanation

In this shared template, `SOCurrencyData` owns the balance. Gameplay reads `wallet.Coins` and calls `wallet.CreditCoins(3)` when a pickup succeeds. The SO checks the amount and changes its own private field.

## Concrete code

The serialized field keeps its existing name, `coins`. The getter is public and the setter is private, so other classes can read the balance and the wallet controls each credit. [SOCurrencyData.cs](https://github.com/qinbatista/qin-unity-skills/blob/master/unity-code-skill/assets/SOCurrencyData.cs)

```csharp
// SOCurrencyData.cs
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
```

Choose `DirectAccess` on the wallet asset. At profile entry the bootstrap opens its runtime copy with `await wallet.OpenAsync("slot-a")`. The pickup calls `wallet.CreditCoins(3)` and the UI reads `wallet.Coins`.

## Detailed explanation and steps

```text
Slot holds 37 → CreditCoins(3) → the setter writes coins = 40 → the local key is committed
Restart → open the same slot and storage ID → wallet.Coins returns 40
```

The controller observes the backing-field assignment inside the setter. With native DirectAccess the record is on disk when the credit method returns.
