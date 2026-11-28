using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

// This source is compiled only into the disposable external probe application, never a game or Skill payload.
public static class SODeviceProbe
{
    [Serializable] private sealed class Settings { public string projectId; public string environment; public string profile; }
    [Serializable] private sealed class ModeReceipt
    {
        public string mode;
        public int initial;
        public int confirmed;
        public int duplicateEvents;
        public int framesWhilePending;
        public double foregroundMilliseconds;
        public bool independentCloudRead;
    }
    [Serializable] private sealed class Receipt
    {
        public string status;
        public string phase;
        public string platform;
        public string device;
        public string unity;
        public string error;
        public bool remoteCleanup;
        public List<ModeReceipt> modes = new List<ModeReceipt>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static async void Run()
    {
        if (Application.isEditor) return;
        string first = Path.Combine(Application.persistentDataPath, "so-device-first.json");
        bool reopen = File.Exists(first);
        Receipt receipt = new Receipt { phase = reopen ? "cold-reopen" : "first-write", platform = Application.platform.ToString(), device = SystemInfo.deviceModel, unity = Application.unityVersion };
        string output = reopen ? Path.Combine(Application.persistentDataPath, "so-device-reopen.json") : first;
        bool signedIn = false;
        try
        {
            Settings settings = JsonUtility.FromJson<Settings>(Resources.Load<TextAsset>("SODeviceSettings").text);
            Require(settings.projectId == Application.cloudProjectId, "The Player must bind the selected UGS project.");
            await UnityServices.InitializeAsync(new InitializationOptions().SetEnvironmentName(settings.environment).SetProfile(settings.profile));
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            signedIn = true;
            foreach (SODataMode mode in new[] { SODataMode.None, SODataMode.LocalFile, SODataMode.DirectAccess, SODataMode.DirectAccessOnline, SODataMode.LocalFileOnline })
                receipt.modes.Add(await ExerciseModeAsync(mode, reopen));
            receipt.modes.Add(await ExerciseRealtimeAsync());
            receipt.status = "PASS";
        }
        catch (Exception error) { receipt.status = "FAIL"; receipt.error = error.ToString(); Debug.LogException(error); }
        finally
        {
            if (signedIn && (reopen || receipt.status == "FAIL"))
            {
                try
                {
                    var files = await CloudSaveService.Instance.Files.Player.ListAllAsync();
                    foreach (var file in files) await CloudSaveService.Instance.Files.Player.DeleteAsync(file.Key);
                    await CloudSaveService.Instance.Data.Player.DeleteAllAsync();
                    await AuthenticationService.Instance.DeleteAccountAsync();
                    AuthenticationService.Instance.SignOut(true);
                    receipt.remoteCleanup = true;
                }
                catch (Exception error) { receipt.status = "FAIL"; receipt.error += " Cleanup: " + error; Debug.LogException(error); }
            }
            PlayerPrefs.Save();
            string json = JsonUtility.ToJson(receipt, true);
            await Task.Run(() => File.WriteAllText(output, json));
            Debug.Log("SO_DEVICE_RECEIPT " + JsonUtility.ToJson(receipt));
        }
    }

    private static async Task<ModeReceipt> ExerciseModeAsync(SODataMode mode, bool reopen)
    {
        SODeviceValue data = ScriptableObject.CreateInstance<SODeviceValue>();
        data.selectedMode = mode;
        string slot = "device/" + mode;
        ModeReceipt result = new ModeReceipt { mode = mode.ToString() };
        try
        {
            await data.OpenAsync(slot);
            Require(data.StorageError == null, mode + " initial storage failure: " + data.StorageError);
            result.initial = data.value;
            Require(data.value == (reopen && mode != SODataMode.None ? 5 : 37), mode + " cold value differs.");
            if (reopen && (mode == SODataMode.LocalFile || mode == SODataMode.LocalFileOnline)) Require(data.padding.Length == 2 * 1024 * 1024, "The whole snapshot must restore.");
            int events = 0;
            data.DataChanged += key => { if (key == "value") events++; };
            if (mode == SODataMode.LocalFile || mode == SODataMode.LocalFileOnline) data.padding = new string('x', 2 * 1024 * 1024);
            Stopwatch watch = Stopwatch.StartNew();
            data.value = 5;
            for (int i = 0; i < 10000; i++) data.value = 5;
            watch.Stop();
            result.foregroundMilliseconds = watch.Elapsed.TotalMilliseconds;
            Task saving = data.FlushAsync();
            int frame = Time.frameCount;
            await AwaitReceiptAsync(saving, () => Require(data.value == 5, "A delayed reply replaced the visible value."));
            result.framesWhilePending = Time.frameCount - frame;
            int acceptedEvents = events;
            string revision = await CloudRevisionAsync(data, mode, slot);
            for (int i = 0; i < 10000; i++) data.value = 5;
            await data.FlushAsync();
            result.duplicateEvents = events - acceptedEvents;
            Require(result.duplicateEvents == 0, "Equal assignments notified gameplay again.");
            Require(revision == await CloudRevisionAsync(data, mode, slot), "Equal assignments created a cloud revision.");
            if (mode == SODataMode.DirectAccessOnline)
            {
                string key = UnityCloudSaveKeyStore.GetCloudKey(data.GetStorageScope(slot), "value");
                var records = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { key });
                Require(records[key].Value.GetAsString() == "{\"value\":5}", "Independent cloud key read differs.");
                result.independentCloudRead = true;
            }
            if (mode == SODataMode.LocalFileOnline)
            {
                data.value = 6;
                Task uploading = data.FlushAsync();
                data.value = 7;
                await AwaitReceiptAsync(uploading, () => Require(data.value == 7, "An old file receipt overwrote 7."));
                await data.FlushAsync();
                data.value = 5;
                await data.FlushAsync();
                var files = await CloudSaveService.Instance.Files.Player.ListAllAsync();
                Require(files.Count == 1, "Replaced file blobs were retained.");
                byte[] bytes = await CloudSaveService.Instance.Files.Player.LoadBytesAsync(files.Single().Key);
                Require(Encoding.UTF8.GetString(bytes).Contains("\"value\":5"), "Independent cloud file read differs.");
                result.independentCloudRead = true;
            }
            result.confirmed = data.value;
            await data.CloseAsync();
            if (!reopen && mode == SODataMode.LocalFileOnline)
            {
                // Remove only this mode's local snapshot, leaving other domains and device-key ownership intact.
                string hash = SOFileEnvelope.Revision(Encoding.UTF8.GetBytes(data.GetStorageScope(slot)));
                string directory = Path.Combine(Application.persistentDataPath, "SOData", "profiles", hash);
                await Task.Run(() => Directory.Delete(directory, true));
            }
            return result;
        }
        finally { data.CloseData(discardLocalChanges: true); Object.Destroy(data); }
    }

