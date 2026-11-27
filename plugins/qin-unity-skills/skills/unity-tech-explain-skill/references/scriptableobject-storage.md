# ScriptableObject storage example

*Can an existing wallet keep its own ordinary field and method while the controller saves a pickup?*

## Brief explanation

Yes. The pack's currency method only changes its balance. A separate controller can also attach to a game-owned plain ScriptableObject. For native DirectAccess, the assignment writes only the changed field before returning.

## Concrete code

**`unity-code-skill/assets/SOCurrencyData.cs` · `SOCurrencyData.CreditCoins`** (pack sample)

```csharp
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
```

Bootstrap opens the runtime clone with `await wallet.OpenAsync("slot-a")`. The pickup calls `wallet.CreditCoins(3)`. With an existing plain SO, bootstrap uses `await storage.AttachAsync(wallet, "slot-a")` instead. Its own `wallet.money += 3` call works in the same way.

## Detailed explanation and steps

In `unity-code-skill/assets/SODataController.cs`, `SODataController.AttachAsync` loads the matching profile and binds the runtime object. The Editor compiler places an observer after regular field assignments. In `unity-code-skill/assets/SOKeySession.cs`, `SOKeySession.Assigned` captures that field.

```text
slot holds 37 → CreditCoins(3) → coins becomes 40
  ├─ DirectAccess → native coins record is flushed → reopen slot → 40
  ├─ DirectAccessOnline → request pending → await FlushAsync → cloud accepts 40
  └─ None → runtime clone holds 40 → reopen → authored balance
```

Use the receiving game's real class, writer, profile and storage mode when explaining that game's behavior. For an in-place collection edit, distinguish the next automatic checkpoint from an explicit immediate flush.
