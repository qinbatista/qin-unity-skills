using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed partial class UnityFoundationTests
{
    [Test]
    public void NoneSharesOneInstanceAndCapturesDefaultsOnceOnOpen()
    {
        var data = Domain<SOStorageWorkloadData>(SODataMode.None, "shared");
        try
        {
            var reader = data;
            string padding = new string('x', 64 * 1024);
            JsonUtility.FromJsonOverwrite("{\"_padding\":\"" + padding + "\"}", data);
            int beforeOpen = data.SerializationPasses;
            Assert.AreEqual(0, reader.Value);
            int afterOpen = data.SerializationPasses;
            Assert.That(afterOpen - beforeOpen, Is.InRange(0, 1), "Opening may capture authored defaults once.");
            int instances = Resources.FindObjectsOfTypeAll<SOStorageWorkloadData>().Length;
            int notifications = 0;
            data.DataChanged += _ => notifications++;
            for (int value = 1; value <= 128; value++) data.Value = value;
            for (int i = 0; i < 10000; i++) data.Value = reader.Value;
            Assert.AreEqual(128, reader.Value);
            Assert.AreEqual(padding, reader.Padding);
            Assert.AreEqual(128, notifications);
            Assert.AreEqual(afterOpen, data.SerializationPasses);
            int newInstances = Resources.FindObjectsOfTypeAll<SOStorageWorkloadData>().Length - instances;
            Assert.AreEqual(0, newInstances);
            Assert.IsFalse(Directory.Exists(_root));
            TestContext.WriteLine($"SO cost None: openFullSerializations={afterOpen - beforeOpen} hotFullSerializations={data.SerializationPasses - afterOpen} changes={notifications} cleanAssignments=10000 newInstances={newInstances} files=0");
            data.CloseData();
            Assert.AreEqual(0, reader.Value);
        }
        finally { Object.DestroyImmediate(data); }
    }

    [Test]
    public void OfflineKeyCommitsBeforeReturnAndAColdOwnerReadsTheLastValue()
    {
        var data = Domain<SOStorageWorkloadData>(SODataMode.DirectAccess, "offline");
        var cold = Domain<SOStorageWorkloadData>(SODataMode.DirectAccess, "offline");
        try
        {
            data.OpenAsync("slot").GetAwaiter().GetResult();
            int receipts = 0;
            data.DataChanged += key => { if (key == "value") receipts++; };
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            for (int value = 1; value <= 128; value++)
            {
                data.Value = value;
                Assert.AreEqual(value, receipts);
            }
            elapsed.Stop();
            string record = Directory.GetFiles(_root, "*.record", SearchOption.AllDirectories).Single();
            byte[] committed = File.ReadAllBytes(record);
            for (int i = 0; i < 10000; i++) data.Value = 128;
            Assert.AreEqual(128, receipts);
            CollectionAssert.AreEqual(committed, File.ReadAllBytes(record));
            data.CloseData();
            cold.OpenAsync("slot").GetAwaiter().GetResult();
            Assert.AreEqual(128, cold.Value);
            TestContext.WriteLine($"SO cost DirectAccess: changes=128 receiptsBeforeReturn={receipts} recordFiles=1 burstMs={elapsed.Elapsed.TotalMilliseconds:F1} cleanAssignments=10000 cleanWrites=0 reopened={cold.Value}");
        }
        finally { Object.DestroyImmediate(cold); Object.DestroyImmediate(data); }
    }

    [Test]
    public async Task OfflineKeyDuringALargeSnapshotRetainsBothValues()
    {
        var snapshot = Domain<SOStorageWorkloadData>(SODataMode.LocalFile, "bulk");
        var key = Domain<SOStorageWorkloadData>(SODataMode.DirectAccess, "key");
        try
        {
            const int size = 2 * 1024 * 1024;
            JsonUtility.FromJsonOverwrite("{\"_padding\":\"" + new string('x', size) + "\"}", snapshot);
            snapshot.OpenAsync("slot").GetAwaiter().GetResult();
            key.OpenAsync("slot").GetAwaiter().GetResult();
            key.Value = 1;
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            key.Value = 2;
            double aloneMs = elapsed.Elapsed.TotalMilliseconds;
            snapshot.Value = 5;
            Task writing = snapshot.FlushAsync();
            string profiles = Path.Combine(_root, "SOData", "profiles");
            Assert.IsTrue(SpinWait.SpinUntil(() => Directory.Exists(profiles)
                && Directory.GetFiles(profiles, "writer.lock", SearchOption.AllDirectories).Length > 0, 5000));
            bool awaitingReplacement = Directory.GetFiles(profiles, nameof(SOStorageWorkloadData), SearchOption.AllDirectories).Length == 0;
            elapsed.Restart();
            key.Value = 3;
            elapsed.Stop();
            await writing;
            key.CloseData();
            snapshot.CloseData();
            key.OpenAsync("slot").GetAwaiter().GetResult();
            snapshot.OpenAsync("slot").GetAwaiter().GetResult();
            Assert.AreEqual(3, key.Value);
            Assert.AreEqual(5, snapshot.Value);
            Assert.AreEqual(size, snapshot.Padding.Length);
            TestContext.WriteLine($"SO cost FileOverlap: snapshotBytesOfPadding={size} beforeReplacement={awaitingReplacement} keyAloneMs={aloneMs:F2} keyDuringCheckpointMs={elapsed.Elapsed.TotalMilliseconds:F2} reopenedKey={key.Value} reopenedSnapshot={snapshot.Value}");
        }
        finally { Object.DestroyImmediate(key); Object.DestroyImmediate(snapshot); }
    }

    [Test]
    public void UnchangedInventoryAndScalarUseDifferentComparisonPaths()
    {
        var data = ScriptableObject.CreateInstance<NativeWalletDomain>();
        try
        {
            List<NativeWalletDomain.Item> items = new List<NativeWalletDomain.Item>();
            for (int i = 0; i < 1024; i++)
                items.Add(new NativeWalletDomain.Item { id = "item-" + i, count = i });
            data.items = items;
            string record = Directory.GetFiles(_root, "*.record", SearchOption.AllDirectories).Single();
            byte[] committed = File.ReadAllBytes(record);
            int receipts = 0;
            data.DataChanged += _ => receipts++;
            int sameMoney = data.money;
            data.money = sameMoney;
            data.items = items;
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 128; i++) data.money = sameMoney;
            double scalarMs = elapsed.Elapsed.TotalMilliseconds;
            elapsed.Restart();
            for (int i = 0; i < 128; i++) data.items = items;
            elapsed.Stop();
            Assert.AreEqual(0, receipts);
            CollectionAssert.AreEqual(committed, File.ReadAllBytes(record));
            TestContext.WriteLine($"SO cost UnchangedCollection: items={items.Count} assignmentsEach=128 scalarMs={scalarMs:F3} collectionMs={elapsed.Elapsed.TotalMilliseconds:F3} extraWrites={receipts}");
        }
        finally { Object.DestroyImmediate(data); }
    }
}
