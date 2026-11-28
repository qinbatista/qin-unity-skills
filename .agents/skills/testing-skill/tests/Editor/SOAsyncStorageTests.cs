using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed partial class UnityFoundationTests
{
    [TestCase(SODataMode.LocalFile)]
    [TestCase(SODataMode.DirectAccess)]
    public async Task FailedInitialReadCanRefreshWithoutLosingTheStagedValue(SODataMode mode)
    {
        SOStorageWorkloadData data = Domain<SOStorageWorkloadData>(mode, "recover-read");
        string scope = data.GetStorageScope("slot");
        string path = mode == SODataMode.LocalFile
            ? Path.Combine(_root, "SOData", "profiles", SOFileEnvelope.Revision(Encoding.UTF8.GetBytes(scope)), nameof(SOStorageWorkloadData))
            : Path.Combine(_root, "SOData", "keys", SOFileEnvelope.Revision(Encoding.UTF8.GetBytes(scope + "\0value")) + ".record");
        Directory.CreateDirectory(path);
        try
        {
            Task opening = data.OpenAsync("slot");
            data.Value = 5;
            try { await opening; Assert.Fail("A directory cannot be read as a data file."); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { }
            Assert.AreEqual(5, data.Value);
            Directory.Delete(path);
            await data.RefreshAsync();
            Assert.IsTrue(data.IsReady);
            await data.FlushAsync();
            Assert.IsNull(data.StorageError);
            await data.CloseAsync();
            await data.OpenAsync("slot");
            Assert.AreEqual(5, data.Value);
        }
        finally { data.CloseData(discardLocalChanges: true); Object.DestroyImmediate(data); }
    }

    [TestCase(SODataMode.LocalFile)]
    [TestCase(SODataMode.DirectAccess)]
    public async Task LoadingUsesOldValuesAndPreservesAnEditMadeBeforeTheReadCompletes(SODataMode mode)
    {
        SOStorageWorkloadData data = Domain<SOStorageWorkloadData>(mode, "late");
        string scope = data.GetStorageScope("slot");
        string path = mode == SODataMode.LocalFile
            ? Path.Combine(_root, "SOData", "profiles", SOFileEnvelope.Revision(Encoding.UTF8.GetBytes(scope)), nameof(SOStorageWorkloadData))
            : Path.Combine(_root, "SOData", "keys", SOFileEnvelope.Revision(Encoding.UTF8.GetBytes(scope + "\0value")) + ".record");
        if (mode == SODataMode.LocalFile) SOFileStorage.WriteSnapshot(path, "{\"$sodata\":1,\"value\":37}");
        else SOFileStorage.Commit(path, "{\"value\":37}", null, true, false, CancellationToken.None);
        TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>();
        ManualResetEventSlim release = new ManualResetEventSlim();
        Task<int> held = SOFileStorage.RunAsync(path, () => { entered.TrySetResult(true); release.Wait(); return 0; }, CancellationToken.None);
        try
        {
            await entered.Task;
            Task opening = data.OpenAsync("slot");
            Assert.IsFalse(opening.IsCompleted);
            Assert.AreEqual(0, data.Value);
            Stopwatch elapsed = Stopwatch.StartNew();
            data.Value = 5;
            for (int i = 0; i < 10000; i++) Assert.AreEqual(5, data.Value);
            elapsed.Stop();
            Assert.IsFalse(opening.IsCompleted);
            release.Set();
            await held;
            await opening;
            Assert.AreEqual(5, data.Value, "A late disk read must not overwrite the gameplay edit.");
            await data.CloseAsync();
            await data.OpenAsync("slot");
            Assert.AreEqual(5, data.Value);
            TestContext.WriteLine($"SO async {mode}: delayedRead=True continuedReads=10000 pendingValue=5 reopened=5 foregroundMs={elapsed.Elapsed.TotalMilliseconds:F3}");
        }
        finally { release.Set(); await held; release.Dispose(); await data.CloseAsync(); Object.DestroyImmediate(data); }
    }

    [Test]
    public async Task LargeSnapshotFocusCallbackMeasuresForegroundTimeAndRestoresEveryDomain()
    {
        List<SOStorageWorkloadData> domains = new List<SOStorageWorkloadData>();
        try
        {
            for (int i = 0; i < 4; i++)
            {
                SOStorageWorkloadData data = Domain<SOStorageWorkloadData>(SODataMode.LocalFile, "large-" + i);
                domains.Add(data);
                JsonUtility.FromJsonOverwrite("{\"_padding\":\"" + new string('x', 2 * 1024 * 1024) + "\"}", data);
                await data.OpenAsync("slot");
                data.Value = 1;
                await data.FlushAsync();
            }
            foreach (SOStorageWorkloadData data in domains) data.Value = 5;
            MethodInfo flush = typeof(SOLocalFileMode).GetMethod("FlushPending", BindingFlags.NonPublic | BindingFlags.Static);
            Stopwatch elapsed = Stopwatch.StartNew();
            flush.Invoke(null, null);
            elapsed.Stop();
            double callbackMs = elapsed.Elapsed.TotalMilliseconds;
            foreach (SOStorageWorkloadData data in domains) await data.FlushAsync();
            foreach (SOStorageWorkloadData data in domains)
            {
                await data.CloseAsync();
                await data.OpenAsync("slot");
                Assert.AreEqual(5, data.Value);
            }
            TestContext.WriteLine($"SO async LocalFile: domains=4 paddingEach=2097152 focusCallbackMs={callbackMs:F3} reopened=5,5,5,5");
        }
        finally { foreach (SOStorageWorkloadData data in domains) Object.DestroyImmediate(data); }
    }

    [Test]
    public async Task OfflineBurstMeasuresForegroundTimeAndRestoresTheFinalValue()
    {
        SOStorageWorkloadData data = Domain<SOStorageWorkloadData>(SODataMode.DirectAccess, "async-key");
        try
        {
            await data.OpenAsync("slot");
            data.Value = 1;
            await data.FlushAsync();
            Stopwatch elapsed = Stopwatch.StartNew();
            for (int i = 2; i <= 129; i++) data.Value = i;
            elapsed.Stop();
            double setterMs = elapsed.Elapsed.TotalMilliseconds;
            Assert.AreEqual(129, data.Value);
            await data.CloseAsync();
            await data.OpenAsync("slot");
            Assert.AreEqual(129, data.Value);
            TestContext.WriteLine($"SO async DirectAccess: changes=128 foregroundSetterMs={setterMs:F3} reopened=129");
        }
        finally { Object.DestroyImmediate(data); }
    }
}
