#if SO_DATA_UNITY_CLOUD_SAVE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Internal;
using Unity.Services.CloudSave.Models;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Player = Unity.Services.CloudSave.Models.Data.Player;

public sealed partial class UnityFoundationTests
{
    // Model the documented atomic Default Player Data batch, using the pinned SDK's request and response types.
    private sealed class CloudPlayerFixture : IPlayerDataService
    {
        private readonly Dictionary<string, Item> _items = new Dictionary<string, Item>();
        public readonly List<string[]> Batches = new List<string[]>();
        public Func<string, Task> AfterRead;
        public string DropAcknowledgementFor;

        public async Task<Dictionary<string, Item>> LoadAsync(ISet<string> keys, Player.LoadOptions options)
        {
            Assert.IsInstanceOf<Player.DefaultReadAccessClassOptions>(options.AccessClassOptions);
            string key = keys.Single();
            Dictionary<string, Item> result = new Dictionary<string, Item>();
            lock (_items)
            {
                if (_items.TryGetValue(key, out Item item)) result.Add(key, item);
            }
            if (AfterRead != null) await AfterRead(key);
            return result;
        }

        public Task<Dictionary<string, string>> SaveAsync(IDictionary<string, SaveItem> items, Player.SaveOptions options)
        {
            Assert.IsInstanceOf<Player.DefaultWriteAccessClassOptions>(options.AccessClassOptions);
            Dictionary<string, string> revisions = new Dictionary<string, string>();
            lock (_items)
            {
                foreach (KeyValuePair<string, SaveItem> pair in items)
                {
                    if (pair.Value.WriteLock == null) continue;
                    if (!_items.TryGetValue(pair.Key, out Item item) || item.WriteLock != pair.Value.WriteLock) throw SdkConflict();
                }

                foreach (KeyValuePair<string, SaveItem> pair in items)
                {
                    string revision = Guid.NewGuid().ToString("N");
                    _items[pair.Key] = SdkItem(pair.Key, (string)pair.Value.Value, revision);
                    revisions.Add(pair.Key, revision);
                }
                Batches.Add(items.Keys.ToArray());
            }
            if (DropAcknowledgementFor != null && items.ContainsKey(DropAcknowledgementFor)) throw new IOException("The server committed, then the connection closed.");
            return Task.FromResult(revisions);
        }

        // These SDK result and exception constructors are private to the package.
        private static Item SdkItem(string key, string value, string revision)
        {
            Type model = typeof(Item).Assembly.GetType("Unity.Services.CloudSave.Internal.Models.Item", true);
            object response = Activator.CreateInstance(model, key, value, revision, null, null);
            return (Item)Activator.CreateInstance(typeof(Item), BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { response }, null);
        }

        private static CloudSaveException SdkConflict()
        {
            object[] arguments = { CloudSaveExceptionReason.Conflict, 409, "Fixture write-lock conflict", null };
            return (CloudSaveException)Activator.CreateInstance(typeof(CloudSaveException), BindingFlags.NonPublic | BindingFlags.Instance, null, arguments, null);
        }

        // Any extra call, including a force-save overload, fails this fixture.
        public Task<List<ItemKey>> ListAllKeysAsync() => throw new NotSupportedException();
        public Task<List<ItemKey>> ListAllKeysAsync(Player.ListAllKeysOptions options) => throw new NotSupportedException();
        public Task<Dictionary<string, Item>> LoadAllAsync() => throw new NotSupportedException();
        public Task<Dictionary<string, Item>> LoadAllAsync(Player.LoadAllOptions options) => throw new NotSupportedException();
        public Task<Dictionary<string, Item>> LoadAsync(ISet<string> keys) => throw new NotSupportedException();
        public Task<Dictionary<string, string>> SaveAsync(IDictionary<string, SaveItem> items) => throw new NotSupportedException();
        public Task<Dictionary<string, string>> SaveAsync(IDictionary<string, object> items) => throw new NotSupportedException();
        public Task<Dictionary<string, string>> SaveAsync(IDictionary<string, object> items, Player.SaveOptions options) => throw new NotSupportedException();
        public Task DeleteAllAsync() => throw new NotSupportedException();
        public Task DeleteAllAsync(Player.DeleteAllOptions options) => throw new NotSupportedException();
#pragma warning disable 0618
        public Task DeleteAsync(string key, Unity.Services.CloudSave.DeleteOptions options = null) => throw new NotSupportedException();
#pragma warning restore 0618
        public Task DeleteAsync(string key, Player.DeleteOptions options) => throw new NotSupportedException();
        public Task<List<EntityData>> QueryAsync(Query query, Player.QueryOptions options) => throw new NotSupportedException();
    }

