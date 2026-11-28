using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class UnityFoundationTests
{
    private const string RootVariable = "AI_MANAGER_PERSISTENCE_ROOT";
    private string _previousRoot;
    private string _root;
    private string SavePath => Path.Combine(_root, "audio-preferences.json");

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
    public void AudioPreferencesReadTheOriginalSaveAndCommitOnlyWhenChanged()
    {
        Directory.CreateDirectory(_root);
        File.Copy("Assets/Fixtures/AudioPreferencesLegacy.json", SavePath);
        var audio = ScriptableObject.CreateInstance<SOAudioPreferences>();
        try
        {
            byte[] bytes = File.ReadAllBytes(SavePath);
            Assert.AreEqual(0.625f, audio.Volume);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(SavePath));
            audio.Volume = 0.75f;
            audio.CloseData();
            Assert.AreEqual(0.75f, audio.Volume);
        }
        finally { Object.DestroyImmediate(audio); }
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
        first.Settings.Volume = 0.25f;
        string firstSave = File.ReadAllText(SavePath);
        first.Settings.Volume = 0.75f;
        Assert.AreNotEqual(firstSave, File.ReadAllText(SavePath));
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
        replacement.Settings.CloseData();
        Assert.Throws<InvalidDataException>(() => { var volume = replacement.Settings.Volume; });
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
