using System;
using System.IO;
using System.Threading.Tasks;
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
    public async Task PreviousWalletOpensWithoutChangingItsBytesAndUpgradesOnAssignment()
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "wallet.json");
        File.Copy(PreviousWallet, path);
        var wallet = ScriptableObject.CreateInstance<SOPlayerWallet>();
        try
        {
            await wallet.OpenAsync("default");
            Assert.AreEqual(37, wallet.Coin);
            Assert.AreEqual(0, wallet.Money);
            CollectionAssert.AreEqual(File.ReadAllBytes(PreviousWallet), File.ReadAllBytes(path));
            wallet.Money = 19;
            wallet.Coin += 5;
            await wallet.CloseAsync();
            await wallet.OpenAsync("default");
            Assert.AreEqual(42, wallet.Coin);
            Assert.AreEqual(19, wallet.Money);
        }
        finally { Object.DestroyImmediate(wallet); }
    }

    [TestCase("\"version\":1", "\"version\":3")]
    [TestCase("\"coin\":37", "\"coin\":99")]
    public async Task CorruptPreviousWalletPreservesItsRecoveryBytes(string before, string after)
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "wallet.json");
        File.WriteAllText(path, File.ReadAllText(PreviousWallet).Replace(before, after));
        byte[] original = File.ReadAllBytes(path);
        var wallet = ScriptableObject.CreateInstance<SOPlayerWallet>();
        try
        {
            try { await wallet.OpenAsync("default"); Assert.Fail("The corrupted save must be rejected."); }
            catch (InvalidDataException) { }
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        }
        finally { Object.DestroyImmediate(wallet); }
    }
}
