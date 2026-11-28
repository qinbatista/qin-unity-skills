using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class UnityFoundationTests
{
    [UnityTest]
    public IEnumerator PickupWalkthroughMenuReportsTheActualWriteAndReopen()
    {
        List<string> trace = new List<string>();
        void Capture(string message, string stack, LogType type)
        {
            if (message.StartsWith("[SO pickup] ", StringComparison.Ordinal)) trace.Add(message);
        }

        Application.logMessageReceived += Capture;
        try
        {
            Assert.IsTrue(EditorApplication.ExecuteMenuItem("Tools/SODataController/Run Pickup Walkthrough"));
            double deadline = EditorApplication.timeSinceStartup + 10;
            while (trace.Count < 7 && EditorApplication.timeSinceStartup < deadline) yield return null;
            CollectionAssert.AreEqual(new[]
            {
                "[SO pickup] 1. Opened DirectAccessOnline, value=37",
                "[SO pickup] 2. Calling PickupController.OnPickup(), which assigns _pickupData.value = 5",
                "[SO pickup] 3. Channel received scope=demo/SOPickupData, key=value, payload={\"value\":5}, revision=1",
                "[SO pickup] 4. Getter reads 5, confirmations=0",
                "[SO pickup] 5. Assigned 5 again, writes=1, close pending=True",
                "[SO pickup] 6. DataChanged(value), confirmed value=5",
                "[SO pickup] 7. Fresh SO opened the same slot, value=5"
            }, trace);
        }
        finally { Application.logMessageReceived -= Capture; }
    }

    private static T Domain<T>(SODataMode mode, string id) where T : SODataController
    {
        var data = ScriptableObject.CreateInstance<T>();
        JsonUtility.FromJsonOverwrite("{\"_storageMode\":" + (int)mode + ",\"_storageId\":\"" + id + "\"}", data);
        return data;
    }

    [TestCase(SODataMode.None)]
    [TestCase(SODataMode.LocalFile)]
    [TestCase(SODataMode.DirectAccess)]
    public async Task IdenticalScalarAssignmentsDoNotCommitOrNotify(SODataMode mode)
    {
        SOStorageWorkloadData data = Domain<SOStorageWorkloadData>(mode, "equal-value");
        try
        {
            await data.OpenAsync("slot");
            int changes = 0;
            int snapshots = 0;
            data.DataChanged += key => { if (key == "value") changes++; };
            data.SnapshotCommitted += json => snapshots++;
            data.Value = 5;
            await data.FlushAsync();
            Assert.AreEqual(1, changes);
            Dictionary<string, byte[]> saved = Directory.Exists(_root)
                ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes)
                : new Dictionary<string, byte[]>();
            int committedSnapshots = snapshots;
            for (int i = 0; i < 10000; i++) data.Value = 5;
            await data.FlushAsync();
            Assert.AreEqual(5, data.Value);
            Assert.AreEqual(1, changes);
            Assert.AreEqual(committedSnapshots, snapshots);
            foreach (KeyValuePair<string, byte[]> file in saved)
                CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(file.Key));
            TestContext.WriteLine($"SO equal {mode}: value=5 repeats=10000 extraChanges={changes - 1} extraSnapshots={snapshots - committedSnapshots}");
        }
        finally { Object.DestroyImmediate(data); }
    }

    [TestCase(SODataMode.None)]
    [TestCase(SODataMode.LocalFile)]
    [TestCase(SODataMode.DirectAccess)]
    public async Task SamePropertyRoutesToOneModeAndAnotherSlotIsIndependent(SODataMode mode)
    {
        var data = Domain<SOOutfitData>(mode, "clothes");
        var cold = Object.Instantiate(data);
        try
        {
            int changed = 0;
            data.DataChanged += key => { if (key == "hatId") changed++; };
            await data.OpenAsync("slot");
            data.HatId = "miner";
            data.BootsId = "red";
            data.HatId = "miner";
            if (mode == SODataMode.LocalFile) Assert.AreEqual(0, changed);
            await data.FlushAsync();
            Assert.AreEqual(1, changed);
            Assert.AreEqual("miner", data.HatId);
            await cold.OpenAsync("slot");
            Assert.AreEqual(mode == SODataMode.None ? "" : "miner", cold.HatId);
            Assert.AreEqual(mode == SODataMode.None ? "" : "red", cold.BootsId);
            if (mode == SODataMode.None) Assert.IsFalse(Directory.Exists(_root));
            cold.CloseData();
            await cold.OpenAsync("slot-b");
            Assert.AreEqual("", cold.HatId);
        }
        finally { Object.DestroyImmediate(cold); Object.DestroyImmediate(data); }
    }

    [Test]
    public async Task DirectPropertyUpdatesOnlyOneOldKeyAndRejectsAStaleWriter()
    {
        var first = Domain<SOOutfitData>(SODataMode.DirectAccess, "clothes");
        var stale = Object.Instantiate(first);
        try
        {
            string dir = Path.Combine(_root, "SOData", "keys");
            string old = Path.Combine(dir, SOFileEnvelope.Revision(Encoding.UTF8.GetBytes("default/clothes\0hatId")) + ".record");
            SOFileStorage.Commit(old, "{\"value\":\"old\"}", null, true, false, CancellationToken.None);
            await first.OpenAsync("default");
            await stale.OpenAsync("default");
            Assert.AreEqual("old", first.HatId);
            Assert.AreEqual("old", stale.HatId);
            first.BootsId = "2026-11-27T12:00:00Z";
            await first.FlushAsync();
            var before = Directory.GetFiles(dir, "*.record").ToDictionary(path => path, File.ReadAllBytes);
            first.HatId = "miner";
            await first.FlushAsync();
            Assert.AreEqual(1, before.Count(pair => !pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key))));
            stale.HatId = "stale";
            try { await stale.FlushAsync(); Assert.Fail("A stale writer must be rejected."); }
            catch (SODataConflictException) { }
            Assert.AreEqual("miner", stale.HatId);
            stale.CloseData(discardLocalChanges: true);
            await stale.OpenAsync("default");
            Assert.AreEqual("miner", stale.HatId);
            Assert.AreEqual("2026-11-27T12:00:00Z", stale.BootsId);
        }
        finally { Object.DestroyImmediate(first); Object.DestroyImmediate(stale); }
    }

    [Test]
    public async Task FailedLocalSnapshotPreservesTheFileAndPendingValueUntilReload()
    {
        var first = Domain<SOOutfitData>(SODataMode.LocalFile, "clothes");
        var stale = Object.Instantiate(first);
        try
        {
            first.HatId = "miner";
            await first.FlushAsync();
            await stale.OpenAsync("default");
            Assert.AreEqual("miner", stale.HatId);
            first.BootsId = "work";
            await first.FlushAsync();
            stale.HatId = "racing";
            Exception failed = null;
            stale.StorageFailed += error => failed = error;
            try { await stale.CloseAsync(); Assert.Fail("A stale slot closed without reporting the conflict."); }
            catch (SODataConflictException error) { Assert.AreSame(error, failed); }
            Assert.AreEqual("racing", stale.HatId);
            stale.CloseData(discardLocalChanges: true);
            await stale.OpenAsync("default");
            Assert.AreEqual("miner", stale.HatId);
            Assert.AreEqual("work", stale.BootsId);
        }
        finally { Object.DestroyImmediate(first); Object.DestroyImmediate(stale); }
    }

    [Test]
    public async Task StructArrayAndListPropertiesRestoreThroughTheSameKeyAndDoNotEcho()
    {
        var first = ScriptableObject.CreateInstance<NativeWalletDomain>();
        var next = ScriptableObject.CreateInstance<NativeWalletDomain>();
        try
        {
            await first.OpenAsync("default");
            first.money += 3;
            first.items = new List<NativeWalletDomain.Item> { new NativeWalletDomain.Item { id = "ore", count = 8 } };
            first.drops = new[] { new NativeWalletDomain.Item { id = "ruby", count = 2 } };
            first.region = new Bounds(Vector3.one, new Vector3(4, 6, 8));
            first.Hat = "miner";
            first.items[0] = new NativeWalletDomain.Item { id = "ore", count = 9 };
            first.items = first.items;
            await first.FlushAsync();
            await next.OpenAsync("default");
            var before = Directory.GetFiles(Path.Combine(_root, "SOData", "keys"), "*.record").ToDictionary(path => path, File.ReadAllBytes);
            for (int i = 0; i < 100; i++) first.money = 40;
            Assert.AreEqual(5, before.Count);
            Assert.AreEqual(40, next.money);
            Assert.AreEqual("ore", next.items[0].id);
            Assert.AreEqual(9, next.items[0].count);
            Assert.AreEqual(2, next.drops[0].count);
            Assert.AreEqual(8f, next.region.size.z);
            Assert.AreEqual("miner", next.Hat);
            Assert.IsTrue(before.All(pair => pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key))));
        }
        finally { Object.DestroyImmediate(first); Object.DestroyImmediate(next); }
    }

    [Test]
    public void OnlinePropertyStagesAValueBeforeBindingAndHasARealSetter()
    {
        var data = ScriptableObject.CreateInstance<SOPickupData>();
        try
        {
            Assert.IsNotNull(typeof(SOPickupData).GetProperty("value").GetSetMethod());
            Assert.IsNull(typeof(SOPickupData).GetField("value"));
            Assert.AreEqual(0, data.value);
            data.value = 5;
            Assert.AreEqual(5, data.value);
            Assert.IsFalse(data.IsReady);
            data.CloseData(discardLocalChanges: true);
        }
        finally { Object.DestroyImmediate(data); }
    }
}
