# ScriptableObject storage example

*When I collect three coins, when are they written and how does a new session recover them?*

## Brief explanation

In the pack's `DirectAccess` currency example, the runtime SO holds the displayed balance. Each successful reward writes only the currency key to the device. A new clone reads the committed balance when the game opens the same profile and storage ID.

## Concrete code

**`unity-code-skill/assets/SOCurrencyData.cs` · `SOCurrencyData.CreditCoins` and `CreditCoinsAsync`** (pack sample)

```csharp
public sealed class SOCurrencyData : SODataController
{
    // Other members omitted
    public void CreditCoins(int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        coins = checked(coins + amount);
    }

    public Task CreditCoinsAsync(int amount, CancellationToken cancellation = default) =>
        CommitChangeAsync(nameof(coins), () => CreditCoins(amount), cancellation);
}
```

The pickup owner calls `await wallet.CreditCoinsAsync(3)` before showing the new balance. `unity-code-skill/assets/SOKeySession.cs` · `SOKeySession.ChangeAsync` stages that one field, awaits the store's conditional write and applies the acknowledged result.

## Detailed explanation and steps

Set the currency asset to `DirectAccess`. Bootstrap clones it, then calls `await wallet.OpenAsync("slot-a")`. The disk reader loads the accepted key or retains the authored initial balance for a new profile.

```text
slot-a holds 37 → CreditCoinsAsync(3) → write the coins key against its revision
  ├─ device acknowledges → display 40 → restart opens slot-a → 40
  └─ stale revision      → the other writer wins → reopen reads the winning value
```

With `None`, the same reward changes the shared clone for this session. A new clone starts from the authored balance. Use the receiving game's real storage owner, calls and save format when explaining its behavior.
