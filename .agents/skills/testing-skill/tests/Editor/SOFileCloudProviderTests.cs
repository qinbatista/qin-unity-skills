#if SO_DATA_UNITY_CLOUD_SAVE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Internal;
using Unity.Services.CloudSave.Internal.Models;
using Unity.Services.CloudSave.Models;
using Object = UnityEngine.Object;

public sealed partial class UnityFoundationTests
{
    private sealed class CloudFilesFixture : IPlayerFilesService
    {
        public readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>();
        public int Uploads;
        public int Downloads;
        public bool FailAfterUpload;
        public Task Acknowledgement = Task.CompletedTask;
        public async Task SaveAsync(string key, byte[] bytes, SaveOptions options = null)
        {
            Files[key] = (byte[])bytes.Clone();
            Uploads++;
            await Acknowledgement;
            if (FailAfterUpload) { FailAfterUpload = false; throw new IOException("The file was accepted before the connection closed."); }
        }
        public Task<byte[]> LoadBytesAsync(string key) { Downloads++; return Task.FromResult((byte[])Files[key].Clone()); }
        public Task DeleteAsync(string key, DeleteOptions options = null) { Files.Remove(key); return Task.CompletedTask; }
        public Task<List<FileItem>> ListAllAsync() => throw new NotSupportedException();
        public Task<FileItem> GetMetadataAsync(string key) => throw new NotSupportedException();
        public Task SaveAsync(string key, Stream stream, SaveOptions options = null) => throw new NotSupportedException();
        public Task<Stream> LoadStreamAsync(string key) => throw new NotSupportedException();
    }

    [Test]
    public async Task FileProviderPublishesBinaryDataAndRejectsAStaleWriterWithoutOrphans()
    {
        CloudFilesFixture files = new CloudFilesFixture();
        CloudPlayerFixture heads = new CloudPlayerFixture();
        UnityCloudSaveKeyStore index = new UnityCloudSaveKeyStore(heads, () => "alice");
        UnityCloudSaveFileStore first = new UnityCloudSaveFileStore(files, index, () => "alice");
        UnityCloudSaveFileStore second = new UnityCloudSaveFileStore(files, index, () => "alice");
        byte[] bytes = { 0, 255, 5, 37 };
        SODataFileRecord saved = await first.WriteAsync("binary", bytes, null, CancellationToken.None);
        CollectionAssert.AreEqual(bytes, (await second.ReadAsync("binary", CancellationToken.None)).Content);
        SODataFileRecord same = await second.WriteAsync("binary", bytes, saved.Revision, CancellationToken.None);
        Assert.AreEqual(saved.Revision, same.Revision);
        Assert.AreEqual(1, files.Uploads);
        saved = await first.WriteAsync("binary", new byte[] { 6 }, saved.Revision, CancellationToken.None);
        Assert.AreEqual(1, files.Files.Count);
        try { await second.WriteAsync("binary", new byte[] { 7 }, same.Revision, CancellationToken.None); Assert.Fail("The old revision must be rejected."); }
        catch (SODataConflictException) { }
        Assert.AreEqual(1, files.Files.Count);
        files.FailAfterUpload = true;
        try { await first.WriteAsync("binary", new byte[] { 8 }, saved.Revision, CancellationToken.None); Assert.Fail("The upload reply was lost."); }
        catch (IOException) { }
        Assert.AreEqual(1, files.Files.Count);
        CollectionAssert.AreEqual(new byte[] { 6 }, (await first.ReadAsync("binary", CancellationToken.None)).Content);
        heads.DropAcknowledgementFor = UnityCloudSaveKeyStore.GetCloudKey("binary", "file");
        saved = await first.WriteAsync("binary", new byte[] { 9 }, saved.Revision, CancellationToken.None);
        heads.DropAcknowledgementFor = null;
        Assert.AreEqual(1, files.Files.Count);
        CollectionAssert.AreEqual(new byte[] { 9 }, (await second.ReadAsync("binary", CancellationToken.None)).Content);
    }

