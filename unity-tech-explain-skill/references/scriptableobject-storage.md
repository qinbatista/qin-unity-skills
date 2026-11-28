# ScriptableObject storage example

## Brief explanation

This example creates a client-writable account wallet for a game that has no wallet yet. Its `Coins` property uses DirectAccessOnline. When a game already owns its balance, follow that wallet's public API. For server-issued money, keep its validated grant command and show how the confirmed balance reaches the display. [Choose the owner for server data](../../unity-code-skill/references/so-data-controller.md#choose-the-owner-for-server-data)

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

In this client-writable example, after opening the account slot the pickup assigns `wallet.Coins += 3` and the display reads `wallet.Coins`. The serialized field preserves the existing asset value and storage key `coins`. The SO raises `DataChanged` on acknowledgement. [Wallet source](../../unity-code-skill/assets/script/SODataController/Examples/SOCurrencyData.cs)

## Detailed explanation and steps

```text
Client-writable wallet, cloud balance 37 → wallet.Coins += 3 → memory shows 40 → remote write pending
  ├─ acknowledged → DataChanged("coins") → reopen same slot → 40
  └─ reply lost → reopen and read the service before deciding whether to retry
```
