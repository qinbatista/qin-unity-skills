using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class UnityFoundationTests
{
    [UnityTest, Timeout(360000)]
    public IEnumerator LocalFileClockCommitsTwoDirtyDomainsAfterFiveRealMinutes()
    {
        var first = Domain<SOOutfitData>(SODataMode.LocalFile, "clock-a");
        var second = Domain<SOOutfitData>(SODataMode.LocalFile, "clock-b");
        var clean = Domain<SOOutfitData>(SODataMode.LocalFile, "clock-clean");
        try
        {
            first.OpenAsync("slot").GetAwaiter().GetResult();
            second.OpenAsync("slot").GetAwaiter().GetResult();
            clean.OpenAsync("slot").GetAwaiter().GetResult();
            int receipts = 0;
            first.DataChanged += _ => receipts++;
            second.DataChanged += _ => receipts++;
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            first.HatId = "first";
            first.HatId = "";
            for (int i = 0; i < 100; i++)
            {
                clean.HatId = "temporary";
                clean.HatId = "";
            }
            while (elapsed.Elapsed.TotalSeconds < 298) yield return null;
            Assert.AreEqual(0, receipts);
            Assert.IsFalse(Directory.Exists(_root));
            first.HatId = "latest";
            second.HatId = "second";
            while (receipts < 2 && elapsed.Elapsed.TotalSeconds < 325) yield return null;
            Assert.AreEqual(2, receipts);
            Assert.GreaterOrEqual(elapsed.Elapsed.TotalSeconds, 299.9);
            Assert.AreEqual(2, Directory.GetFiles(_root, nameof(SOOutfitData), SearchOption.AllDirectories).Length);
            first.CloseData();
            first.OpenAsync("slot").GetAwaiter().GetResult();
            Assert.AreEqual("latest", first.HatId);
            TestContext.WriteLine($"LocalFile actual clock: receipts={receipts}, elapsedSeconds={elapsed.Elapsed.TotalSeconds:F2}, cleanFiles=0");
        }
        finally { Object.DestroyImmediate(clean); Object.DestroyImmediate(second); Object.DestroyImmediate(first); }
    }
}
