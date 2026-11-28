using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

// Run the built player twice on the same clean installation. The second launch performs only a cold read.
public static class NativeWalletProbe
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static async void Run()
    {
        string first = Path.Combine(Application.persistentDataPath, "wallet-probe-first.txt");
        bool reopen = File.Exists(first);
        string receipt = reopen ? Path.Combine(Application.persistentDataPath, "wallet-probe-reopen.txt") : first;
        int status = 1;
        NativeWalletDomain wallet = null;
        try
        {
            wallet = ScriptableObject.CreateInstance<NativeWalletDomain>();
            await wallet.OpenAsync("native-wallet-replay");
            if (!reopen)
            {
                if (wallet.money != 37) throw new InvalidDataException("Start the replay on a clean installation.");
                wallet.money += 3;
                wallet.Hat = "miner";
                wallet.items = new List<NativeWalletDomain.Item> { new NativeWalletDomain.Item { id = "ore", count = 8 } };
                wallet.drops = new[] { new NativeWalletDomain.Item { id = "ruby", count = 2 } };
                wallet.region = new Bounds(Vector3.one, new Vector3(4, 6, 8));
                await wallet.FlushAsync();
            }
            if (wallet.money != 40 || wallet.Hat != "miner" || wallet.items.Count != 1 || wallet.items[0].id != "ore" || wallet.items[0].count != 8 ||
                wallet.drops == null || wallet.drops.Length != 1 || wallet.drops[0].id != "ruby" || wallet.drops[0].count != 2 || wallet.region.size.z != 8f)
                throw new InvalidDataException("Incorrect balance, outfit, item list, drop array or bounds.");
            wallet.CloseData();
            File.WriteAllText(receipt, reopen ? "PASS cold read of five keys before gameplay writes" : "PASS committed five keys through ordinary assignments");
            status = 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(receipt, "FAIL " + error);
        }
        finally
        {
            if (wallet != null) Object.Destroy(wallet);
            Application.Quit(status);
        }
    }
}