    private static async Task<string> CloudRevisionAsync(SODeviceValue data, SODataMode mode, string slot)
    {
        if (mode == SODataMode.DirectAccessOnline) return (await new UnityCloudSaveKeyStore().ReadAsync(data.GetStorageScope(slot), "value", CancellationToken.None))?.Revision;
        if (mode == SODataMode.LocalFileOnline) return await new UnityCloudSaveFileStore().GetRevisionAsync(data.GetStorageScope(slot), CancellationToken.None);
        return null;
    }

    private static async Task<ModeReceipt> ExerciseRealtimeAsync()
    {
        GameObject prefab = Resources.Load<GameObject>("SODeviceNetwork");
        ushort port;
        using (UdpClient socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0))) port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
        NetworkManager server = CreatePeer("Storage authority", prefab, port);
        NetworkManager client = CreatePeer("Storage observer", prefab, port);
        SODeviceValue authority = ScriptableObject.CreateInstance<SODeviceValue>();
        SODeviceValue observer = ScriptableObject.CreateInstance<SODeviceValue>();
        authority.selectedMode = observer.selectedMode = SODataMode.RealtimeOnline;
        ModeReceipt receipt = new ModeReceipt { mode = "RealtimeOnline", initial = authority.value };
        NetworkObject network = null;
        try
        {
            Require(server.StartServer() && client.StartClient(), "The real UDP peers did not start.");
            await UntilAsync(() => client.IsConnectedClient, "client connection");
            network = Object.Instantiate(prefab).GetComponent<NetworkObject>();
            SOUnityNetcodeStore channel = network.GetComponent<SOUnityNetcodeStore>();
            channel.SetScopeBeforeSpawn(authority.GetStorageScope("match"));
            network.Spawn();
            await UntilAsync(() => client.SpawnManager.SpawnedObjects.ContainsKey(network.NetworkObjectId), "replica spawn");
            SOUnityNetcodeStore replica = client.SpawnManager.SpawnedObjects[network.NetworkObjectId].GetComponent<SOUnityNetcodeStore>();
            await authority.OpenAsync("match", channel);
            await observer.OpenAsync("match", replica);
            authority.value = 5;
            await authority.FlushAsync();
            await UntilAsync(() => observer.value == 5, "remote property push");
            int pushes = 0;
            observer.DataChanged += key => { if (key == "value") pushes++; };
            string revision = (await channel.ReadAsync(authority.GetStorageScope("match"), "value", CancellationToken.None)).Revision;
            for (int i = 0; i < 10000; i++) authority.value = 5;
            await authority.FlushAsync();
            Require(pushes == 0 && revision == (await channel.ReadAsync(authority.GetStorageScope("match"), "value", CancellationToken.None)).Revision, "Equal live values were resubmitted.");
            receipt.confirmed = observer.value;
            bool denied = false;
            try { observer.value = 99; await observer.FlushAsync(); }
            catch (UnauthorizedAccessException) { denied = true; }
            Require(denied, "A client raw write must be rejected.");
            await authority.CloseAsync();
            observer.CloseData(discardLocalChanges: true);
            return receipt;
        }
        finally
        {
            authority.CloseData(discardLocalChanges: true);
            observer.CloseData(discardLocalChanges: true);
            Object.Destroy(authority);
            Object.Destroy(observer);
            if (network != null && network.IsSpawned) network.Despawn();
            client.Shutdown();
            server.Shutdown();
            await Task.Yield();
            Object.Destroy(client.gameObject);
            Object.Destroy(server.gameObject);
        }
    }

    private static NetworkManager CreatePeer(string name, GameObject prefab, ushort port)
    {
        GameObject host = new GameObject(name);
        host.SetActive(false);
        NetworkManager manager = host.AddComponent<NetworkManager>();
        UnityTransport transport = host.AddComponent<UnityTransport>();
        manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
        transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
        host.SetActive(true);
        manager.AddNetworkPrefab(prefab);
        return manager;
    }

    private static async Task UntilAsync(Func<bool> condition, string operation)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 20;
        while (!condition()) { Require(Time.realtimeSinceStartupAsDouble < deadline, "Timed out: " + operation); await Task.Yield(); }
    }

    private static async Task AwaitReceiptAsync(Task work, Action inspect)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 120;
        while (!work.IsCompleted) { Require(Time.realtimeSinceStartupAsDouble < deadline, "Storage receipt timed out."); inspect(); await Task.Yield(); }
        await work;
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
