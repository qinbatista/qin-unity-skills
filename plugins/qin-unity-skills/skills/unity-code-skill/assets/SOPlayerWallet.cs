using System;
using System.IO;
using UnityEngine;

public sealed class SOPlayerWallet : SOLocalData
{
    // Frozen local layouts. Each writer signs its own compact JSON with an empty checksum.
    [Serializable]
    private class WalletV1
    {
        public int version;
        public int coin = -1;
        public string checksum;
    }

    [Serializable]
    private sealed class WalletV2 : WalletV1
    {
        public int money = -1;
    }

    // A full service snapshot. A missing balance is distinguishable from a real zero.
    [Serializable]
    public sealed class OnlineWalletSnapshot
    {
        public int version;
        public int coin = -1;
        public int money = -1;
    }

    [SerializeField] private int coin;
    [SerializeField] private int money;

    public int Coin => coin;
    public int Money => money;

    public override void SetPathName() => FileName = "wallet.json";

    public override void InitialValue()
    {
        coin = 0;
        money = 0;
    }

    public void CreditCoins(int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        coin = checked(coin + amount);
        RequestAutoSave();
    }

    public OnlineWalletSnapshot CaptureOnline() =>
        new OnlineWalletSnapshot { version = 2, coin = coin, money = money };

    public void ApplyOnline(string json)
    {
        OnlineWalletSnapshot snapshot = new OnlineWalletSnapshot();
        JsonUtility.FromJsonOverwrite(json, snapshot);
        if (snapshot.version != 2 || snapshot.coin < 0 || snapshot.money < 0)
            throw new InvalidDataException("Expected a complete version 2 wallet snapshot with nonnegative balances.");
        if (coin == snapshot.coin && money == snapshot.money) return;
        coin = snapshot.coin;
        money = snapshot.money;
        RequestAutoSave();
    }

    protected override string SerializeLocalData() =>
        JsonUtility.ToJson(new WalletV2 { version = 2, coin = coin, money = money, checksum = LocalChecksum });

    protected override bool RestoreLocalData(string savedData)
    {
        WalletV1 record = new WalletV1();
        JsonUtility.FromJsonOverwrite(savedData, record);
        int restoredMoney = 0;
        switch (record.version)
        {
            case 1:
                break;
            case 2:
                WalletV2 current = new WalletV2();
                JsonUtility.FromJsonOverwrite(savedData, current);
                record = current;
                restoredMoney = current.money;
                break;
            default:
                throw new InvalidDataException($"Unsupported wallet version {record.version}.");
        }
        // An unrecognized field must survive for a compatible reader. Leave the original file intact.
        if (record.coin < 0 || restoredMoney < 0 || JsonUtility.ToJson(record) != savedData.Trim())
            throw new InvalidDataException("The wallet does not match its declared local layout.");
        string checksum = record.checksum;
        record.checksum = string.Empty;
        if (GetSHA256(JsonUtility.ToJson(record)) != checksum) return false;
        coin = record.coin;
        money = restoredMoney;
        return true;
    }
}
