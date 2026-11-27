using System;
using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class UnityFoundationTests
{
    private const string RootVariable = "AI_MANAGER_PERSISTENCE_ROOT";
    private string _previousRoot;
    private string _root;
    private string SavePath => Path.Combine(_root, "audio-preferences.json");

    private sealed class SerializerProbe : SOLocalData
    {
        [SerializeField] private int _value;
        [NonSerialized] public bool FailSerialization;
        [NonSerialized] public bool Prepared;

        public int Value { get => _value; set => _value = value; }
        public override void SetPathName() => FileName = "serializer-probe";
        public override void InitialValue() => _value = 1;
        protected override void PrepareForLocalSerialization() => Prepared = true;
        protected override void RestoreAfterLocalSerialization() => Prepared = false;

        protected override string SerializeLocalData()
        {
            Assert.IsTrue(Prepared);
            if (FailSerialization) throw new InvalidDataException("Fixture serializer failed.");
            return base.SerializeLocalData();
        }
    }

    [SetUp]
    public void SetUp()
    {
        _previousRoot = Environment.GetEnvironmentVariable(RootVariable);
        _root = Path.Combine(Path.GetDirectoryName(Application.dataPath), "SaveFixtures", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(RootVariable, _root);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(RootVariable, _previousRoot);
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Test]
    public void AuthoredAssetSavesWithoutCreatingAPlayerFile()
    {
        const string assetPath = "Assets/AudioPreferencesFixture.asset";
        SOAudioPreferences asset = ScriptableObject.CreateInstance<SOAudioPreferences>();
        try
        {
            AssetDatabase.CreateAsset(asset, assetPath);
            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("_volume").floatValue = 0.25f;
            Assert.IsTrue(serialized.ApplyModifiedProperties());
            AssetDatabase.SaveAssetIfDirty(asset);
            Resources.UnloadAsset(asset);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            Assert.AreEqual(0.25f, AssetDatabase.LoadAssetAtPath<SOAudioPreferences>(assetPath).Volume);
            Assert.IsFalse(File.Exists(SavePath));
        }
        finally { AssetDatabase.DeleteAsset(assetPath); }
    }

    [Test]
    public void PreviousDefaultSerializerRestoresBeforeUpgradingTheFile()
    {
        Directory.CreateDirectory(_root);
        const string fixture = "Assets/Fixtures/AudioPreferencesLegacy.json";
        File.Copy(fixture, SavePath);
        SOAudioPreferences data = ScriptableObject.CreateInstance<SOAudioPreferences>();
        try
        {
            data.LoadLocalData();
            Assert.AreEqual(0.625f, data.Volume);
            CollectionAssert.AreEqual(File.ReadAllBytes(fixture), File.ReadAllBytes(SavePath));
            data.SetVolume(0.5f);
            data.SaveDataAtLocal();
            StringAssert.StartsWith("SODATA/1\n", File.ReadAllText(SavePath));
            data.LoadLocalData();
            Assert.AreEqual(0.5f, data.Volume);
        }
        finally { Object.DestroyImmediate(data); }
    }

    [Test]
    public void LocalFileRoundTripsAndFailedReloadPreservesBothCopies()
    {
        SOAudioPreferences first = ScriptableObject.CreateInstance<SOAudioPreferences>();
        SOAudioPreferences next = ScriptableObject.CreateInstance<SOAudioPreferences>();
        try
        {
            first.LoadLocalData();
            Assert.AreEqual(1f, first.Volume);
            first.SetVolume(0.25f);
            first.SaveDataAtLocal();
            next.LoadLocalData();
            Assert.AreEqual(0.25f, next.Volume);
            string valid = File.ReadAllText(SavePath);
            string damaged = valid.Substring(0, valid.Length - 4);
            Assert.AreNotEqual(valid, damaged);
            File.WriteAllText(SavePath, damaged);
            Assert.Throws<InvalidDataException>(() => next.LoadLocalData());
            Assert.AreEqual(0.25f, next.Volume);
            Assert.Throws<InvalidOperationException>(() => next.SaveDataAtLocal());
            Assert.AreEqual(damaged, File.ReadAllText(SavePath));
            File.WriteAllText(SavePath, valid);
            next.LoadLocalData();
            next.Clean();
            first.LoadLocalData();
            Assert.AreEqual(1f, first.Volume);
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(next);
        }
    }

    [Test]
    public void LegacyStringSaveMigratesAndUnsupportedRecordStaysIntact()
    {
        SOAudioPreferences data = ScriptableObject.CreateInstance<SOAudioPreferences>();
        try
        {
            data.LoadLocalData();
            data.SetVolume(0.5f);
            data.SaveDataAtLocal();
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(SOFileStorage.ReadSnapshot(SavePath)));
            using (BinaryWriter legacy = new BinaryWriter(File.Create(SavePath)))
            {
                legacy.Write((byte)0); // Stream header, root ID, header ID and version.
                legacy.Write(1);
                legacy.Write(-1);
                legacy.Write(1);
                legacy.Write(0);
                legacy.Write((byte)6); // BinaryObjectString.
                legacy.Write(1);
                legacy.Write(encoded);
                legacy.Write((byte)11); // MessageEnd.
            }
            byte[] legacyFile = File.ReadAllBytes(SavePath);
            data.SetVolume(0f);
            data.LoadLocalData();
            Assert.AreEqual(0.5f, data.Volume);
            data.SaveDataAtLocal();
            StringAssert.StartsWith("SODATA/1\n", File.ReadAllText(SavePath));
            legacyFile[17] = 5;
            File.WriteAllBytes(SavePath, legacyFile);
            Assert.Throws<InvalidDataException>(() => data.LoadLocalData());
            CollectionAssert.AreEqual(legacyFile, File.ReadAllBytes(SavePath));
        }
        finally { Object.DestroyImmediate(data); }
    }

    [Test]
    public void FailedSaveRestoresSerializationStateAndCanBeRetried()
    {
        SerializerProbe data = ScriptableObject.CreateInstance<SerializerProbe>();
        try
        {
            data.LoadLocalData();
            string path = Path.Combine(_root, "serializer-probe");
            byte[] committed = File.ReadAllBytes(path);
            data.Value = 8;
            string previous = JsonUtility.ToJson(data);
            data.FailSerialization = true;
            Assert.Throws<InvalidDataException>(() => data.SaveDataAtLocal());
            Assert.IsFalse(data.Prepared);
            Assert.AreEqual(previous, JsonUtility.ToJson(data));
            CollectionAssert.AreEqual(committed, File.ReadAllBytes(path));
            data.FailSerialization = false;
            data.SaveDataAtLocal();
            data.Value = 0;
            data.LoadLocalData();
            Assert.AreEqual(8, data.Value);
            Assert.IsEmpty(Directory.GetFiles(_root, "*.tmp"));
        }
        finally { Object.DestroyImmediate(data); }
    }

    [UnityTest]
    public IEnumerator DuplicateRetiresAndTheNextOwnerLoadsTheFinalValue()
    {
        yield return new EnterPlayMode();
        yield return VerifyOwnersInPlay();
    }

    private IEnumerator VerifyOwnersInPlay()
    {
        Assert.IsTrue(Application.isPlaying);
        AudioPreferencesManager first = new GameObject("First").AddComponent<AudioPreferencesManager>();
        AudioPreferencesManager duplicate = new GameObject("Duplicate").AddComponent<AudioPreferencesManager>();
        Assert.AreSame(first, AudioPreferencesManager.Instance);
        Assert.IsNull(duplicate.Settings);
        Assert.AreEqual(1f, first.Settings.Volume);
        first.Settings.SetVolume(0.25f);
        string firstSave = File.ReadAllText(SavePath);
        first.Settings.SetVolume(0.75f);
        Assert.AreEqual(firstSave, File.ReadAllText(SavePath));
        yield return null;
        Assert.IsTrue(duplicate == null);
        Assert.AreSame(first, AudioPreferencesManager.Instance);
        Object.Destroy(first.gameObject);
        yield return null;
        Assert.IsNull(AudioPreferencesManager.Instance);
        AudioPreferencesManager replacement = new GameObject("Replacement").AddComponent<AudioPreferencesManager>();
        Assert.AreSame(replacement, AudioPreferencesManager.Instance);
        Assert.AreEqual(0.75f, replacement.Settings.Volume);
        string damaged = "{}";
        File.WriteAllText(SavePath, damaged);
        Assert.Throws<InvalidDataException>(() => replacement.Settings.LoadLocalData());
        Object.Destroy(replacement.gameObject);
        yield return null;
        Assert.AreEqual(damaged, File.ReadAllText(SavePath));
        Assert.IsNull(AudioPreferencesManager.Instance);
    }

    [UnityTearDown]
    public IEnumerator ExitTestScene()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
