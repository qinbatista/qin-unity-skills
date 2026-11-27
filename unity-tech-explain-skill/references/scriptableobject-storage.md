# ScriptableObject storage example

*How does the packaged wallet keep coins after I restart the game?*

## 1. Principle

The active `SOPlayerWallet` holds the balance in memory during play. `SOLocalData` writes a checksummed snapshot to `wallet.json` under `Application.persistentDataPath` and reads that file into the active wallet on the next start. A game's authored `.asset`, when used, can supply initial configuration for its runtime instance.

## 2. Code and usage

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

## 3. Steps and diagram

```text
Play Mode startup -> OnEnable() -> LoadLocalData() -> active wallet (37 coins)
Reward -> CreditCoins(3) -> active wallet (40 coins)
Autosave or checkpoint -> SaveDataAtLocal() -> wallet.json (40 coins)
Next startup -> LoadLocalData() -> active wallet (40 coins)
```

1. When the runtime wallet is enabled in Play Mode, `OnEnable()` calls `LoadLocalData()`. A missing file initializes the defaults and saves them. An invalid existing file blocks writes and leaves the file available for recovery.
2. `CreditCoins(3)` changes a loaded balance of 37 to 40. On a successful save, the owner serializes the selected fields, writes a temporary sibling file, then moves or replaces it at `wallet.json`.
3. The next startup validates and restores `coin = 40`. If the game adds cloud sync, its adapter uploads the committed data for the active account and slot. A remote restore needs validation and a local commit before applying the restored data to the active wallet. The code shown here is the local save path.
