using System;
using System.IO;
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
    public async Task ConcurrentFileCommitsWithOneRevisionHaveOnlyOneWinner()
    {
        string path = Path.Combine(_root, "coins.record");
        string previous = SOFileStorage.Commit(path, "37", null, true, false, CancellationToken.None);
        Task<string> left = Task.Run(() => SOFileStorage.Commit(path, "40", previous, true, false, CancellationToken.None));
        Task<string> right = Task.Run(() => SOFileStorage.Commit(path, "44", previous, true, false, CancellationToken.None));
        try { await Task.WhenAll(left, right); }
        catch (SODataConflictException) { }
        Assert.AreNotEqual(left.IsFaulted, right.IsFaulted);
        Assert.IsInstanceOf<SODataConflictException>((left.IsFaulted ? left : right).Exception.GetBaseException());
        string latest = SOFileStorage.Read(path, false, out string revision);
        Assert.AreEqual(left.IsFaulted ? "44" : "40", latest);
        Assert.AreEqual((left.IsFaulted ? right : left).Result, revision);
    }

    [Test]
    public void AnInterruptedNativeWriteKeepsTheLastAcknowledgedValue()
    {
        string path = Path.Combine(_root, "coins.record");
        string revision = SOFileStorage.Commit(path, "37", null, true, false, CancellationToken.None);
        string pending = Path.Combine(_root, ".sodata", "pending");
        File.WriteAllText(pending, "partial");
        Assert.AreEqual("37", SOFileStorage.Read(path, false, out _));
        File.Delete(pending);
        Directory.CreateDirectory(pending);
        Assert.Catch<Exception>(() => SOFileStorage.Commit(path, "40", revision, true, false, CancellationToken.None));
        Assert.AreEqual("37", SOFileStorage.Read(path, false, out string retained));
        Assert.AreEqual(revision, retained);
        Directory.Delete(pending);
        SOFileStorage.Commit(path, "40", retained, true, false, CancellationToken.None);
        Assert.AreEqual("40", SOFileStorage.Read(path, false, out _));
    }
}