    [UnityTest]
    public IEnumerator CloudSaveChecksBothFirstWriterRacesAndUpdatesOnlyTheChangedKey() => RunCloudCase(VerifyCloudCreationAsync());

    private static async Task VerifyCloudCreationAsync()
    {
        CloudPlayerFixture player = new CloudPlayerFixture();
        UnityCloudSaveKeyStore first = new UnityCloudSaveKeyStore(player, () => "alice");
        UnityCloudSaveKeyStore second = new UnityCloudSaveKeyStore(player, () => "alice");
        CancellationToken token = CancellationToken.None;
        string coins = UnityCloudSaveKeyStore.GetCloudKey("wallet", "coins");
        Assert.AreNotEqual(coins, UnityCloudSaveKeyStore.GetCloudKey("wall", "etcoins"));
        Assert.IsNull(await first.ReadAsync("wallet", "coins", token));

        // Both devices see a missing fence, then both see a missing field before either inserts it.
        Dictionary<string, TaskCompletionSource<bool>> barriers = new Dictionary<string, TaskCompletionSource<bool>>();
        player.AfterRead = key =>
        {
            lock (barriers)
            {
                if (barriers.TryGetValue(key, out TaskCompletionSource<bool> waiting))
                {
                    waiting.TrySetResult(true);
                    return Task.CompletedTask;
                }
                TaskCompletionSource<bool> barrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                barriers.Add(key, barrier);
                return barrier.Task;
            }
        };
        Task<SODataRecord> a = first.WriteAsync("wallet", "coins", "37", null, token);
        Task<SODataRecord> b = second.WriteAsync("wallet", "coins", "40", null, token);
        Task both = Task.WhenAll(a, b);
        Assert.AreSame(both, await Task.WhenAny(both, Task.Delay(TimeSpan.FromSeconds(8))), "Both simulated devices must finish.");
        try { await both; }
        catch (SODataConflictException) { }
        Assert.AreNotEqual(a.IsFaulted, b.IsFaulted, "Exactly one of the competing first writes must win.");
        Assert.IsInstanceOf<SODataConflictException>((a.IsFaulted ? a : b).Exception.GetBaseException());
        Assert.AreEqual(2, barriers.Count);
        player.AfterRead = null;

        SODataRecord winner = await (a.IsFaulted ? b : a);
        Assert.AreEqual(winner.Value, (await first.ReadAsync("wallet", "coins", token)).Value);
        SODataRecord updated = await second.WriteAsync("wallet", "coins", "42", winner.Revision, token);
        CollectionAssert.AreEqual(new[] { coins }, player.Batches.Last());
        Assert.AreNotEqual(winner.Revision, updated.Revision);
        await ExpectCloudFailure<SODataConflictException>(first.WriteAsync("wallet", "coins", "99", winner.Revision, token));
        await ExpectCloudFailure<SODataConflictException>(first.WriteAsync("wallet", "coins", "99", null, token));
        Assert.AreEqual("42", (await first.ReadAsync("wallet", "coins", token)).Value);
    }

    [UnityTest]
    public IEnumerator CloudSaveCurrencyCloneRestoresThroughTheGameplayEntry() => RunCloudCase(VerifyCloudCurrencyAsync());

