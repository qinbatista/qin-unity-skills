# ScriptableObject storage example

*How do I give a wallet three coins and get the balance back after restarting?*

## Brief explanation

The wallet's `int money` field holds the player's coin count. A pickup changes that field directly.

## Concrete code

The starting value is one line in the existing SO. [NativeWalletDomain.cs](https://github.com/qinbatista/qin-unity-skills/blob/025c192947faef58c8b8bdee5ac50faa5e336a6f/.agents/skills/testing-skill/tests/fixtures/scriptableobjects/NativeWalletDomain.cs#L15)

```csharp
public int money = 37;
```

Set the Storage Controller asset to `DirectAccess` in the Inspector. At startup, `session` is its runtime copy and `wallet` is the instance used in play. The two relevant lines from the player's replay are below. [NativeWalletProbe.Run](https://github.com/qinbatista/qin-unity-skills/blob/025c192947faef58c8b8bdee5ac50faa5e336a6f/.agents/skills/testing-skill/tests/fixtures/scriptableobjects/NativeWalletProbe.cs#L25)

```csharp
await session.AttachAsync(wallet, "native-wallet-replay"); // Once when opening the save slot.
wallet.money += 3;                                        // When the player picks up three coins.
```

## Detailed explanation and steps

```text
37 coins → pick up 3 → the money record is written as 40
Restart → open the same slot with the same controller settings → wallet.money is 40
```

The controller receives a notification when gameplay assigns the field. With native DirectAccess, that line returns after the changed field is on disk.
