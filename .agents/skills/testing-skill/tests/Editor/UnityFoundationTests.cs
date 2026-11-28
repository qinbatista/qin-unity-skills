using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
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
    public async Task TearDown()
    {
        Dictionary<string, Task> retiring = (Dictionary<string, Task>)typeof(SODataController).GetField("Retirements", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        try { await Task.WhenAll(retiring.Values.ToArray()); }
        catch (Exception) { /* Failure cases assert the original error before resource cleanup. */ }
        Environment.SetEnvironmentVariable(RootVariable, _previousRoot);
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Test]
    public async Task AudioPreferencesReadTheOriginalSaveAndCommitOnlyWhenChanged()
    {
        Directory.CreateDirectory(_root);
        File.Copy("Assets/Fixtures/AudioPreferencesLegacy.json", SavePath);
        var audio = ScriptableObject.CreateInstance<SOAudioPreferences>();
        try
        {
            await audio.OpenAsync("default");
            byte[] bytes = File.ReadAllBytes(SavePath);
            Assert.AreEqual(0.625f, audio.Volume);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(SavePath));
            audio.Volume = 0.75f;
            await audio.CloseAsync();
            await audio.OpenAsync("default");
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
        Assert.IsFalse(File.Exists(SavePath));
        first.Settings.Volume = 0.75f;
        Assert.IsFalse(File.Exists(SavePath));
        yield return null;
        Assert.IsTrue(duplicate == null);
        Assert.AreSame(first, AudioPreferencesManager.Instance);
        SOAudioPreferences retiredSettings = first.Settings;
        Object.Destroy(first.gameObject);
        yield return null;
        Task retirement = retiredSettings.Retirement;
        double deadline = UnityEditor.EditorApplication.timeSinceStartup + 10;
        while (!retirement.IsCompleted && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
        Assert.IsTrue(retirement.IsCompleted);
        retirement.GetAwaiter().GetResult();
        Assert.IsNull(AudioPreferencesManager.Instance);
        Assert.IsTrue(File.Exists(SavePath));
        AudioPreferencesManager replacement = new GameObject("Replacement").AddComponent<AudioPreferencesManager>();
        Assert.AreSame(replacement, AudioPreferencesManager.Instance);
        float initial = replacement.Settings.Volume;
        Task initialization = replacement.Settings.Initialization;
        deadline = UnityEditor.EditorApplication.timeSinceStartup + 10;
        while (!initialization.IsCompleted && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
        Assert.IsTrue(initialization.IsCompleted);
        initialization.GetAwaiter().GetResult();
        Assert.IsTrue(replacement.Settings.IsReady);
        StringAssert.Contains("0.75", SOFileStorage.ReadSnapshot(SavePath));
        Assert.AreEqual(0.75f, replacement.Settings.Volume);
        string damaged = "{}";
        File.WriteAllText(SavePath, damaged);
        replacement.Settings.CloseData();
        float stale = replacement.Settings.Volume;
        Task invalid = replacement.Settings.Initialization;
        deadline = UnityEditor.EditorApplication.timeSinceStartup + 10;
        while (!invalid.IsCompleted && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
        Assert.IsInstanceOf<InvalidDataException>(replacement.Settings.StorageError);
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
