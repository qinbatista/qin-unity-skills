#if SO_DATA_UNITY_CLOUD_SAVE
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class UnityFoundationTests
{
    [Serializable] private sealed class HostedFileSettings { public string projectId; public string environment; }

    [UnityTest]
    public IEnumerator HostedUnityFilesRestoresALocalSnapshotForADisposablePlayer()
    {
        string settings = Environment.GetEnvironmentVariable("SO_STORAGE_LIVE_CONFIG");
        if (string.IsNullOrEmpty(settings)) Assert.Ignore("Pass private project settings outside the Skill to run hosted service acceptance.");
        yield return new EnterPlayMode();
        _root = Environment.GetEnvironmentVariable(RootVariable);
        Task operation = RunHostedFileAsync(Environment.GetEnvironmentVariable("SO_STORAGE_LIVE_CONFIG"));
        double deadline = EditorApplication.timeSinceStartup + 180;
        while (!operation.IsCompleted && EditorApplication.timeSinceStartup < deadline) yield return null;
        Assert.IsTrue(operation.IsCompleted, "The hosted file operation did not finish within its test budget.");
        operation.GetAwaiter().GetResult();
    }

    private async Task RunHostedFileAsync(string settingsPath)
    {
        HostedFileSettings settings = JsonUtility.FromJson<HostedFileSettings>(File.ReadAllText(settingsPath));
        Assert.AreEqual(settings.projectId, Application.cloudProjectId);
        InitializationOptions options = new InitializationOptions().SetEnvironmentName(settings.environment).SetProfile("sofiles-" + Guid.NewGuid().ToString("N").Substring(0, 16));
        await UnityServices.InitializeAsync(options);
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        SOStorageWorkloadData data = Domain<SOStorageWorkloadData>(SODataMode.LocalFileOnline, "hosted-file");
        UnityCloudSaveFileStore provider = new UnityCloudSaveFileStore();
        bool cleaned = false;
        try
        {
            JsonUtility.FromJsonOverwrite("{\"_padding\":\"" + new string('x', 2 * 1024 * 1024) + "\"}", data);
            await data.OpenAsync("slot", fileProvider: provider);
            Assert.IsNull(data.StorageError);
            data.Value = 5;
            await data.FlushAsync();
            // A direct SDK read independently verifies the uploaded file, without using the wrapper's cache.
            var files = await CloudSaveService.Instance.Files.Player.ListAllAsync();
            Assert.AreEqual(1, files.Count);
            byte[] bytes = await CloudSaveService.Instance.Files.Player.LoadBytesAsync(files.Single().Key);
            StringAssert.Contains("\"value\":5", Encoding.UTF8.GetString(bytes));
            SODataFileRecord accepted = await provider.ReadAsync(data.GetStorageScope("slot"), CancellationToken.None);
            for (int i = 0; i < 10000; i++) data.Value = 5;
            await data.FlushAsync();
            Assert.AreEqual(accepted.Revision, (await provider.ReadAsync(data.GetStorageScope("slot"), CancellationToken.None)).Revision);
            await data.CloseAsync();
            Directory.Delete(_root, true);
            Object.DestroyImmediate(data);
            data = Domain<SOStorageWorkloadData>(SODataMode.LocalFileOnline, "hosted-file");
            await data.OpenAsync("slot", fileProvider: new UnityCloudSaveFileStore());
            Assert.AreEqual(5, data.Value);
            Assert.AreEqual(2 * 1024 * 1024, data.Padding.Length);
            data.Value = 6;
            await data.FlushAsync();
            files = await CloudSaveService.Instance.Files.Player.ListAllAsync();
            Assert.AreEqual(1, files.Count);
            bytes = await CloudSaveService.Instance.Files.Player.LoadBytesAsync(files.Single().Key);
            StringAssert.Contains("\"value\":6", Encoding.UTF8.GetString(bytes));
            await data.CloseAsync();
            TestContext.WriteLine("SO hosted files: independentSDKRead=5 coldLocalRemoved=True restored=5 changed=6 equalAssignments=10000 extraRevisions=0 retainedRemoteFiles=1 provider=realUnityCloudSaveFiles");
        }
        finally
        {
            data.CloseData(discardLocalChanges: true);
            Object.DestroyImmediate(data);
            var files = await CloudSaveService.Instance.Files.Player.ListAllAsync();
            foreach (var file in files) await CloudSaveService.Instance.Files.Player.DeleteAsync(file.Key);
            await CloudSaveService.Instance.Data.Player.DeleteAllAsync();
            await AuthenticationService.Instance.DeleteAccountAsync();
            AuthenticationService.Instance.SignOut(true);
            PlayerPrefs.Save();
            cleaned = true;
            TestContext.WriteLine("SO hosted files: disposableFilesRemoved=True disposablePlayerDeleted=True");
        }
        Assert.IsTrue(cleaned);
    }
}
#endif