    [Test]
    public async Task ConcurrentFirstFileWritersPublishOneCompleteFileAndRemoveTheLosingBlob()
    {
        CloudFilesFixture files = new CloudFilesFixture();
        CloudPlayerFixture heads = new CloudPlayerFixture();
        UnityCloudSaveKeyStore index = new UnityCloudSaveKeyStore(heads, () => "alice");
        UnityCloudSaveFileStore first = new UnityCloudSaveFileStore(files, index, () => "alice");
        UnityCloudSaveFileStore second = new UnityCloudSaveFileStore(files, index, () => "alice");
        TaskCompletionSource<bool> release = new TaskCompletionSource<bool>();
        try
        {
            files.Acknowledgement = release.Task;
            Task<SODataFileRecord> one = first.WriteAsync("race", new byte[] { 5 }, null, CancellationToken.None);
            Task<SODataFileRecord> two = second.WriteAsync("race", new byte[] { 6 }, null, CancellationToken.None);
            for (int i = 0; i < 100 && files.Uploads < 2; i++) await Task.Delay(10);
            Assert.AreEqual(2, files.Uploads);
            release.SetResult(true);
            try { await Task.WhenAll(one, two); Assert.Fail("A conditional first-writer race must reject one writer."); }
            catch (SODataConflictException) { }
            Assert.AreEqual(1, new[] { one, two }.Count(work => work.Status == TaskStatus.RanToCompletion));
            SODataFileRecord winner = one.Status == TaskStatus.RanToCompletion ? await one : await two;
            CollectionAssert.AreEqual(winner.Content, (await first.ReadAsync("race", CancellationToken.None)).Content);
            Assert.AreEqual(1, files.Files.Count);
            TestContext.WriteLine("SO file concurrency: firstWriters=2 acceptedHeads=1 losingBlobs=0 completeFile=True provider=controlledUnitySDK");
        }
        finally { release.TrySetResult(true); }
    }

    [Test]
    public async Task LocalFileOnlineCommitsLocallyBeforeUploadAndRecoversOnANewDevice()
    {
        CloudFilesFixture files = new CloudFilesFixture();
        CloudPlayerFixture heads = new CloudPlayerFixture();
        UnityCloudSaveFileStore provider = new UnityCloudSaveFileStore(files, new UnityCloudSaveKeyStore(heads, () => "alice"), () => "alice");
        SOStorageWorkloadData data = Domain<SOStorageWorkloadData>(SODataMode.LocalFileOnline, "file");
        TaskCompletionSource<bool> release = new TaskCompletionSource<bool>();
        try
        {
            await data.OpenAsync("slot", fileProvider: provider);
            Assert.AreEqual(0, files.Uploads);
            files.Acknowledgement = release.Task;
            data.Value = 5;
            Task saving = data.FlushAsync();
            for (int i = 0; i < 100 && files.Uploads == 0; i++) await Task.Delay(10);
            Assert.AreEqual(1, files.Uploads);
            Assert.IsFalse(saving.IsCompleted);
            string path = Directory.GetFiles(_root, nameof(SOStorageWorkloadData), SearchOption.AllDirectories).Single();
            StringAssert.Contains("\"value\":5", SOFileStorage.ReadSnapshot(path));
            data.Value = 6;
            Assert.AreEqual(6, data.Value);
            release.SetResult(true);
            await saving;
            await data.FlushAsync();
            SODataFileRecord remote = await provider.ReadAsync(data.GetStorageScope("slot"), CancellationToken.None);
            StringAssert.Contains("\"value\":6", Encoding.UTF8.GetString(remote.Content));
            int uploads = files.Uploads;
            int changes = 0;
            data.DataChanged += key => changes++;
            for (int i = 0; i < 10000; i++) data.Value = 6;
            await data.FlushAsync();
            Assert.AreEqual(uploads, files.Uploads);
            Assert.AreEqual(0, changes);
            int downloads = files.Downloads;
            await data.RefreshAsync();
            Assert.AreEqual(downloads, files.Downloads, "An unchanged remote revision must not download the whole file.");
            await data.CloseAsync();
            Directory.Delete(_root, true);
            await data.OpenAsync("slot", fileProvider: provider);
            Assert.AreEqual(6, data.Value);
            Assert.AreEqual(uploads, files.Uploads, "Restoring the accepted remote file must not upload it again.");
            Assert.AreEqual(1, files.Files.Count);
            TestContext.WriteLine("SO file online: localBeforeRemoteAck=True liveEdit=6 coldRemoteRestore=6 equalAssignments=10000 extraUploads=0 extraChanges=0 provider=controlledUnitySDK");
        }
        finally { release.TrySetResult(true); await data.CloseAsync(); Object.DestroyImmediate(data); }
    }

