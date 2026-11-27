using System;
using System.Collections;
using System.IO;
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
    public IEnumerator InPlaceListEditsReachDiskAtTheAutomaticPlayCheckpoint()
    {
        yield return new EnterPlayMode();
        var policy = Domain<SODataController>(SODataMode.DirectAccess, "periodic");
        var target = ScriptableObject.CreateInstance<PlainMoney>();
        try
        {
            yield return AwaitStorage(policy.AttachAsync(target, "slot-a"));
            target.claimedRewards.Add(17);
            string root = Environment.GetEnvironmentVariable(RootVariable);
            SOFileKeyStore disk = new SOFileKeyStore(Path.Combine(root, "SOData", "keys"));
            SODataRecord saved = null;
            double deadline = EditorApplication.timeSinceStartup + 8;
            while (saved == null && EditorApplication.timeSinceStartup < deadline)
            {
                Task<SODataRecord> read = disk.ReadAsync("slot-a/periodic", "claimedRewards", CancellationToken.None);
                yield return AwaitStorage(read);
                saved = read.Result;
                yield return null;
            }
            Assert.IsNotNull(saved);
            Assert.AreEqual("{\"value\":[17]}", saved.Value);
            CollectionAssert.AreEqual(new[] { 17 }, target.claimedRewards);
        }
        finally
        {
            policy.CloseData();
            Object.DestroyImmediate(policy);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public async Task ClosingANativeWalletSavesItsLastInPlaceEdit()
    {
        var policy = Domain<SODataController>(SODataMode.DirectAccess, "closing");
        var target = ScriptableObject.CreateInstance<PlainMoney>();
        try
        {
            await policy.AttachAsync(target, "slot");
            target.claimedRewards.Add(19);
            policy.CloseData();
            await policy.AttachAsync(target, "slot");
            CollectionAssert.AreEqual(new[] { 19 }, target.claimedRewards);
        }
        finally { Object.DestroyImmediate(policy); Object.DestroyImmediate(target); }
    }

    [Test]
    public async Task AFailedSecondAttachLeavesTheOriginalWriterBound()
    {
        var target = ScriptableObject.CreateInstance<PlainMoney>();
        var original = Domain<SODataController>(SODataMode.DirectAccess, "owner");
        var second = Domain<SODataController>(SODataMode.DirectAccess, "owner");
        try
        {
            await original.AttachAsync(target, "slot");
            await ExpectError<InvalidOperationException>(() => second.AttachAsync(target, "slot"));
            second.CloseData();
            target.money = 21;
            SOFileKeyStore disk = new SOFileKeyStore(Path.Combine(_root, "SOData", "keys"));
            var saved = await disk.ReadAsync("slot/owner", "money", CancellationToken.None);
            Assert.AreEqual("{\"value\":21}", saved.Value);
        }
        finally { Object.DestroyImmediate(second); Object.DestroyImmediate(original); Object.DestroyImmediate(target); }
    }
}