    private static async Task VerifyCloudCurrencyAsync()
    {
        CloudPlayerFixture player = new CloudPlayerFixture();
        SOCurrencyData asset = ScriptableObject.CreateInstance<SOCurrencyData>();
        SOCurrencyData runtime = null;
        try
        {
            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("_storageMode").enumValueIndex = (int)SODataMode.DirectAccessOnline;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            runtime = UnityEngine.Object.Instantiate(asset);
            await runtime.OpenAsync("slot-a", new UnityCloudSaveKeyStore(player, () => "alice"));
            await runtime.CreditCoinsAsync(37);
            await runtime.CreditCoinsAsync(3);
            Assert.AreEqual(40, runtime.Coins);
            Assert.AreEqual(0, asset.Coins);
            CollectionAssert.AreEqual(new[] { UnityCloudSaveKeyStore.GetCloudKey(runtime.GetStorageScope("slot-a"), "coins") }, player.Batches.Last());
            runtime.CloseData();
            UnityEngine.Object.DestroyImmediate(runtime);

            runtime = UnityEngine.Object.Instantiate(asset);
            await runtime.OpenAsync("slot-a", new UnityCloudSaveKeyStore(player, () => "alice"));
            Assert.AreEqual(40, runtime.Coins);
            runtime.CloseData();
            await runtime.OpenAsync("slot-b", new UnityCloudSaveKeyStore(player, () => "alice"));
            Assert.AreEqual(0, runtime.Coins);
        }
        finally
        {
            if (runtime != null) UnityEngine.Object.DestroyImmediate(runtime);
            UnityEngine.Object.DestroyImmediate(asset);
        }
    }

    [UnityTest]
    public IEnumerator CloudSaveRejectsSessionChangesAndReconcilesAnUnacknowledgedWrite() => RunCloudCase(VerifyCloudSessionAsync());

    private static async Task VerifyCloudSessionAsync()
    {
        CloudPlayerFixture player = new CloudPlayerFixture();
        string account = null;
        Assert.Throws<UnauthorizedAccessException>(() => new UnityCloudSaveKeyStore(player, () => account));
        account = "alice";
        UnityCloudSaveKeyStore store = new UnityCloudSaveKeyStore(player, () => account);
        CancellationToken token = CancellationToken.None;
        SODataRecord initial = await store.WriteAsync("wallet", "coins", "37", null, token);
        int accepted = player.Batches.Count;
        await ExpectCloudFailure<OperationCanceledException>(store.WriteAsync("wallet", "coins", "38", initial.Revision, new CancellationToken(true)));
        Assert.AreEqual(accepted, player.Batches.Count);
        player.AfterRead = key => { account = "bob"; return Task.CompletedTask; };
        await ExpectCloudFailure<UnauthorizedAccessException>(store.ReadAsync("wallet", "coins", token));
        await ExpectCloudFailure<UnauthorizedAccessException>(store.WriteAsync("wallet", "coins", "39", initial.Revision, token));
        Assert.AreEqual(accepted, player.Batches.Count);

        account = "alice";
        player.AfterRead = null;
        player.DropAcknowledgementFor = UnityCloudSaveKeyStore.GetCloudKey("wallet", "coins");
        await ExpectCloudFailure<IOException>(store.WriteAsync("wallet", "coins", "40", initial.Revision, token));
        player.DropAcknowledgementFor = null;
        UnityCloudSaveKeyStore reopened = new UnityCloudSaveKeyStore(player, () => account);
        SODataRecord recovered = await reopened.ReadAsync("wallet", "coins", token);
        Assert.AreEqual("40", recovered.Value);
        Assert.AreNotEqual(initial.Revision, recovered.Revision);
        await ExpectCloudFailure<SODataConflictException>(reopened.WriteAsync("wallet", "coins", "43", initial.Revision, token));
    }

    private static async Task ExpectCloudFailure<T>(Task operation) where T : Exception
    {
        try { await operation; }
        catch (T) { return; }
        Assert.Fail("Expected " + typeof(T).Name);
    }

    // Let the Editor run the Unity synchronization context while continuations are pending.
    private static IEnumerator RunCloudCase(Task operation)
    {
        double deadline = EditorApplication.timeSinceStartup + 15;
        while (!operation.IsCompleted && EditorApplication.timeSinceStartup < deadline) yield return null;
        Assert.IsTrue(operation.IsCompleted, "Timed out running the cloud provider case.");
        operation.GetAwaiter().GetResult();
    }
}
#endif
