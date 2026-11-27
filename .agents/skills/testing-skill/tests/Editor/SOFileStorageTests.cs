using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

public sealed partial class UnityFoundationTests
{
    [Test]
    public void LocalFileEncryptsTheWholePayloadAndRejectsStaleOrDamagedData()
    {
        string path = Path.Combine(_root, "outfit.save");
        const string outfit = "{\"hat\":\"miner\",\"boots\":\"blue\"}";
        Assert.IsNull(SOFileStorage.ReadSnapshot(path, out string missing));
        string first = SOFileStorage.WriteSnapshot(path, outfit, missing);
        string firstText = File.ReadAllText(path);
        StringAssert.StartsWith("SODATA/1\n", firstText);
        StringAssert.DoesNotContain("miner", firstText);
        Assert.AreEqual(outfit, SOFileStorage.ReadSnapshot(path, out string loaded));
        Assert.AreEqual(first, loaded);

        string second = SOFileStorage.WriteSnapshot(path, outfit, loaded);
        Assert.AreNotEqual(first, second, "Fresh IV even for identical payloads.");
        byte[] good = File.ReadAllBytes(path);
        Assert.Throws<SODataConflictException>(() => SOFileStorage.WriteSnapshot(path, "stale", first));
        CollectionAssert.AreEqual(good, File.ReadAllBytes(path));

        string prefix = "SODATA/1\n";
        byte[] encrypted = Convert.FromBase64String(File.ReadAllText(path).Substring(prefix.Length));
        encrypted[20] ^= 1;
        string damaged = prefix + Convert.ToBase64String(encrypted);
        File.WriteAllText(path, damaged);
        Assert.Throws<InvalidDataException>(() => SOFileStorage.ReadSnapshot(path));
        Assert.AreEqual(damaged, File.ReadAllText(path));
        File.WriteAllText(path, prefix + "AQ==");
        Assert.Throws<InvalidDataException>(() => SOFileStorage.ReadSnapshot(path));
        File.WriteAllText(path, "SODATA/9\nAQ==");
        Assert.Throws<InvalidDataException>(() => SOFileStorage.ReadSnapshot(path));

        File.WriteAllBytes(path, good);
        string swapped = Path.Combine(_root, "other.save");
        File.Copy(path, swapped);
        Assert.Throws<InvalidDataException>(() => SOFileStorage.ReadSnapshot(swapped));
        string keyPath = Path.Combine(_root, ".sodata", "device.key");
        byte[] originalKey = File.ReadAllBytes(keyPath);
        File.Delete(keyPath);
        Assert.Throws<InvalidDataException>(() => SOFileStorage.ReadSnapshot(path));
        Assert.Throws<InvalidDataException>(() => SOFileStorage.WriteSnapshot(path, outfit, second));
        Assert.Throws<InvalidDataException>(() => SOFileStorage.WriteSnapshot(Path.Combine(_root, "new.save"), outfit, null));
        CollectionAssert.AreEqual(good, File.ReadAllBytes(path));
        File.WriteAllBytes(keyPath, originalKey);
        Assert.AreEqual(outfit, SOFileStorage.ReadSnapshot(path));
    }

