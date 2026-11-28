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
using UnityEngine.UIElements;
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

    private static T StorageProfile<T>(params SODataController[] domains) where T : SODataProfile
    {
        T profile = ScriptableObject.CreateInstance<T>();
        using SerializedObject inspector = new SerializedObject(profile);
        SerializedProperty references = inspector.FindProperty("_domains");
        references.arraySize = domains.Length;
        for (int index = 0; index < domains.Length; index++) references.GetArrayElementAtIndex(index).objectReferenceValue = domains[index];
        inspector.ApplyModifiedPropertiesWithoutUndo();
        return profile;
    }

    [Test]
    public async Task ProfileRollsBackAnIncompleteOpenAndPreservesTheExistingOwner()
    {
        SOOutfitData first = Domain<SOOutfitData>(SODataMode.LocalFile, "first");
        SOOutfitData occupied = Domain<SOOutfitData>(SODataMode.DirectAccess, "occupied");
        SOOutfitData attached = ScriptableObject.CreateInstance<SOOutfitData>();
        SODataProfile profile = StorageProfile<SODataProfile>(first, occupied);
        try
        {
            await occupied.OpenAsync("other");
            await ExpectError<InvalidOperationException>(() => profile.EnterAsync("slot"));
            Assert.IsFalse(SODataWriteHooks.IsBound(first));
            Assert.IsTrue(SODataWriteHooks.IsBound(occupied));
            occupied.WearHat("other-hat");
            occupied.CloseData();

            await occupied.AttachAsync(attached, "external");
            await ExpectError<InvalidOperationException>(() => profile.EnterAsync("slot"));
            Assert.IsTrue(SODataWriteHooks.IsBound(attached));
            occupied.CloseData();

            await profile.EnterAsync("slot");
            first.WearHat("slot-hat");
            Assert.AreEqual("", occupied.HatId);
            occupied.WearBoots("slot-boots");
            await profile.LeaveAsync();
            await profile.EnterAsync("slot");
            Assert.AreEqual("slot-hat", first.HatId);
            Assert.AreEqual("slot-boots", occupied.BootsId);
            await profile.LeaveAsync();
            await occupied.OpenAsync("other");
            Assert.AreEqual("other-hat", occupied.HatId);
        }
        finally { Object.DestroyImmediate(profile); Object.DestroyImmediate(first); Object.DestroyImmediate(occupied); Object.DestroyImmediate(attached); }
    }

    [Test]
    public async Task NoneSharesTheRuntimeObjectAndRestartsFromAuthoredDefaults()
    {
        SOOutfitData asset = ScriptableObject.CreateInstance<SOOutfitData>();
        var first = Object.Instantiate(asset);
        var second = Object.Instantiate(asset);
        try
        {
            Assert.AreEqual(SODataMode.None, first.Mode);
            first.WearHat("straw");
            DelayedStore unused = new DelayedStore();
            await first.OpenAsync(null, unused);
            first.WearHat("felt");
            first.SaveDataAtLocal();
            Assert.AreEqual("felt", first.HatId);
            Assert.AreEqual("", asset.HatId);
            Assert.AreEqual("", second.HatId);
            Assert.AreEqual(0, unused.Reads);
            Assert.IsEmpty(unused.Writes);
            Assert.IsFalse(Directory.Exists(_root));
        }
        finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); Object.DestroyImmediate(asset); }
    }

    [Test]
    public void InspectorLocksTheEffectiveCurrencyModeAndKeepsOrdinaryDomainsEditable()
    {
        SOCurrencyData wallet = Domain<SOCurrencyData>(SODataMode.None);
        SOOutfitData outfit = Domain<SOOutfitData>(SODataMode.LocalFile);
        try
        {
            SOStorageModeDrawer drawer = new SOStorageModeDrawer();
            using (SerializedObject walletProperties = new SerializedObject(wallet))
            using (SerializedObject outfitProperties = new SerializedObject(outfit))
            using (SerializedObject both = new SerializedObject(new Object[] { wallet, outfit }))
            {
                EnumField fixedField = (EnumField)drawer.CreatePropertyGUI(walletProperties.FindProperty("_storageMode"));
                Assert.AreEqual("Storage Mode", fixedField.label);
                Assert.AreEqual(SODataMode.DirectAccessOnline, fixedField.value);
                Assert.IsFalse(fixedField.enabledSelf);
                EnumField editableField = (EnumField)drawer.CreatePropertyGUI(outfitProperties.FindProperty("_storageMode"));
                Assert.AreEqual(SODataMode.LocalFile, editableField.value);
                Assert.IsTrue(editableField.enabledSelf);
                EnumField mixed = (EnumField)drawer.CreatePropertyGUI(both.FindProperty("_storageMode"));
                Assert.IsTrue(mixed.showMixedValue);
                Assert.IsFalse(mixed.enabledSelf);
            }
        }
        finally { Object.DestroyImmediate(wallet); Object.DestroyImmediate(outfit); }
    }

    [TestCase(SODataMode.None)]
    [TestCase(SODataMode.LocalFile)]
    [TestCase(SODataMode.DirectAccess)]
    [TestCase(SODataMode.RealtimeOnline)]
    public async Task CurrencyRejectsConflictingPoliciesBeforeAnyBindingOrWrite(SODataMode mismatch)
    {
        SOCurrencyData wallet = Domain<SOCurrencyData>(mismatch);
        SODataController other = Domain<SODataController>(mismatch);
        SODataController online = Domain<SODataController>(SODataMode.DirectAccessOnline);
        DelayedStore cloud = new DelayedStore();
        try
        {
            Assert.AreEqual(SODataMode.DirectAccessOnline, wallet.Mode);
            Assert.Throws<InvalidOperationException>(() => wallet.CreditCoins(3));
            await ExpectError<InvalidOperationException>(() => other.AttachAsync(wallet, "slot"));
            await ExpectError<ArgumentNullException>(() => wallet.OpenAsync("slot"));
            await ExpectError<ArgumentException>(() => wallet.OpenAsync("slot", new SOFileKeyStore(_root)));
            Assert.Throws<InvalidOperationException>(() => wallet.CreditCoins(3));
            Assert.AreEqual(0, wallet.Coins);
            Assert.IsFalse(Directory.Exists(_root));
            await online.AttachAsync(wallet, "slot", cloud);
            wallet.CreditCoins(3);
            Assert.AreEqual(3, wallet.Coins);
            Task receipt = online.FlushAsync();
            Assert.IsFalse(receipt.IsCompleted);
            Assert.AreEqual("{\"value\":3}", cloud.Writes[0].Value);
            cloud.Accept(0, "revision-1");
            await receipt;
            online.CloseData();
            Assert.Throws<InvalidOperationException>(() => wallet.CreditCoins(2));
            Assert.IsFalse(Directory.Exists(_root));
        }
        finally { Object.DestroyImmediate(online); Object.DestroyImmediate(other); Object.DestroyImmediate(wallet); }
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
    public void CurrencyAssetWithThePreviousSerializedFieldImportsItsAuthoredBalance()
    {
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Fixtures/LegacyCurrency.asset");
        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<SOCurrencyData>(), path);
        try
        {
            AssetDatabase.SaveAssets();
            string yaml = File.ReadAllText(path);
            StringAssert.Contains("  _coins: 0", yaml);
            File.WriteAllText(path, yaml.Replace("  _coins: 0", "  coins: 11"));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            SOCurrencyData wallet = AssetDatabase.LoadAssetAtPath<SOCurrencyData>(path);
            Assert.AreEqual(11, wallet.Coins);
            Assert.AreEqual(SODataMode.DirectAccessOnline, wallet.Mode);
        }
        finally { AssetDatabase.DeleteAsset(path); }
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
        SOOutfitData data = Domain<SOOutfitData>(SODataMode.DirectAccessOnline);
        try
        {
            Task<ArgumentNullException> unbound = ExpectError<ArgumentNullException>(() => data.OpenAsync("A"));
            yield return AwaitStorage(unbound);
            DelayedStore first = new DelayedStore();
            yield return AwaitStorage(data.OpenAsync("A", first));
            data.WearHat("straw");
            Task oldAck = data.FlushAsync();
            Assert.IsFalse(oldAck.IsCompleted);
            Assert.AreEqual("straw", data.HatId);
            data.CloseData();
            yield return AwaitStorage(ExpectError<OperationCanceledException>(() => oldAck));
            DelayedStore second = new DelayedStore();
            yield return AwaitStorage(data.OpenAsync("B", second));
            first.Accept(0, "a1");
            Assert.AreEqual("", data.HatId);
            data.WearHat("felt");
            data.WearHat("wool");
            Assert.AreEqual("wool", data.HatId);
            Task ack = data.FlushAsync();
            Assert.AreEqual(1, second.Writes.Count);
            Assert.AreEqual("{\"value\":\"felt\"}", second.Writes[0].Value);
            second.Accept(0, "b1");
            yield return AwaitCondition(() => second.Writes.Count == 2);
            Assert.AreEqual("b1", second.Writes[1].ExpectedRevision);
            Assert.AreEqual("{\"value\":\"wool\"}", second.Writes[1].Value);
            Assert.AreEqual("wool", data.HatId);
            second.Accept(1, "b2");
            yield return AwaitStorage(ack);
            data.WearHat("denim");
            Task failed = data.FlushAsync();
            second.Writes[2].Reply.SetException(new IOException("Test provider disconnected."));
            yield return AwaitCondition(() => failed.IsCompleted);
            Assert.IsTrue(failed.IsFaulted);
            Assert.IsInstanceOf<IOException>(failed.Exception.GetBaseException());
            Assert.AreEqual("wool", data.HatId);
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
