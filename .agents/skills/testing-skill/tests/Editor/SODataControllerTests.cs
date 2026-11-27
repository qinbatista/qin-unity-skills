using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class UnityFoundationTests
{
    private static T Domain<T>(SODataMode mode, string id = "domain") where T : SODataController
    {
        var data = ScriptableObject.CreateInstance<T>();
        SerializedObject inspector = new SerializedObject(data);
        inspector.FindProperty("_storageMode").intValue = (int)mode;
        inspector.FindProperty("_storageId").stringValue = id;
        inspector.ApplyModifiedPropertiesWithoutUndo();
        return data;
    }

    private static async Task<T> ExpectError<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T error) { return error; }
        throw new AssertionException("Expected " + typeof(T).Name);
    }

    [Test]
    public async Task NoneSharesTheRuntimeObjectAndRestartsFromAuthoredDefaults()
    {
        var asset = ScriptableObject.CreateInstance<SOCurrencyData>();
        var first = Object.Instantiate(asset);
        var second = Object.Instantiate(asset);
        try
        {
            Assert.AreEqual(SODataMode.None, first.Mode);
            first.CreditCoins(37);
            DelayedStore unused = new DelayedStore();
            await first.OpenAsync(null, unused);
            first.CreditCoins(3);
            first.SaveDataAtLocal();
            Assert.AreEqual(40, first.Coins);
            Assert.AreEqual(0, asset.Coins);
            Assert.AreEqual(0, second.Coins);
            Assert.AreEqual(0, unused.Reads);
            Assert.IsEmpty(unused.Writes);
            Assert.IsFalse(Directory.Exists(_root));
        }
        finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); Object.DestroyImmediate(asset); }
    }

    [Test]
    public async Task LocalFileLoadsBothOutfitFieldsAndPreservesProfileDefaults()
    {
        var outfit = Domain<SOOutfitData>(SODataMode.LocalFile);
        var cold = Object.Instantiate(outfit);
        try
        {
            await outfit.OpenAsync("slot-a");
            outfit.WearBoots("boots-blue");
            outfit.WearHat("hat-red");
            await cold.OpenAsync("slot-a");
            Assert.AreEqual("boots-blue", cold.BootsId);
            Assert.AreEqual("hat-red", cold.HatId);
            outfit.WearHat("hat-green");
            Assert.Throws<SODataConflictException>(() => cold.WearBoots("stale"));
            Assert.AreEqual("boots-blue", cold.BootsId);
            cold.CloseData();
            await cold.OpenAsync("slot-b");
            Assert.AreEqual("", cold.HatId);
            Assert.AreEqual("", cold.BootsId);
        }
        finally { Object.DestroyImmediate(outfit); Object.DestroyImmediate(cold); }
    }

    [Test]
    public async Task DirectAccessOrdinaryCreditWritesTheOriginalWalletKeyBeforeReturning()
    {
        var first = Domain<SOCurrencyData>(SODataMode.DirectAccess, "wallet");
        var cold = Object.Instantiate(first);
        try
        {
            // Seed the field name, wire shape and profile scope used by the previous release.
            SOFileKeyStore original = new SOFileKeyStore(Path.Combine(_root, "SOData", "keys"));
            const string oldScope = "profile%2F1/wallet";
            await original.WriteAsync(oldScope, "coins", "{\"value\":37}", null, CancellationToken.None);
            await first.OpenAsync("profile/1");
            Assert.AreEqual(37, first.Coins);
            first.CreditCoins(1);
            first.CreditCoins(2);
            Assert.AreEqual("{\"value\":40}", (await original.ReadAsync(oldScope, "coins", CancellationToken.None)).Value);
            await cold.OpenAsync("profile/1");
            Assert.AreEqual(40, cold.Coins);
            first.CreditCoins(2);
            Assert.Throws<SODataConflictException>(() => cold.CreditCoins(10));
            Assert.AreEqual(40, cold.Coins);
            cold.CloseData();
            await cold.OpenAsync("profile/1");
            Assert.AreEqual(42, cold.Coins);
            // An existing call site can still opt into the asynchronous compatibility extension.
            await cold.CreditCoinsAsync(1);
            await ExpectError<OperationCanceledException>(() => cold.CreditCoinsAsync(5, new CancellationToken(true)));
            Assert.AreEqual(43, cold.Coins);
            Assert.AreEqual("{\"value\":43}", (await original.ReadAsync(oldScope, "coins", CancellationToken.None)).Value);
            cold.CloseData();
            await cold.OpenAsync("profile%2F1");
            Assert.AreEqual(0, cold.Coins);
        }
        finally { Object.DestroyImmediate(first); Object.DestroyImmediate(cold); }
    }

    [TestCase(SODataMode.LocalFile)]
    [TestCase(SODataMode.DirectAccess)]
    public async Task AnExternalAssemblyCanAssignAnUnmodifiedPlainSOAndRestartImmediately(SODataMode mode)
    {
        Assert.AreNotSame(typeof(PlainMoney).Assembly, typeof(UnityFoundationTests).Assembly);
        Assert.AreEqual(typeof(ScriptableObject), typeof(PlainMoney).BaseType);
        var authored = ScriptableObject.CreateInstance<PlainMoney>();
        var live = Object.Instantiate(authored);
        var cold = Object.Instantiate(authored);
        var first = Domain<SODataController>(mode, "plain-wallet");
        var second = Domain<SODataController>(mode, "plain-wallet");
        try
        {
            await first.AttachAsync(live, "slot");
            // These field assignments compile into the caller assembly; PlainMoney contains no storage code.
            live.money = 37;
            live.gems = 8;
            SOFileKeyStore keys = new SOFileKeyStore(Path.Combine(_root, "SOData", "keys"));
            SODataRecord gems = null;
            if (mode == SODataMode.DirectAccess) gems = await keys.ReadAsync("slot/plain-wallet", "gems", CancellationToken.None);
            live.money += 3;
            await second.AttachAsync(cold, "slot");
            Assert.AreEqual(40, cold.money);
            Assert.AreEqual(8, cold.gems);
            Assert.AreEqual(0, authored.money);
            if (mode == SODataMode.DirectAccess)
            {
                Assert.IsNotNull(gems);
                Assert.AreEqual(gems.Revision, (await keys.ReadAsync("slot/plain-wallet", "gems", CancellationToken.None)).Revision);
            }

            second.CloseData();
            live.claimedRewards.Add(17);
            await first.FlushAsync();
            await second.AttachAsync(cold, "slot");
            CollectionAssert.AreEqual(new[] { 17 }, cold.claimedRewards);
            first.CloseData();
            live.money = 999; // An explicitly detached object is ordinary shared memory again.
            second.CloseData();
            await second.AttachAsync(cold, "slot");
            Assert.AreEqual(40, cold.money);
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(live);
            Object.DestroyImmediate(cold);
            Object.DestroyImmediate(authored);
        }
    }

    private sealed class UnsupportedSnapshot : SODataController
    {
        [SerializeField] private System.Collections.Generic.Dictionary<int, int> amounts;
        public int Count => amounts?.Count ?? 0;
    }

    [Test]
    public async Task SnapshotRejectsFieldsUnityWouldSilentlyDrop()
    {
        var data = Domain<UnsupportedSnapshot>(SODataMode.LocalFile);
        try
        {
            var error = await ExpectError<InvalidOperationException>(() => data.OpenAsync("slot"));
            StringAssert.Contains("amounts", error.Message);
            Assert.IsFalse(Directory.Exists(_root));
        }
        finally { Object.DestroyImmediate(data); }
    }

    private sealed class DelayedStore : ISODataKeyStore
    {
        public sealed class Pending
        {
            public string Value;
            public string ExpectedRevision;
            public readonly TaskCompletionSource<SODataRecord> Reply = new TaskCompletionSource<SODataRecord>();
        }

        public int Reads;
        public readonly List<Pending> Writes = new List<Pending>();

        public Task<SODataRecord> ReadAsync(string scope, string key, CancellationToken cancellation)
        {
            Reads++;
            return Task.FromResult<SODataRecord>(null);
        }

        public Task<SODataRecord> WriteAsync(string scope, string key, string value, string expectedRevision, CancellationToken cancellation)
        {
            Pending pending = new Pending { Value = value, ExpectedRevision = expectedRevision };
            Writes.Add(pending);
            return pending.Reply.Task;
        }

        public void Accept(int index, string revision) => Writes[index].Reply.SetResult(new SODataRecord(Writes[index].Value, revision));
    }

    [UnityTest]
    public IEnumerator OnlineWritesKeepTheLatestOptimisticEditAndIgnoreAnOldAccountReply()
    {
        var data = Domain<SOCurrencyData>(SODataMode.DirectAccessOnline);
        try
        {
            Task<ArgumentNullException> unbound = ExpectError<ArgumentNullException>(() => data.OpenAsync("A"));
            yield return AwaitStorage(unbound);
            DelayedStore first = new DelayedStore();
            yield return AwaitStorage(data.OpenAsync("A", first));
            data.CreditCoins(3);
            Task oldAck = data.FlushAsync();
            Assert.IsFalse(oldAck.IsCompleted);
            Assert.AreEqual(3, data.Coins);
            data.CloseData();
            yield return AwaitStorage(ExpectError<OperationCanceledException>(() => oldAck));
            DelayedStore second = new DelayedStore();
            yield return AwaitStorage(data.OpenAsync("B", second));
            first.Accept(0, "a1");
            Assert.AreEqual(0, data.Coins);
            data.CreditCoins(5);
            data.CreditCoins(2);
            Assert.AreEqual(7, data.Coins);
            Task ack = data.FlushAsync();
            Assert.AreEqual(1, second.Writes.Count);
            Assert.AreEqual("{\"value\":5}", second.Writes[0].Value);
            second.Accept(0, "b1");
            yield return AwaitCondition(() => second.Writes.Count == 2);
            Assert.AreEqual("b1", second.Writes[1].ExpectedRevision);
            Assert.AreEqual("{\"value\":7}", second.Writes[1].Value);
            Assert.AreEqual(7, data.Coins);
            second.Accept(1, "b2");
            yield return AwaitStorage(ack);
            data.CreditCoins(10);
            Task failed = data.FlushAsync();
            second.Writes[2].Reply.SetException(new IOException("Test provider disconnected."));
            yield return AwaitCondition(() => failed.IsCompleted);
            Assert.IsTrue(failed.IsFaulted);
            Assert.IsInstanceOf<IOException>(failed.Exception.GetBaseException());
            Assert.AreEqual(7, data.Coins);
            Assert.IsFalse(Directory.Exists(_root));
        }
        finally { Object.DestroyImmediate(data); }
    }

    private static IEnumerator AwaitCondition(Func<bool> completed)
    {
        double deadline = EditorApplication.timeSinceStartup + 12;
        while (!completed() && EditorApplication.timeSinceStartup < deadline) yield return null;
        Assert.IsTrue(completed(), "Timed out waiting for the storage session.");
    }

    private static IEnumerator AwaitStorage(Task operation)
    {
        yield return AwaitCondition(() => operation.IsCompleted);
        operation.GetAwaiter().GetResult();
    }
}
