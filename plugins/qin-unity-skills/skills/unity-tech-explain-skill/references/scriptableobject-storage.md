# ScriptableObject storage example

*How do I give a wallet three coins and get the balance back after restarting?*

## Brief explanation

The wallet's `int money` field holds the player's coin count. A pickup changes that field directly.

## Concrete code

The starting balance belongs to the `NativeWalletDomain` SO. [Wallet source](https://github.com/qinbatista/qin-unity-skills/blob/025c192947faef58c8b8bdee5ac50faa5e336a6f/.agents/skills/testing-skill/tests/fixtures/scriptableobjects/NativeWalletDomain.cs#L6)

```csharp
// NativeWalletDomain.cs
public sealed class NativeWalletDomain : ScriptableObject
{
    public int money = 37;
    // ...
}
```

Set the Storage Controller asset to `DirectAccess` in the Inspector. In this excerpt from the replay, `session` opens the wallet's slot on each launch. The first launch simulates one pickup. [NativeWalletProbe.Run](https://github.com/qinbatista/qin-unity-skills/blob/025c192947faef58c8b8bdee5ac50faa5e336a6f/.agents/skills/testing-skill/tests/fixtures/scriptableobjects/NativeWalletProbe.cs#L11)

```csharp
// NativeWalletProbe.cs
public static class NativeWalletProbe
{
    private static async void Run()
    {
        // ...
        await session.AttachAsync(wallet, "native-wallet-replay");
        if (!reopen)
        {
            wallet.money += 3;
        }
        // ...
    }
}
```

## Detailed explanation and steps

```text
37 coins → pick up 3 → the money record is written as 40
Restart → open the same slot with the same controller settings → wallet.money is 40
```

The controller receives a notification when gameplay assigns the field. With native DirectAccess, that line returns after the changed field is on disk.
