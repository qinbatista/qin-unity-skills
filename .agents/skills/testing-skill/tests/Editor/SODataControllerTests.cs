using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
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
            await first.OpenAsync(null);
            await first.CreditCoinsAsync(3);
            first.SaveDataAtLocal();
            Assert.AreEqual(40, first.Coins);
            Assert.AreEqual(0, asset.Coins);
            Assert.AreEqual(0, second.Coins);
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
            await outfit.WearBootsAsync("boots-blue");
            await outfit.WearHatAsync("hat-red");
            await cold.OpenAsync("slot-a");
            Assert.AreEqual("boots-blue", cold.BootsId);
            Assert.AreEqual("hat-red", cold.HatId);
            await outfit.WearHatAsync("hat-green");
            await ExpectError<SODataConflictException>(async () => await cold.WearBootsAsync("stale"));
            Assert.AreEqual("boots-blue", cold.BootsId);
            cold.CloseData();
            await cold.OpenAsync("slot-b");
            Assert.AreEqual("", cold.HatId);
            Assert.AreEqual("", cold.BootsId);
        }
        finally { Object.DestroyImmediate(outfit); Object.DestroyImmediate(cold); }
    }

    [Test]
    public async Task DirectAccessCreditsImmediatelyAndSerializesOverlappingAwards()
    {
        var first = Domain<SOCurrencyData>(SODataMode.DirectAccess, "wallet");
        var cold = Object.Instantiate(first);
        try
        {
            await first.OpenAsync("profile/1");
            await first.CreditCoinsAsync(37);
            await Task.WhenAll(first.CreditCoinsAsync(1), first.CreditCoinsAsync(2));
            await cold.OpenAsync("profile/1");
            Assert.AreEqual(40, cold.Coins);
            await first.CreditCoinsAsync(2);
            await ExpectError<SODataConflictException>(async () => await cold.CreditCoinsAsync(10));
            Assert.AreEqual(40, cold.Coins);
            cold.CloseData();
            await cold.OpenAsync("profile/1");
            Assert.AreEqual(42, cold.Coins);
            cold.CloseData();
            await cold.OpenAsync("profile%2F1");
            Assert.AreEqual(0, cold.Coins);
        }
        finally { Object.DestroyImmediate(first); Object.DestroyImmediate(cold); }
    }

    private sealed class MissingField : SODataController
    {
        [SerializeField] private int _unmapped = 1;
        public int Unmapped => _unmapped;
    }

    [Test]
    public async Task KeyedModeRefusesAnUnmappedDomainField()
    {
        var data = Domain<MissingField>(SODataMode.DirectAccess);
        try
        {
            var error = await ExpectError<InvalidOperationException>(() => data.OpenAsync("player"));
            StringAssert.Contains("_unmapped", error.Message);
            Assert.IsFalse(Directory.Exists(_root));
        }
        finally { Object.DestroyImmediate(data); }
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
        internal readonly TaskCompletionSource<SODataRecord> Reply = new TaskCompletionSource<SODataRecord>();
        public Task<SODataRecord> ReadAsync(string scope, string key, CancellationToken cancellation) =>
            Task.FromResult<SODataRecord>(null);
        public Task<SODataRecord> WriteAsync(string scope, string key, string value, string expectedRevision, CancellationToken cancellation) =>
            Reply.Task;
    }

    [Test]
    public async Task OnlineBalanceWaitsForAcknowledgementAndIgnoresAnOldAccountReply()
    {
        var data = Domain<SOCurrencyData>(SODataMode.DirectAccessOnline);
        try
        {
            await ExpectError<ArgumentNullException>(async () => await data.OpenAsync("A"));
            DelayedStore first = new DelayedStore();
            await data.OpenAsync("A", first);
            Task credit = data.CreditCoinsAsync(3);
            Assert.AreEqual(0, data.Coins);
            data.CloseData();
            DelayedStore second = new DelayedStore();
            await data.OpenAsync("B", second);
            first.Reply.SetResult(new SODataRecord("{\"value\":3}", "a1"));
            await ExpectError<OperationCanceledException>(async () => await credit);
            Assert.AreEqual(0, data.Coins);
            credit = data.CreditCoinsAsync(5);
            second.Reply.SetResult(new SODataRecord("{\"value\":5}", "b1"));
            await credit;
            Assert.AreEqual(5, data.Coins);
            Assert.IsFalse(Directory.Exists(_root));
        }
        finally { Object.DestroyImmediate(data); }
    }
}