    [Test]
    public void BothHistoricalFileEncodingsUpgradeOnlyOnTheNextSave()
    {
        Directory.CreateDirectory(_root);
        const string json = "{\"coin\":37}";
        string path = Path.Combine(_root, "legacy");
        File.WriteAllText(path, json, Encoding.UTF8);
        byte[] previous = File.ReadAllBytes(path);
        Assert.AreEqual(json, SOFileStorage.ReadSnapshot(path, out string plainRevision));
        CollectionAssert.AreEqual(previous, File.ReadAllBytes(path));
        using (BinaryWriter writer = new BinaryWriter(File.Create(path), Encoding.UTF8))
        {
            writer.Write((byte)0);
            writer.Write(1);
            writer.Write(-1);
            writer.Write(1);
            writer.Write(0);
            writer.Write((byte)6);
            writer.Write(1);
            writer.Write(Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
            writer.Write((byte)11);
        }
        byte[] historical = File.ReadAllBytes(path);
        Assert.AreEqual(json, SOFileStorage.ReadSnapshot(path, out string framedRevision));
        CollectionAssert.AreEqual(historical, File.ReadAllBytes(path));
        Assert.Throws<SODataConflictException>(() => SOFileStorage.WriteSnapshot(path, json, plainRevision));
        SOFileStorage.WriteSnapshot(path, json, framedRevision);
        StringAssert.StartsWith("SODATA/1\n", File.ReadAllText(path));
        Assert.AreEqual(json, SOFileStorage.ReadSnapshot(path));
    }

    [Test]
    public async Task DirectAccessChangesOnlyOneKeyAndReopensTheAcknowledgedValue()
    {
        SOFileKeyStore store = new SOFileKeyStore(_root);
        Assert.IsNull(await store.ReadAsync("wallet", "coins", CancellationToken.None));
        SODataRecord old = await store.WriteAsync("wallet", "coins", "37", null, CancellationToken.None);
        await store.WriteAsync("wallet", "level", "9", null, CancellationToken.None);
        await store.WriteAsync("equipment", "coins", "pinned", null, CancellationToken.None);
        Dictionary<string, byte[]> before = Directory.GetFiles(_root, "*.record").ToDictionary(path => path, File.ReadAllBytes);
        Assert.AreEqual(3, before.Count);
        SODataRecord committed = await store.WriteAsync("wallet", "coins", "40", old.Revision, CancellationToken.None);
        Assert.AreEqual(1, before.Count(pair => !pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key))));
        SOFileKeyStore reopened = new SOFileKeyStore(_root);
        SODataRecord coins = await reopened.ReadAsync("wallet", "coins", CancellationToken.None);
        Assert.AreEqual("40", coins.Value);
        Assert.AreEqual(committed.Revision, coins.Revision);
        Assert.AreEqual("9", (await reopened.ReadAsync("wallet", "level", CancellationToken.None)).Value);
        Assert.AreEqual("pinned", (await reopened.ReadAsync("equipment", "coins", CancellationToken.None)).Value);
        Assert.Throws<SODataConflictException>(() => store.WriteAsync("wallet", "coins", "3", null, CancellationToken.None).GetAwaiter().GetResult());
        Assert.Throws<SODataConflictException>(() => store.WriteAsync("wallet", "coins", "3", old.Revision, CancellationToken.None).GetAwaiter().GetResult());
        using CancellationTokenSource cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Catch<OperationCanceledException>(() => store.WriteAsync("wallet", "coins", "3", committed.Revision, cancelled.Token).GetAwaiter().GetResult());
        Assert.AreEqual("40", (await reopened.ReadAsync("wallet", "coins", CancellationToken.None)).Value);
    }

    [Test]
    public async Task TwoWritersWithOneRevisionCanOnlyAcknowledgeOneChange()
    {
        SOFileKeyStore first = new SOFileKeyStore(_root);
        SOFileKeyStore second = new SOFileKeyStore(_root);
        SODataRecord initial = await first.WriteAsync("wallet", "coins", "37", null, CancellationToken.None);
        Task<SODataRecord> left = first.WriteAsync("wallet", "coins", "40", initial.Revision, CancellationToken.None);
        Task<SODataRecord> right = second.WriteAsync("wallet", "coins", "44", initial.Revision, CancellationToken.None);
        try { await Task.WhenAll(left, right); }
        catch (SODataConflictException) { }
        Assert.AreNotEqual(left.IsFaulted, right.IsFaulted);
        Task<SODataRecord> loser = left.IsFaulted ? left : right;
        Assert.IsInstanceOf<SODataConflictException>(loser.Exception.GetBaseException());
        SODataRecord winner = left.IsFaulted ? right.Result : left.Result;
        SODataRecord reloaded = await second.ReadAsync("wallet", "coins", CancellationToken.None);
        Assert.AreEqual(winner.Revision, reloaded.Revision);
        Assert.AreEqual(winner.Value, reloaded.Value);
    }

    [Test]
    public async Task InterruptedStagingAndFailedCommitKeepTheLastKey()
    {
        SOFileKeyStore store = new SOFileKeyStore(_root);
        SODataRecord initial = await store.WriteAsync("wallet", "coins", "37", null, CancellationToken.None);
        string pending = Path.Combine(_root, ".sodata", "pending");
        File.WriteAllText(pending, "half a write");
        Assert.AreEqual("37", (await store.ReadAsync("wallet", "coins", CancellationToken.None)).Value);
        SODataRecord committed = await store.WriteAsync("wallet", "coins", "40", initial.Revision, CancellationToken.None);
        Assert.IsFalse(File.Exists(pending));
        // An unwritable staging name must fail before replacing the acknowledged record.
        Directory.CreateDirectory(pending);
        Exception failure = Assert.Catch(() => store.WriteAsync("wallet", "coins", "44", committed.Revision, CancellationToken.None).GetAwaiter().GetResult());
        Assert.IsTrue(failure is IOException || failure is UnauthorizedAccessException, failure.ToString());
        Assert.AreEqual("40", (await store.ReadAsync("wallet", "coins", CancellationToken.None)).Value);
        Directory.Delete(pending);
        Assert.AreEqual("44", (await store.WriteAsync("wallet", "coins", "44", committed.Revision, CancellationToken.None)).Value);
    }
}
