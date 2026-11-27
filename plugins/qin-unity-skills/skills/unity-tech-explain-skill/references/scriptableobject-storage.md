# ScriptableObject storage example

*How does the packaged wallet keep coins after I restart the game?*

## Brief explanation

The active `SOPlayerWallet` holds the balance in memory during play. `SOLocalData` writes a checksummed snapshot to `wallet.json` under `Application.persistentDataPath` and reads that file into the active wallet on the next start. A game's authored `.asset`, when used, can supply initial configuration for its runtime instance.

## Concrete code

**`unity-code-skill/assets/SOPlayerWallet.cs` · `SOPlayerWallet.CreditCoins`** (pack sample)

```csharp
public sealed class SOPlayerWallet : SOLocalData
{
    // Other members omitted
    public void CreditCoins(int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        coin = checked(coin + amount);
        RequestAutoSave();
    }
}
```

Bind the reward handler to the active wallet and call `wallet.CreditCoins(3)` when it grants three coins. The method updates the in-memory balance, then asks the save owner to persist it.

**`unity-code-skill/assets/SOLocalData.cs` · `SOLocalData.RequestAutoSave`** (pack sample)

```csharp
public abstract class SOLocalData : ScriptableObject, ISerializationCallbackReceiver
{
    // Other members omitted
    protected void RequestAutoSave(float cooldownSeconds = 0.5f)
    {
        if (_writeBlocked || !Application.isPlaying || Time.unscaledTime - _lastAutoSaveTime < cooldownSeconds) return;
        SaveDataAtLocal();
        _lastAutoSaveTime = Time.unscaledTime;
    }
}
```

When saving is allowed and the cooldown has elapsed, `SaveDataAtLocal()` commits the JSON. A gameplay checkpoint can call `wallet.SaveDataAtLocal()` explicitly so a recent balance change skipped by the cooldown reaches the file.

## Detailed explanation and steps

- `SaveDataAtLocal()` serializes the selected fields and checksum, writes a temporary sibling file, then moves or replaces it at `wallet.json`.
- At startup, the wallet's `OnEnable()` calls `LoadLocalData()` in Play Mode. The wallet validates the file before restoring the balance. A missing file initializes and saves the defaults. An invalid file blocks writes and stays available for recovery.

```text
CreditCoins(3) → wallet balance 37 → 40
  ├─ Save succeeds     → wallet.json = 40 → restart restores 40
  └─ Exit before saving → wallet.json = 37 → restart restores 37
```
