# ScriptableObject storage example

## Brief explanation

An account wallet selects DirectAccessOnline. Its `Coins` property reads the runtime balance and routes assignments to the selected account's store. Games with server-issued money display the balance confirmed by their validated grant endpoint.

## Concrete code

```csharp
// SOCurrencyData.cs
public sealed class SOCurrencyData : SODataController
{
    [SerializeField, FormerlySerializedAs("coins")] private int _coins;
    public override SODataMode? RequiredMode => SODataMode.DirectAccessOnline;
    public int Coins { get => Read(ref _coins); set => Write(ref _coins, value); }
}
```

After opening the account slot, the pickup assigns `wallet.Coins += 3` and the display reads `wallet.Coins`. The serialized field preserves the existing asset value and storage key `coins`. The SO raises `DataChanged` on acknowledgement. [Wallet source](../../unity-code-skill/assets/SOCurrencyData.cs)

## Detailed explanation and steps

```text
Cloud balance 37 → wallet.Coins += 3 → memory shows 40 → remote write pending
  ├─ acknowledged → DataChanged("coins") → reopen same slot → 40
  └─ reply lost → reopen and read the service before deciding whether to retry
```
