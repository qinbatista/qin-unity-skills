using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed partial class UnityFoundationTests
{
    [Test]
    public async Task LargeSnapshotBurstReadsBackTheLastValueAndLeavesCleanDataUntouched()
    {
        string path = Path.Combine(_root, "wallet.json");
        string padding = new string('x', 64 * 1024);
        SOFileStorage.WriteSnapshot(path, "{\"$sodata\":1,\"coin\":0,\"money\":17,\"padding\":\"" + padding + "\"}");
        var wallet = ScriptableObject.CreateInstance<SOPlayerWallet>();
        try
        {
            Assert.AreEqual(0, wallet.Coin);
            int instances = Resources.FindObjectsOfTypeAll<SOPlayerWallet>().Length;
            int commits = 0;
            int fieldsConfirmed = 0;
            long bytes = 0;
            string backup = null;
            wallet.DataChanged += key => { if (key == "coin") fieldsConfirmed++; };
            wallet.SnapshotCommitted += json => { commits++; bytes += new FileInfo(path).Length; backup = json; };
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            for (int value = 1; value <= 128; value++) wallet.Coin = value;
            await wallet.FlushAsync();
            elapsed.Stop();
            int burstCommits = commits;
            Assert.AreEqual(1, burstCommits);
            Assert.AreEqual(1, fieldsConfirmed);
            byte[] saved = File.ReadAllBytes(path);
            int received = 0;
            for (int i = 0; i < 10000; i++)
            {
                wallet.Coin = 128;
                received += wallet.Coin;
            }
            await wallet.FlushAsync();
            Assert.AreEqual(1280000, received);
            Assert.AreEqual(instances, Resources.FindObjectsOfTypeAll<SOPlayerWallet>().Length);
            Assert.AreEqual(burstCommits, commits);
            CollectionAssert.AreEqual(saved, File.ReadAllBytes(path));
            await wallet.CloseAsync();
            Assert.AreEqual(128, wallet.Coin);
            Assert.AreEqual(17, wallet.Money);
            Assert.AreEqual(SOFileStorage.ReadSnapshot(path), backup);
            StringAssert.Contains(padding, backup);
            TestContext.WriteLine($"SO snapshot: changes=128 commits={burstCommits} committedBytes={bytes} elapsedMs={elapsed.Elapsed.TotalMilliseconds:F1} cleanAssignments=10000 cleanWrites={commits - burstCommits}");
            File.WriteAllText(path, "{}");
            Assert.AreEqual(128, wallet.Coin);
            wallet.CloseData();
            Assert.Throws<InvalidDataException>(() => { var cold = wallet.Coin; });
            Assert.AreEqual("{}", File.ReadAllText(path));
        }
        finally { Object.DestroyImmediate(wallet); }
    }

    [Test]
    public async Task RevertedChangesSkipDiskAndEditsDuringAWriteSurviveTheNextCheckpoint()
    {
        var wallet = ScriptableObject.CreateInstance<SOPlayerWallet>();
        try
        {
            wallet.Coin = 5;
            wallet.Coin = 0;
            await wallet.FlushAsync();
            Assert.IsFalse(Directory.Exists(_root));
            int confirmations = 0;
            int snapshots = 0;
            wallet.SnapshotCommitted += json => snapshots++;
            wallet.DataChanged += key => { if (key == "coin") confirmations++; };
            wallet.Coin = 5;
            Task first = wallet.FlushAsync();
            wallet.Coin = 0;
            wallet.Money = 19;
            await first;
            await wallet.CloseAsync();
            Assert.AreEqual(0, wallet.Coin);
            Assert.AreEqual(19, wallet.Money);
            Assert.AreEqual(2, confirmations);
            Assert.AreEqual(2, snapshots);

            wallet.Coin = 6;
            Task submitted = wallet.FlushAsync();
            wallet.Coin = 7;
            wallet.CloseData();
            await submitted;
            Assert.AreEqual(7, wallet.Coin);
        }
        finally { Object.DestroyImmediate(wallet); }
    }

    [Test]
    public async Task FailedCheckpointCanRetryThePendingValueAfterTheFileIsAvailable()
    {
        var wallet = ScriptableObject.CreateInstance<SOPlayerWallet>();
        try
        {
            wallet.Coin = 5;
            string path = Path.Combine(_root, "wallet.json");
            Directory.CreateDirectory(path);
            int confirmed = 0;
            wallet.DataChanged += _ => confirmed++;
            try { await wallet.FlushAsync(); Assert.Fail("The save path is a directory."); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { Assert.AreSame(error, wallet.StorageError); }
            Assert.AreEqual(5, wallet.Coin);
            Assert.AreEqual(0, confirmed);
            Directory.Delete(path);
            await wallet.CloseAsync();
            Assert.AreEqual(1, confirmed);
            Assert.IsNull(wallet.StorageError);
            Assert.AreEqual(5, wallet.Coin);
        }
        finally { Object.DestroyImmediate(wallet); }
    }
}
