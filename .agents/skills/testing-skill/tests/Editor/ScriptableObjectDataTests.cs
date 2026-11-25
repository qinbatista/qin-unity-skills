using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed partial class UnityFoundationTests
{
    private const string PreviousCatalog = "Assets/Fixtures/SpellCatalogV1.asset";
    private const string PreviousWallet = "Assets/Fixtures/WalletV1.json";

    [Test]
    public void EmptyCatalogHasNoSearchableKeysOrPlayerFile()
    {
        SOSpellCatalog data = ScriptableObject.CreateInstance<SOSpellCatalog>();
        try
        {
            JsonUtility.FromJsonOverwrite("{\"spells\":null}", data);
            Assert.IsFalse(data.TryGetSpell("spark", out _));
            Assert.IsFalse(Directory.Exists(_root));
        }
        finally { Object.DestroyImmediate(data); }
    }

    [Test]
    public void PreviousSpellAssetMigratesDurationAndRebuildsChangedKeys()
    {
        const string working = "Assets/EditedCatalog.asset";
        byte[] oldBytes = File.ReadAllBytes(PreviousCatalog);
        StringAssert.Contains("castTime: 1.25", File.ReadAllText(PreviousCatalog));
        SOSpellCatalog old = AssetDatabase.LoadAssetAtPath<SOSpellCatalog>(PreviousCatalog);
        Assert.IsTrue(typeof(SOSpellCatalog.SpellData).IsValueType);
        Assert.IsNull(typeof(SOSpellCatalog.SpellData).GetProperty("Damage").GetSetMethod());
        Assert.IsTrue(old.TryGetSpell("ice", out var ice));
        Assert.AreEqual(7, ice.Damage);
        Assert.AreEqual(0.5f, ice.DurationSeconds);
        Assert.IsTrue(old.TryGetSpell("spark", out var spark));
        Assert.AreEqual(13, spark.Damage);
        Assert.AreEqual(1.25f, spark.DurationSeconds);
        Assert.IsFalse(old.TryGetSpell(null, out _));
        Assert.IsFalse(old.TryGetSpell("Spark", out _));

        Assert.IsTrue(AssetDatabase.CopyAsset(PreviousCatalog, working));
        try
        {
            string guid = AssetDatabase.AssetPathToGUID(working);
            var edited = AssetDatabase.LoadAssetAtPath<SOSpellCatalog>(working);
            Assert.IsTrue(edited.TryGetSpell("spark", out _));
            SerializedObject serialized = new SerializedObject(edited);
            var first = serialized.FindProperty("spells").GetArrayElementAtIndex(0);
            first.FindPropertyRelative("id").stringValue = "flare";
            first.FindPropertyRelative("damage").intValue = 21;
            Assert.IsTrue(serialized.ApplyModifiedProperties());
            Assert.IsFalse(edited.TryGetSpell("spark", out _));
            Assert.IsTrue(edited.TryGetSpell("flare", out var flare));
            Assert.AreEqual(21, flare.Damage);
            AssetDatabase.SaveAssetIfDirty(edited);
            Resources.UnloadAsset(edited);
            AssetDatabase.ImportAsset(working, ImportAssetOptions.ForceSynchronousImport);
            var reloaded = AssetDatabase.LoadAssetAtPath<SOSpellCatalog>(working);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(working));
            Assert.IsTrue(reloaded.TryGetSpell("flare", out flare));
            Assert.AreEqual(21, flare.Damage);
            Assert.AreEqual(1.25f, flare.DurationSeconds);
            Assert.IsTrue(reloaded.TryGetSpell("ice", out var other));
            Assert.AreEqual(ice.Damage, other.Damage);
            Assert.AreEqual(ice.DurationSeconds, other.DurationSeconds);
            CollectionAssert.AreEqual(oldBytes, File.ReadAllBytes(PreviousCatalog));
            Assert.IsFalse(Directory.Exists(_root));
        }
        finally { AssetDatabase.DeleteAsset(working); }
    }

    [TestCase("")]
    [TestCase("spark")]
    public void InvalidSpellIdsCannotPublishAPartialIndex(string invalid)
    {
        var data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SOSpellCatalog>(PreviousCatalog));
        try
        {
            SerializedObject serialized = new SerializedObject(data);
            var id = serialized.FindProperty("spells").GetArrayElementAtIndex(1).FindPropertyRelative("id");
            id.stringValue = invalid;
            serialized.ApplyModifiedProperties();
            Assert.Throws<InvalidDataException>(() => data.TryGetSpell("spark", out _));
            Assert.Throws<InvalidDataException>(() => data.TryGetSpell("ice", out _));
            id.stringValue = "ice";
            serialized.ApplyModifiedProperties();
            Assert.IsTrue(data.TryGetSpell("ice", out _));
        }
        finally { Object.DestroyImmediate(data); }
    }

    [Test]
    public void PreviousWalletMigratesWithoutLosingCoinAndRoundTripsBothFormats()
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "wallet.json");
        File.Copy(PreviousWallet, path);
        var wallet = ScriptableObject.CreateInstance<SOPlayerWallet>();
        var next = ScriptableObject.CreateInstance<SOPlayerWallet>();
        try
        {
            wallet.LoadLocalData();
            Assert.AreEqual(37, wallet.Coin);
            Assert.AreEqual(0, wallet.Money);
            CollectionAssert.AreEqual(File.ReadAllBytes(PreviousWallet), File.ReadAllBytes(path));
            wallet.SaveDataAtLocal();
            next.LoadLocalData();
            Assert.AreEqual(37, next.Coin);
            Assert.AreEqual(0, next.Money);
            wallet.ApplyOnline("{\"version\":2,\"coin\":37,\"money\":19}");
            wallet.CreditCoins(5);
            var service = wallet.CaptureOnline();
            Assert.AreEqual(42, service.coin);
            service.coin = 0;
            Assert.AreEqual(42, wallet.Coin);
            string online = JsonUtility.ToJson(service);
            Assert.AreEqual("{\"version\":2,\"coin\":0,\"money\":19}", online);
            next.ApplyOnline(online);
            next.SaveDataAtLocal();
            wallet.LoadLocalData();
            Assert.AreEqual(0, wallet.Coin);
            Assert.AreEqual(19, wallet.Money);
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.CreditCoins(0));
            StringAssert.Contains("\"checksum\":", File.ReadAllText(path));
        }
        finally
        {
            Object.DestroyImmediate(wallet);
            Object.DestroyImmediate(next);
        }
    }

    [TestCase("{\"version\":2,\"money\":0}")]
    [TestCase("{\"version\":2,\"coin\":0}")]
    [TestCase("{\"version\":2,\"coin\":0,\"money\":-1}")]
    [TestCase("{\"version\":9,\"coin\":0,\"money\":0}")]
    public void RejectedOnlineSnapshotPreservesBothBalances(string json)
    {
        var wallet = ScriptableObject.CreateInstance<SOPlayerWallet>();
        try
        {
            wallet.ApplyOnline("{\"version\":2,\"coin\":37,\"money\":19}");
            Assert.Throws<InvalidDataException>(() => wallet.ApplyOnline(json));
            Assert.AreEqual(37, wallet.Coin);
            Assert.AreEqual(19, wallet.Money);
            Assert.IsFalse(Directory.Exists(_root));
        }
        finally { Object.DestroyImmediate(wallet); }
    }

    [TestCase("\"version\":1", "\"version\":3")]
    [TestCase("\"coin\":37", "\"coin\":99")]
    [TestCase("\"coin\":37", "\"coin\":37,\"gems\":5")]
    public void IncompatibleWalletBlocksAWriteAndKeepsRecoveryBytes(string before, string after)
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "wallet.json");
        string old = File.ReadAllText(PreviousWallet);
        string changed = old.Replace(before, after);
        Assert.AreNotEqual(old, changed);
        File.WriteAllText(path, changed);
        var wallet = ScriptableObject.CreateInstance<SOPlayerWallet>();
        try
        {
            wallet.CreditCoins(4);
            byte[] original = File.ReadAllBytes(path);
            Assert.Throws<InvalidDataException>(() => wallet.LoadLocalData());
            Assert.AreEqual(4, wallet.Coin);
            Assert.Throws<InvalidOperationException>(wallet.SaveDataAtLocal);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
            File.Copy(PreviousWallet, path, true);
            wallet.LoadLocalData();
            Assert.AreEqual(37, wallet.Coin);
        }
        finally { Object.DestroyImmediate(wallet); }
    }
}
