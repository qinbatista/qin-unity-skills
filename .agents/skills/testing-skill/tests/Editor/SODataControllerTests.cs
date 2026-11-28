using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed partial class UnityFoundationTests
{
    private static T Domain<T>(SODataMode mode, string id) where T : SODataController
    {
        var data = ScriptableObject.CreateInstance<T>();
        JsonUtility.FromJsonOverwrite("{\"_storageMode\":" + (int)mode + ",\"_storageId\":\"" + id + "\"}", data);
        return data;
    }

    [TestCase(SODataMode.None)]
    [TestCase(SODataMode.LocalFile)]
    [TestCase(SODataMode.DirectAccess)]
    public void SamePropertyRoutesToOneModeAndAnotherSlotIsIndependent(SODataMode mode)
    {
        var data = Domain<SOOutfitData>(mode, "clothes");
        var cold = Object.Instantiate(data);
        try
        {
            int changed = 0;
            data.DataChanged += key => { if (key == "hatId") changed++; };
            data.OpenAsync("slot").GetAwaiter().GetResult();
            data.HatId = "miner";
            data.BootsId = "red";
            data.HatId = "miner";
            Assert.AreEqual(1, changed);
            Assert.AreEqual("miner", data.HatId);
            cold.OpenAsync("slot").GetAwaiter().GetResult();
            Assert.AreEqual(mode == SODataMode.None ? "" : "miner", cold.HatId);
            Assert.AreEqual(mode == SODataMode.None ? "" : "red", cold.BootsId);
            if (mode == SODataMode.None) Assert.IsFalse(Directory.Exists(_root));
            cold.CloseData();
            cold.OpenAsync("slot-b").GetAwaiter().GetResult();
            Assert.AreEqual("", cold.HatId);
        }
        finally { Object.DestroyImmediate(cold); Object.DestroyImmediate(data); }
    }

    [Test]
    public void DirectPropertyUpdatesOnlyOneOldKeyAndRejectsAStaleWriter()
    {
        var first = Domain<SOOutfitData>(SODataMode.DirectAccess, "clothes");
        var stale = Object.Instantiate(first);
        try
        {
            string dir = Path.Combine(_root, "SOData", "keys");
            string old = Path.Combine(dir, SOFileEnvelope.Revision(Encoding.UTF8.GetBytes("default/clothes\0hatId")) + ".record");
            SOFileStorage.Commit(old, "{\"value\":\"old\"}", null, true, false, CancellationToken.None);
            Assert.AreEqual("old", first.HatId);
            Assert.AreEqual("old", stale.HatId);
            first.BootsId = "2026-11-27T12:00:00Z";
            var before = Directory.GetFiles(dir, "*.record").ToDictionary(path => path, File.ReadAllBytes);
            first.HatId = "miner";
            Assert.AreEqual(1, before.Count(pair => !pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key))));
            Assert.Throws<SODataConflictException>(() => stale.HatId = "stale");
            Assert.AreEqual("old", stale.HatId);
            stale.CloseData();
            Assert.AreEqual("miner", stale.HatId);
            Assert.AreEqual("2026-11-27T12:00:00Z", stale.BootsId);
        }
        finally { Object.DestroyImmediate(first); Object.DestroyImmediate(stale); }
    }

    [Test]
    public void FailedLocalSnapshotKeepsTheLastFileAndBackingField()
    {
        var first = Domain<SOOutfitData>(SODataMode.LocalFile, "clothes");
        var stale = Object.Instantiate(first);
        try
        {
            first.HatId = "miner";
            Assert.AreEqual("miner", stale.HatId);
            first.BootsId = "work";
            Assert.Throws<SODataConflictException>(() => stale.HatId = "racing");
            Assert.AreEqual("miner", stale.HatId);
            stale.CloseData();
            Assert.AreEqual("work", stale.BootsId);
        }
        finally { Object.DestroyImmediate(first); Object.DestroyImmediate(stale); }
    }

    [Test]
    public void StructArrayAndListPropertiesRestoreThroughTheSameKeyAndDoNotEcho()
    {
        var first = ScriptableObject.CreateInstance<NativeWalletDomain>();
        var next = ScriptableObject.CreateInstance<NativeWalletDomain>();
        try
        {
            first.money += 3;
            first.items = new List<NativeWalletDomain.Item> { new NativeWalletDomain.Item { id = "ore", count = 8 } };
            first.drops = new[] { new NativeWalletDomain.Item { id = "ruby", count = 2 } };
            first.region = new Bounds(Vector3.one, new Vector3(4, 6, 8));
            first.Hat = "miner";
            var before = Directory.GetFiles(Path.Combine(_root, "SOData", "keys"), "*.record").ToDictionary(path => path, File.ReadAllBytes);
            Assert.AreEqual(5, before.Count);
            Assert.AreEqual(40, next.money);
            Assert.AreEqual("ore", next.items[0].id);
            Assert.AreEqual(2, next.drops[0].count);
            Assert.AreEqual(8f, next.region.size.z);
            Assert.AreEqual("miner", next.Hat);
            Assert.IsTrue(before.All(pair => pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key))));
        }
        finally { Object.DestroyImmediate(first); Object.DestroyImmediate(next); }
    }

    [Test]
    public void OnlinePropertyRequiresAnOpenSlotAndHasARealSetter()
    {
        var data = ScriptableObject.CreateInstance<SOPickupData>();
        try
        {
            Assert.IsNotNull(typeof(SOPickupData).GetProperty("value").GetSetMethod());
            Assert.IsNull(typeof(SOPickupData).GetField("value"));
            Assert.Throws<InvalidOperationException>(() => data.value = 5);
        }
        finally { Object.DestroyImmediate(data); }
    }
}
