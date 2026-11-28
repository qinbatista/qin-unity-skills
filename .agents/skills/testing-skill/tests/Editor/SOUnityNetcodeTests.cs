using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class SOUnityNetcodeTests
{
    private const string PathSlot = "SOUnityNetcodeTests.Prefab";
    private const string Scope = "match-37/fighter";
    private const string Profile = "match-37/opponent";
    private const string Key = "position";
    private static readonly CancellationToken Token = CancellationToken.None;
    private readonly List<NetworkManager> peers = new List<NetworkManager>();

    [UnityTest]
    public IEnumerator TwoRemotePeersReceiveAuthoritativeStateOverLoopback()
    {
        if (!Application.isPlaying && !AssetDatabase.LoadAssetAtPath<GameObject>(SessionState.GetString(PathSlot, "")))
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/SODataNetcodeTest.prefab");
            GameObject source = new GameObject("Replicated SO");
            source.AddComponent<NetworkObject>();
            source.AddComponent<SOUnityNetcodeStore>().SetScopeBeforeSpawn("prefab/default");
            PrefabUtility.SaveAsPrefabAsset(source, path);
            Object.DestroyImmediate(source);
            SessionState.SetString(PathSlot, path);
        }
        yield return new EnterPlayMode();
        yield return ExercisePeers();
    }

    private IEnumerator ExercisePeers()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SessionState.GetString(PathSlot, ""));
        Assert.IsNotNull(prefab);
        ushort port;
        using (var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            port = (ushort)((IPEndPoint)udp.Client.LocalEndPoint).Port;

        NetworkManager server = CreatePeer("Dedicated server", port, prefab);
        Assert.IsTrue(server.StartServer());
        NetworkManager first = CreatePeer("First client", port, prefab);
        Assert.IsTrue(first.StartClient());
        yield return Until(() => first.IsConnectedClient, "first connection");
        Assert.IsTrue(server.IsServer && !server.IsClient && first.IsClient && !first.IsServer);
        var serverObject = Object.Instantiate(prefab).GetComponent<NetworkObject>();
        var authority = serverObject.GetComponent<SOUnityNetcodeStore>();
        authority.SetScopeBeforeSpawn(Scope);
        serverObject.Spawn();
        ulong id = serverObject.NetworkObjectId;
        yield return Until(() => first.SpawnManager.SpawnedObjects.ContainsKey(id), "first replica");

        var replica = first.SpawnManager.SpawnedObjects[id].GetComponent<SOUnityNetcodeStore>();
        Assert.AreNotSame(authority, replica);
        SOCurrencyData wallet = ScriptableObject.CreateInstance<SOCurrencyData>();
        try { Assert.Throws<ArgumentException>(() => wallet.OpenAsync(Profile, authority).GetAwaiter().GetResult()); }
        finally { Object.Destroy(wallet); }
        List<SODataRecord> changes = new List<SODataRecord>();
        using var subscription = replica.SubscribeAsync(Scope, Key, changes.Add, Token).GetAwaiter().GetResult();
        Assert.AreEqual(1, changes.Count);
        Assert.IsNull(changes[0]);
        Assert.Throws<UnauthorizedAccessException>(() => replica.WriteAsync(Scope, Key, "99", null, Token).GetAwaiter().GetResult());
        Assert.Throws<ArgumentException>(() => replica.ReadAsync("other/fighter", Key, Token).GetAwaiter().GetResult());
        Assert.Throws<OperationCanceledException>(() => authority.WriteAsync(Scope, Key, "100", null, new CancellationToken(true)).GetAwaiter().GetResult());
        Assert.Throws<ArgumentOutOfRangeException>(() => authority.WriteAsync(Scope, Key, new string('x', 510), null, Token).GetAwaiter().GetResult());
        var initial = authority.WriteAsync(Scope, Key, "10", null, Token).GetAwaiter().GetResult();
        yield return Until(() => changes.Count == 2, "first server push");
        Assert.AreEqual(initial.Revision, changes[1].Revision);

        var late = CreatePeer("Late client", port, prefab);
        Assert.IsTrue(late.StartClient());
        yield return Until(() => late.IsConnectedClient && late.SpawnManager.SpawnedObjects.ContainsKey(id), "late join");
        var second = late.SpawnManager.SpawnedObjects[id].GetComponent<SOUnityNetcodeStore>();
        List<SODataRecord> history = new List<SODataRecord>();
        using var lateSubscription = second.SubscribeAsync(Scope, Key, history.Add, Token).GetAwaiter().GetResult();
        Assert.AreEqual(1, history.Count);
        Assert.AreEqual("10", history[0].Value);
        Assert.AreEqual(initial.Revision, history[0].Revision);
        authority.WriteAsync(Scope, "health", "50", null, Token).GetAwaiter().GetResult();
        var moved = authority.WriteAsync(Scope, Key, "15", initial.Revision, Token).GetAwaiter().GetResult();
        Assert.Throws<SODataConflictException>(() => authority.WriteAsync(Scope, Key, "90", initial.Revision, Token).GetAwaiter().GetResult());
        yield return Until(() => changes.Count == 3 && history.Count == 2, "both replicas");
        Assert.AreEqual(moved.Revision, history[1].Revision);
        Assert.AreEqual("15", changes[2].Value);

        subscription.Dispose();
        var final = authority.WriteAsync(Scope, Key, "20", moved.Revision, Token).GetAwaiter().GetResult();
        yield return Until(() => history.Count == 3 && replica.ReadAsync(Scope, Key, Token).Result.Revision == final.Revision, "unsubscribed replica");
        Assert.AreEqual(3, changes.Count);
        Assert.AreEqual("20", history[2].Value);
        late.Shutdown();
        yield return Until(() => !late.IsListening && server.ConnectedClients.Count == 1, "late peer disconnect");
        authority.WriteAsync(Scope, Key, "25", final.Revision, Token).GetAwaiter().GetResult();
        yield return Until(() => replica.ReadAsync(Scope, Key, Token).Result.Value == "25", "surviving peer");
        Assert.AreEqual(3, history.Count);
        Debug.Log($"SO netcode replay on UDP loopback port {port}: late join read {history[0].Value}, both clients received {changes[2].Value}, the late peer received {history[2].Value}, and the surviving replica read 25 after disconnect.");
        yield return ProjectDomain<SOOutfitData>(prefab, first, "{\"_storageMode\":4,\"hatId\":\"straw\"}", VerifyOutfit);
        yield return ProjectDomain<SOFighterPositionData>(prefab, first, "{\"_storageMode\":4}", VerifyOpponentPosition);
        yield return ProjectDomain<SOStorageWorkloadData>(prefab, first, "{\"_storageMode\":4}", VerifyEqualScalar);
    }

    private static IEnumerator ProjectDomain<T>(GameObject prefab, NetworkManager client, string defaults,
        Func<T, T, SOUnityNetcodeStore, SOUnityNetcodeStore, IEnumerator> verify) where T : SODataController
    {
        var serverState = ScriptableObject.CreateInstance<T>();
        JsonUtility.FromJsonOverwrite(defaults, serverState);
        var clientState = Object.Instantiate(serverState);
        var serverObject = Object.Instantiate(prefab).GetComponent<NetworkObject>();
        try
        {
            var serverStore = serverObject.GetComponent<SOUnityNetcodeStore>();
            serverStore.SetScopeBeforeSpawn(serverState.GetStorageScope(Profile));
            serverObject.Spawn();
            Task serverOpening = serverState.OpenAsync(Profile, serverStore);
            yield return Until(() => serverOpening.IsCompleted, "server SO initialization");
            serverOpening.GetAwaiter().GetResult();
            yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(serverObject.NetworkObjectId), "domain spawn");
            var clientStore = client.SpawnManager.SpawnedObjects[serverObject.NetworkObjectId].GetComponent<SOUnityNetcodeStore>();
            Task clientOpening = clientState.OpenAsync(Profile, clientStore);
            yield return Until(() => clientOpening.IsCompleted, "client SO initialization");
            clientOpening.GetAwaiter().GetResult();
            yield return verify(serverState, clientState, serverStore, clientStore);
        }
        finally
        {
            serverState.CloseData();
            clientState.CloseData();
            Object.Destroy(serverState);
            Object.Destroy(clientState);
            if (serverObject.IsSpawned) serverObject.Despawn();
            else Object.Destroy(serverObject.gameObject);
        }
    }

    private static IEnumerator VerifyOutfit(SOOutfitData server, SOOutfitData client, SOUnityNetcodeStore authority, SOUnityNetcodeStore store)
    {
        Assert.AreEqual("straw", client.HatId);
        int notifications = 0;
        client.DataChanged += key => { if (key == "hatId") notifications++; };
        server.HatId = "felt";
        yield return ConfirmServerWrite(server);
        yield return Until(() => client.HatId == "felt", "outfit SO push");
        Assert.AreEqual("felt", server.HatId);
        Assert.AreEqual(1, notifications);
        var scope = server.GetStorageScope(Profile);
        string revision = store.ReadAsync(scope, "hatId", Token).Result.Revision;
        int duplicateReceipts = 0;
        server.DataChanged += _ => duplicateReceipts++;
        for (int i = 0; i < 100; i++) server.HatId = "felt";
        yield return ConfirmServerWrite(server);
        Assert.AreEqual(0, duplicateReceipts);
        Assert.AreEqual(revision, store.ReadAsync(scope, "hatId", Token).Result.Revision);
        Assert.AreEqual(revision, authority.ReadAsync(scope, "hatId", Token).Result.Revision);
        yield return ExpectDenied(client, () => client.HatId = "denim");
        Assert.AreEqual("felt", client.HatId);
        int beforeClose = notifications;
        client.CloseData();
        server.HatId = "wool";
        yield return ConfirmServerWrite(server);
        yield return Until(() => store.ReadAsync(scope, "hatId", Token).Result.Value == "{\"value\":\"wool\"}", "post-close push");
        Assert.AreEqual(beforeClose, notifications);
        Assert.AreEqual("straw", client.HatId);
    }

    private static IEnumerator VerifyOpponentPosition(SOFighterPositionData server, SOFighterPositionData opponentView,
        SOUnityNetcodeStore authority, SOUnityNetcodeStore store)
    {
        Vector3 acceptedPosition = new Vector3(4.5f, 0, -2);
        Assert.AreEqual(Vector3.zero, opponentView.Position);
        int pushes = 0;
        opponentView.DataChanged += key => { if (key == "position") pushes++; };
        server.Position = acceptedPosition;
        yield return ConfirmServerWrite(server);
        yield return Until(() => opponentView.Position == acceptedPosition, "opponent's world position");
        Assert.AreEqual(1, pushes);
        int start = pushes;
        int receipts = 0;
        server.DataChanged += key => { if (key == "position") receipts++; };
        for (int i = 1; i <= 32; i++) server.Position = new Vector3(i, 0, 0);
        acceptedPosition = new Vector3(32, 0, 0);
        yield return ConfirmServerWrite(server);
        yield return Until(() => opponentView.Position == acceptedPosition, "last position in one frame's burst");
        int burstReceipts = receipts;
        int burstPushes = pushes - start;
        string scope = server.GetStorageScope(Profile);
        string revision = store.ReadAsync(scope, Key, Token).Result.Revision;
        for (int i = 0; i < 100; i++) server.Position = acceptedPosition;
        yield return ConfirmServerWrite(server);
        Assert.AreEqual(burstReceipts, receipts);
        Assert.AreEqual(start + burstPushes, pushes);
        Assert.AreEqual(revision, store.ReadAsync(scope, Key, Token).Result.Revision);
        Assert.AreEqual(revision, authority.ReadAsync(scope, Key, Token).Result.Revision);
        Debug.Log($"SO cost RealtimeOnline: changesInOneFrame=32 serverReceipts={burstReceipts} remotePositionUpdates={burstPushes} cleanAssignments=100 cleanReceipts={receipts - burstReceipts} finalX={opponentView.Position.x}");
        yield return ExpectDenied(opponentView, () => opponentView.Position = Vector3.one);
        Assert.AreEqual(acceptedPosition, opponentView.Position);
        Debug.Log("SO opponent replay: the remote SO received the initial position and the final burst position, and a client position override was rejected.");
    }

    private static IEnumerator VerifyEqualScalar(SOStorageWorkloadData server, SOStorageWorkloadData client,
        SOUnityNetcodeStore authority, SOUnityNetcodeStore replica)
    {
        int receipts = 0;
        int pushes = 0;
        server.DataChanged += key => receipts++;
        client.DataChanged += key => pushes++;
        server.Value = 5;
        for (int i = 0; i < 10000; i++) server.Value = 5;
        yield return ConfirmServerWrite(server);
        yield return Until(() => client.Value == 5, "scalar push");
        Assert.AreEqual(1, receipts);
        Assert.AreEqual(1, pushes);
        string scope = server.GetStorageScope(Profile);
        SODataRecord accepted = authority.ReadAsync(scope, "value", Token).Result;
        for (int i = 0; i < 10000; i++) server.Value = 5;
        yield return ConfirmServerWrite(server);
        Assert.AreEqual(accepted.Revision, authority.ReadAsync(scope, "value", Token).Result.Revision);
        Assert.AreEqual(1, receipts);
        Assert.AreEqual(1, pushes);

        // The server can publish the same payload with a new revision outside this SO's setter.
        SODataRecord duplicate = authority.WriteAsync(scope, "value", accepted.Value, accepted.Revision, Token).Result;
        yield return Until(() => replica.ReadAsync(scope, "value", Token).Result?.Revision == duplicate.Revision, "equal scalar revision");
        Assert.AreNotEqual(accepted.Revision, duplicate.Revision);
        Assert.AreEqual(1, pushes, "An equal remote value must not notify gameplay again.");
        Assert.AreEqual(1, receipts);
        server.Value = 6;
        yield return ConfirmServerWrite(server);
        yield return Until(() => client.Value == 6, "change after equal revision");
        Assert.AreEqual(2, receipts);
        Assert.AreEqual(2, pushes);
        Debug.Log("SO equal RealtimeOnline: value=5 repeatsPending=10000 repeatsConfirmed=10000 extraWrites=0 extraChanges=0 equalPayloadNewRevisionExtraChanges=0 nextValue=6 peers=realUDP");
    }

    private static IEnumerator ConfirmServerWrite(SODataController owner)
    {
        Task confirmed = owner.FlushAsync();
        yield return Until(() => confirmed.IsCompleted, "server acknowledgement");
        confirmed.GetAwaiter().GetResult();
    }

    private static IEnumerator ExpectDenied(SODataController replica, Action change)
    {
        Exception error = null;
        Task acknowledgement = null;
        try
        {
            change();
            acknowledgement = replica.FlushAsync();
        }
        catch (Exception failure) { error = failure.GetBaseException(); }
        if (acknowledgement != null)
        {
            yield return Until(() => acknowledgement.IsCompleted, "client write rejection");
            error = acknowledgement.Exception?.GetBaseException();
        }
        Assert.IsInstanceOf<UnauthorizedAccessException>(error);
    }

    private NetworkManager CreatePeer(string name, ushort port, GameObject prefab)
    {
        GameObject go = new GameObject(name);
        go.SetActive(false);
        var manager = go.AddComponent<NetworkManager>();
        var transport = go.AddComponent<UnityTransport>();
        manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
        transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
        go.SetActive(true);
        manager.AddNetworkPrefab(prefab);
        peers.Add(manager);
        return manager;
    }

    private static IEnumerator Until(Func<bool> condition, string operation)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 12;
        while (!condition())
        {
            Assert.Less(Time.realtimeSinceStartupAsDouble, deadline, operation);
            yield return null;
        }
    }

    [UnityTearDown]
    public IEnumerator ClosePeers()
    {
        for (int i = peers.Count - 1; i >= 0; i--)
        {
            NetworkManager manager = peers[i];
            if (!manager) continue;
            manager.Shutdown();
            yield return Until(() => !manager.IsListening && !manager.ShutdownInProgress, "peer shutdown");
            Object.Destroy(manager.gameObject);
            yield return null;
        }
        peers.Clear();
        if (Application.isPlaying) yield return new ExitPlayMode();
        string path = SessionState.GetString(PathSlot, "");
        if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
        SessionState.EraseString(PathSlot);
    }
}
