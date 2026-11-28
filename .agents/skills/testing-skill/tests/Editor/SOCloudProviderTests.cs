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
        public Task Acknowledgement = Task.CompletedTask;
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

        public async Task<Dictionary<string, string>> SaveAsync(IDictionary<string, SaveItem> items, Player.SaveOptions options)
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
            await Acknowledgement;
            return revisions;
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

    [UnityTest]
    public IEnumerator EqualCloudScalarDoesNotResubmitOrNotify() => RunCloudCase(VerifyEqualCloudScalarAsync());

    private static async Task VerifyEqualCloudScalarAsync()
    {
        CloudPlayerFixture account = new CloudPlayerFixture();
        UnityCloudSaveKeyStore channel = new UnityCloudSaveKeyStore(account, () => "alice");
        SOPickupData data = Domain<SOPickupData>(SODataMode.DirectAccessOnline, "equal-value");
        TaskCompletionSource<bool> release = new TaskCompletionSource<bool>();
        try
        {
            string scope = data.GetStorageScope("slot");
            await channel.WriteAsync(scope, "value", "{\"value\":0}", null, CancellationToken.None);
            await data.OpenAsync("slot", channel);
            int changes = 0;
            data.DataChanged += key => changes++;
            int before = account.Batches.Count;
            account.Acknowledgement = release.Task;
            data.value = 5;
            for (int i = 0; i < 10000; i++) data.value = 5;
            Assert.AreEqual(0, changes);
            Assert.AreEqual(before + 1, account.Batches.Count);
            release.SetResult(true);
            await data.FlushAsync();
            Assert.AreEqual(1, changes);
            for (int i = 0; i < 10000; i++) data.value = 5;
            await data.FlushAsync();
            Assert.AreEqual(1, changes);
            Assert.AreEqual(before + 1, account.Batches.Count);
            TestContext.WriteLine("SO equal DirectAccessOnline: value=5 repeatsPending=10000 repeatsConfirmed=10000 writes=1 confirmations=1 extraWrites=0 extraChanges=0 provider=controlledSDK");
        }
        finally { release.TrySetResult(true); UnityEngine.Object.DestroyImmediate(data); }
    }

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
    public IEnumerator PickupPropertySendsOneCloudWriteAndColdReadsFive() => RunCloudCase(VerifyPickupProfileAsync());

    private static async Task VerifyPickupProfileAsync()
    {
        CloudPlayerFixture account = new CloudPlayerFixture();
        UnityCloudSaveKeyStore cloud = new UnityCloudSaveKeyStore(account, () => "alice");
        SOPickupData data = Domain<SOPickupData>(SODataMode.DirectAccessOnline, "pickups");
        SOPickupData cold = UnityEngine.Object.Instantiate(data);
        GameObject pickupObject = new GameObject("Pickup");
        TaskCompletionSource<bool> reply = new TaskCompletionSource<bool>();
        string assetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Fixtures/Pickups.asset");
        try
        {
            AssetDatabase.CreateAsset(data, assetPath);
            AssetDatabase.SaveAssetIfDirty(data);
            byte[] authored = File.ReadAllBytes(assetPath);
            PickupController pickup = pickupObject.AddComponent<PickupController>();
            SerializedObject binding = new SerializedObject(pickup);
            binding.FindProperty("_pickupData").objectReferenceValue = data;
            binding.ApplyModifiedPropertiesWithoutUndo();
            const string scope = "slot-a/pickups";
            await cloud.WriteAsync(scope, "value", "{\"value\":37}", null, CancellationToken.None);
            await data.OpenAsync("slot-a", cloud);
            Assert.AreEqual(37, data.value);

            int writesBeforePickup = account.Batches.Count;
            List<string> confirmed = new List<string>();
            data.DataChanged += confirmed.Add;
            account.Acknowledgement = reply.Task;
            pickup.OnPickup();
            Assert.AreEqual(5, data.value);
            pickup.OnPickup(); // Reassigning the same value leaves the existing receipt in flight.
            Assert.AreEqual(writesBeforePickup + 1, account.Batches.Count);
            CollectionAssert.AreEqual(new[] { UnityCloudSaveKeyStore.GetCloudKey(scope, "value") }, account.Batches.Last());
            Assert.AreEqual("{\"value\":5}", (await cloud.ReadAsync(scope, "value", CancellationToken.None)).Value);
            Assert.IsEmpty(confirmed);

            Task exit = data.CloseAsync();
            Assert.IsFalse(exit.IsCompleted);
            Assert.Throws<InvalidOperationException>(() => data.value = 9);
            Assert.AreEqual(5, data.value);
            reply.SetResult(true);
            await exit;
            CollectionAssert.AreEqual(new[] { "value" }, confirmed);
            Assert.Throws<InvalidOperationException>(() => { var closed = data.value; });
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            CollectionAssert.AreEqual(authored, File.ReadAllBytes(assetPath));
            await cold.OpenAsync("slot-a", new UnityCloudSaveKeyStore(account, () => "alice"));
            Assert.AreEqual(5, cold.value);
            await cold.CloseAsync();
            await cold.OpenAsync("slot-b", cloud);
            Assert.AreEqual(0, cold.value);
            await cold.CloseAsync();
            Assert.AreEqual(writesBeforePickup + 1, account.Batches.Count);
        }
        finally
        {
            reply.TrySetResult(true);
            UnityEngine.Object.DestroyImmediate(pickupObject);
            if (AssetDatabase.Contains(data)) AssetDatabase.DeleteAsset(assetPath);
            else UnityEngine.Object.DestroyImmediate(data);
            UnityEngine.Object.DestroyImmediate(cold);
        }
    }

    [UnityTest]
    public IEnumerator OriginalAuthoredFieldOpensThroughItsProperty() => RunCloudCase(VerifyOriginalFieldAsync());

    private static async Task VerifyOriginalFieldAsync()
    {
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Fixtures/OriginalPickup.asset");
        var data = ScriptableObject.CreateInstance<SOPickupData>();
        var account = new CloudPlayerFixture();
        try
        {
            AssetDatabase.CreateAsset(data, path);
            var serialized = new SerializedObject(data);
            serialized.FindProperty("_value").intValue = 37;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(data);
            string text = File.ReadAllText(path);
            StringAssert.Contains("  _value: 37", text);
            File.WriteAllText(path, text.Replace("  _value: 37", "  value: 37"));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            data = AssetDatabase.LoadAssetAtPath<SOPickupData>(path);
            await data.OpenAsync("original", new UnityCloudSaveKeyStore(account, () => "alice"));
            Assert.AreEqual(37, data.value);
            Assert.AreEqual(0, account.Batches.Count);
            await data.CloseAsync();
        }
        finally { AssetDatabase.DeleteAsset(path); }
    }

    [UnityTest]
    public IEnumerator TwoAssignmentsKeepTheirOrderUntilTheSlotCloses() => RunCloudCase(VerifyPendingWritesAsync());

    [UnityTest]
    public IEnumerator CloudKeyBurstWaitsForReceiptsAndSkipsUnchangedValues() => RunCloudCase(VerifyCloudBurstAsync());

    private static async Task VerifyCloudBurstAsync()
    {
        CloudPlayerFixture account = new CloudPlayerFixture();
        UnityCloudSaveKeyStore channel = new UnityCloudSaveKeyStore(account, () => "fixture-player");
        var data = Domain<SOStorageWorkloadData>(SODataMode.DirectAccessOnline, "workload");
        TaskCompletionSource<bool> firstRead = new TaskCompletionSource<bool>();
        TaskCompletionSource<bool> firstWrite = new TaskCompletionSource<bool>();
        string scope = data.GetStorageScope("slot");
        try
        {
            await channel.WriteAsync(scope, "value", "{\"value\":0}", null, CancellationToken.None);
            List<string> reads = new List<string>();
            account.AfterRead = key =>
            {
                reads.Add(key);
                return firstRead.Task;
            };
            Task opening = data.OpenAsync("slot", channel);
            Assert.IsFalse(opening.IsCompleted);
            int readsBeforeReply = reads.Count;
            firstRead.SetResult(true);
            await opening;
            CollectionAssert.AreEquivalent(new[] { UnityCloudSaveKeyStore.GetCloudKey(scope, "value"), UnityCloudSaveKeyStore.GetCloudKey(scope, "padding") }, reads);
            account.AfterRead = null;

            int start = account.Batches.Count;
            account.Acknowledgement = firstWrite.Task;
            for (int value = 1; value <= 128; value++) data.Value = value;
            Task pending = data.FlushAsync();
            Assert.IsFalse(pending.IsCompleted);
            Assert.AreEqual(128, data.Value);
            int submitted = account.Batches.Count - start;
            Assert.AreEqual(1, submitted);
            firstWrite.SetResult(true);
            await pending;
            int writes = account.Batches.Count - start;
            SODataRecord latest = await channel.ReadAsync(scope, "value", CancellationToken.None);
            for (int i = 0; i < 10000; i++) data.Value = 128;
            await data.FlushAsync();
            Assert.AreEqual(start + writes, account.Batches.Count);
            Assert.AreEqual(latest.Revision, (await channel.ReadAsync(scope, "value", CancellationToken.None)).Revision);
            Assert.AreEqual("{\"value\":128}", latest.Value);
            await data.CloseAsync();
            await data.OpenAsync("slot", channel);
            Assert.AreEqual(128, data.Value);
            TestContext.WriteLine($"SO cost DirectAccessOnline: openKeys={reads.Count} readsBeforeFirstReply={readsBeforeReply} changedAssignments=128 submittedBeforeFirstReply={submitted} totalSdkSaves={writes} cleanAssignments=10000 cleanSaves=0 reopened={data.Value}");
        }
        finally
        {
            firstRead.TrySetResult(true);
            firstWrite.TrySetResult(true);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static async Task VerifyPendingWritesAsync()
    {
        var account = new CloudPlayerFixture();
        var channel = new UnityCloudSaveKeyStore(account, () => "alice");
        var data = Domain<SOPickupData>(SODataMode.DirectAccessOnline, "pickups");
        var release = new TaskCompletionSource<bool>();
        const string scope = "queue/pickups";
        try
        {
            await channel.WriteAsync(scope, "value", "{\"value\":1}", null, CancellationToken.None);
            await data.OpenAsync("queue", channel);
            var confirmed = new List<int>();
            data.DataChanged += key => confirmed.Add(data.value);
            int before = account.Batches.Count;
            account.Acknowledgement = release.Task;
            data.value = 5;
            data.value = 8;
            Assert.AreEqual(8, data.value);
            Assert.AreEqual(before + 1, account.Batches.Count);
            Task close = data.CloseAsync();
            Assert.IsFalse(close.IsCompleted);
            release.SetResult(true);
            await close;
            Assert.AreEqual(before + 2, account.Batches.Count);
            CollectionAssert.AreEqual(new[] { 8 }, confirmed);
            await data.OpenAsync("queue", channel);
            Assert.AreEqual(8, data.value);
        }
        finally { release.TrySetResult(true); UnityEngine.Object.DestroyImmediate(data); }
    }

    [UnityTest]
    public IEnumerator AccountCurrencyCreditsTheOriginalCloudKeyAndRestoresOnlyTheConfirmedBalance() => RunCloudCase(VerifyCloudCurrencyAsync());

    private async Task VerifyCloudCurrencyAsync()
    {
        CloudPlayerFixture player = new CloudPlayerFixture();
        UnityCloudSaveKeyStore firstDevice = new UnityCloudSaveKeyStore(player, () => "alice");
        UnityCloudSaveKeyStore secondDevice = new UnityCloudSaveKeyStore(player, () => "alice");
        SOCurrencyData first = Domain<SOCurrencyData>(SODataMode.DirectAccess, "wallet");
        SOCurrencyData cold = UnityEngine.Object.Instantiate(first);
        const string scope = "profile%2F1/wallet";
        CancellationToken token = CancellationToken.None;
        try
        {
            await first.OpenAsync("profile/1", firstDevice);
            Assert.AreEqual(0, first.Coins);
            Assert.IsNull(await firstDevice.ReadAsync(scope, "coins", token));
            first.CloseData();

            await firstDevice.WriteAsync(scope, "coins", "{\"value\":37}", null, token);
            await first.OpenAsync("profile/1", firstDevice);
            Assert.AreEqual(37, first.Coins);
            first.Coins += 3;
            await first.FlushAsync();
            CollectionAssert.AreEqual(new[] { UnityCloudSaveKeyStore.GetCloudKey(scope, "coins") }, player.Batches.Last());
            Assert.AreEqual("{\"value\":40}", (await secondDevice.ReadAsync(scope, "coins", token)).Value);
            int writes = player.Batches.Count;
            Assert.Throws<OverflowException>(() => first.Coins = checked(first.Coins + int.MaxValue));
            Assert.AreEqual(writes, player.Batches.Count);

            await cold.OpenAsync("profile/1", secondDevice);
            Assert.AreEqual(40, cold.Coins);
            first.Coins += 2;
            await first.FlushAsync();
            Assert.Throws<SODataConflictException>(() => cold.Coins += 10);
            Assert.AreEqual(40, cold.Coins);
            cold.CloseData();
            await cold.OpenAsync("profile/1", secondDevice);
            Assert.AreEqual(42, cold.Coins);
            cold.Coins += 1;
            await cold.FlushAsync();
            Assert.AreEqual(43, cold.Coins);

            player.DropAcknowledgementFor = UnityCloudSaveKeyStore.GetCloudKey(scope, "coins");
            Assert.Throws<IOException>(() => cold.Coins += 1);
            Assert.AreEqual(43, cold.Coins);
            cold.CloseData();
            player.DropAcknowledgementFor = null;
            await cold.OpenAsync("profile/1", secondDevice);
            Assert.AreEqual(44, cold.Coins);
            cold.CloseData();
            await cold.OpenAsync("profile%2F1", secondDevice);
            Assert.AreEqual(0, cold.Coins);
        }
        finally { UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(cold); }
    }

    [UnityTest]
    public IEnumerator CloudSaveOutfitCloneRestoresThroughTheGameplayEntry() => RunCloudCase(VerifyCloudOutfitAsync());

    private static async Task VerifyCloudOutfitAsync()
    {
        CloudPlayerFixture player = new CloudPlayerFixture();
        SOOutfitData asset = ScriptableObject.CreateInstance<SOOutfitData>();
        SOOutfitData runtime = null;
        try
        {
            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("_storageMode").enumValueIndex = (int)SODataMode.DirectAccessOnline;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            runtime = UnityEngine.Object.Instantiate(asset);
            await runtime.OpenAsync("slot-a", new UnityCloudSaveKeyStore(player, () => "alice"));
            runtime.HatId = "straw";
            runtime.HatId = "felt";
            await runtime.FlushAsync();
            Assert.AreEqual("felt", runtime.HatId);
            Assert.Throws<InvalidOperationException>(() => { var unopened = asset.HatId; });
            CollectionAssert.AreEqual(new[] { UnityCloudSaveKeyStore.GetCloudKey(runtime.GetStorageScope("slot-a"), "hatId") }, player.Batches.Last());
            runtime.CloseData();
            UnityEngine.Object.DestroyImmediate(runtime);

            runtime = UnityEngine.Object.Instantiate(asset);
            await runtime.OpenAsync("slot-a", new UnityCloudSaveKeyStore(player, () => "alice"));
            Assert.AreEqual("felt", runtime.HatId);
            runtime.CloseData();
            await runtime.OpenAsync("slot-b", new UnityCloudSaveKeyStore(player, () => "alice"));
            Assert.AreEqual("", runtime.HatId);
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