    [Test]
    public async Task FailedFileUploadRetainsTheLocalOutboxAcrossRestart()
    {
        CloudFilesFixture files = new CloudFilesFixture();
        CloudPlayerFixture heads = new CloudPlayerFixture();
        UnityCloudSaveFileStore provider = new UnityCloudSaveFileStore(files, new UnityCloudSaveKeyStore(heads, () => "alice"), () => "alice");
        SOStorageWorkloadData data = Domain<SOStorageWorkloadData>(SODataMode.LocalFileOnline, "outbox");
        try
        {
            await data.OpenAsync("slot", fileProvider: provider);
            files.FailAfterUpload = true;
            data.Value = 5;
            try { await data.FlushAsync(); Assert.Fail("The upload reply was lost."); }
            catch (IOException) { }
            Assert.AreEqual(5, data.Value);
            data.CloseData(discardLocalChanges: true);
            await data.OpenAsync("slot", fileProvider: provider);
            await data.FlushAsync();
            Assert.AreEqual(5, data.Value);
            StringAssert.Contains("\"value\":5", Encoding.UTF8.GetString((await provider.ReadAsync(data.GetStorageScope("slot"), CancellationToken.None)).Content));
            Assert.AreEqual(1, files.Files.Count);
        }
        finally { await data.CloseAsync(); Object.DestroyImmediate(data); }
    }

    [Test]
    public async Task FailedBackupRetriesWithoutAnotherGameplayEditOrLocalRewrite()
    {
        CloudFilesFixture files = new CloudFilesFixture();
        CloudPlayerFixture heads = new CloudPlayerFixture();
        UnityCloudSaveFileStore provider = new UnityCloudSaveFileStore(files, new UnityCloudSaveKeyStore(heads, () => "alice"), () => "alice");
        SOStorageWorkloadData data = Domain<SOStorageWorkloadData>(SODataMode.LocalFileOnline, "retry");
        try
        {
            await data.OpenAsync("slot", fileProvider: provider);
            files.FailAfterUpload = true;
            data.Value = 5;
            try { await data.FlushAsync(); Assert.Fail("The upload reply was lost."); }
            catch (IOException) { }
            string local = Directory.GetFiles(_root, nameof(SOStorageWorkloadData), SearchOption.AllDirectories).Single();
            byte[] bytes = File.ReadAllBytes(local);
            for (int i = 0; i < 200 && data.StorageError != null; i++) await Task.Delay(50);
            Assert.IsNull(data.StorageError);
            Assert.AreEqual(2, files.Uploads);
            Assert.AreEqual(1, files.Files.Count);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(local));
            TestContext.WriteLine("SO file retry: extraGameplayEdits=0 failedUploads=1 recoveredUploads=1 localRewrites=0 pendingValue=5");
        }
        finally { await data.CloseAsync(); Object.DestroyImmediate(data); }
    }

    [TestCase(SOFileConflictResolution.KeepLocal)]
    [TestCase(SOFileConflictResolution.UseRemote)]
    public async Task CompetingFileSnapshotsCanResolveThroughTheSO(SOFileConflictResolution resolution)
    {
        CloudFilesFixture files = new CloudFilesFixture();
        CloudPlayerFixture heads = new CloudPlayerFixture();
        UnityCloudSaveFileStore provider = new UnityCloudSaveFileStore(files, new UnityCloudSaveKeyStore(heads, () => "alice"), () => "alice");
        SOStorageWorkloadData data = Domain<SOStorageWorkloadData>(SODataMode.LocalFileOnline, "resolve");
        try
        {
            await data.OpenAsync("slot", fileProvider: provider);
            data.Value = 1;
            await data.FlushAsync();
            string scope = data.GetStorageScope("slot");
            SODataFileRecord previous = await provider.ReadAsync(scope, CancellationToken.None);
            byte[] changed = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(previous.Content).Replace("\"value\":1", "\"value\":7"));
            await provider.WriteAsync(scope, changed, previous.Revision, CancellationToken.None);
            data.Value = 5;
            try { await data.FlushAsync(); Assert.Fail("Both snapshots changed independently."); }
            catch (SODataConflictException) { }
            Assert.AreEqual(5, data.Value);
            await data.ResolveFileConflictAsync(resolution);
            int expected = resolution == SOFileConflictResolution.KeepLocal ? 5 : 7;
            Assert.AreEqual(expected, data.Value);
            StringAssert.Contains("\"value\":" + expected, Encoding.UTF8.GetString((await provider.ReadAsync(scope, CancellationToken.None)).Content));
            Assert.AreEqual(1, files.Files.Count);
            Assert.IsNull(data.StorageError);
            TestContext.WriteLine($"SO file resolution: policy={resolution} memory={expected} localAndRemoteAgree=True retainedFiles=1");
        }
        finally { await data.CloseAsync(); Object.DestroyImmediate(data); }
    }
}
#endif
