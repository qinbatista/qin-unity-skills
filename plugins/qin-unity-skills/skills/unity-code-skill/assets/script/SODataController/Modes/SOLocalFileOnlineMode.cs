using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

public sealed class SODataFileRecord
{
    public byte[] Content { get; }
    public string Revision { get; }
    public SODataFileRecord(byte[] content, string revision)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        Revision = !string.IsNullOrEmpty(revision) ? revision : throw new ArgumentException("A file acknowledgement requires a revision.");
    }
}

public interface ISODataFileStore
{
    Task<string> GetRevisionAsync(string scope, CancellationToken cancellation);
    Task<SODataFileRecord> ReadAsync(string scope, CancellationToken cancellation);
    Task<SODataFileRecord> WriteAsync(string scope, byte[] content, string expectedRevision, CancellationToken cancellation);
}

public enum SOFileConflictResolution { KeepLocal, UseRemote }

sealed class SOLocalFileOnlineMode : SOLocalFileMode
{
    private sealed class BackupState
    {
        public string Revision;
        public string Hash;
    }

    private readonly ISODataFileStore _channel;
    private readonly string _scope;
    private readonly string _statePath;
    private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
    private BackupState _accepted = new BackupState();
    private Task _synchronizing = Task.CompletedTask;
    private Task _retrying = Task.CompletedTask;
    private bool _refreshNeeded = true;
    private bool _stateLoaded;
    private bool _restoring;
    private bool _disposed;
    private string _queued;
    private string _synchronizedLocal;
    private SOFileConflictResolution? _resolution;
    private readonly SemaphoreSlim _resolutions = new SemaphoreSlim(1);

    public SOLocalFileOnlineMode(SODataController owner, string scope, string slot, ISODataFileStore channel) : base(owner, scope, slot)
    {
#if SO_DATA_UNITY_CLOUD_SAVE
        if (channel == null) channel = new UnityCloudSaveFileStore();
#endif
        _channel = channel ?? throw new ArgumentException("Supply the signed-in file provider.");
        _scope = scope;
        _statePath = FilePath + ".cloud-record";
        Application.focusChanged += OnFocus;
    }

    internal override async Task OpenAsync(CancellationToken token)
    {
        await base.OpenAsync(token);
        try { await StartSync(true); }
        catch (Exception error) { if (Owner.IsCurrent(this)) Owner.Failed(error); }
    }

    protected override void OnCommitted(string json)
    {
        _queued = json;
        if (_restoring || !_stateLoaded) return;
        Task work = StartSync(false);
        work.GetAwaiter().OnCompleted(() => { if (work.IsFaulted) work.Exception.Handle(error => true); });
    }

    internal override async Task FlushAsync()
    {
        await FlushLocalAsync();
        await StartSync(false);
    }

    internal override Task RefreshAsync() => StartSync(true);

    public async Task ResolveAsync(SOFileConflictResolution resolution)
    {
        await _resolutions.WaitAsync(_lifetime.Token);
        _resolution = resolution;
        try
        {
            await FlushLocalAsync();
            try { await _synchronizing; }
            catch (SODataConflictException) { }
            _refreshNeeded = true;
            await StartSync(true);
        }
        finally { _resolution = null; _resolutions.Release(); }
    }

    private Task StartSync(bool refresh)
    {
        if (_disposed) return Task.CompletedTask;
        if (!_synchronizing.IsCompleted) return _synchronizing;
        if (!refresh && !_refreshNeeded && _stateLoaded && _queued == null && ReferenceEquals(_synchronizedLocal, SnapshotJson)) return Task.CompletedTask;
        _synchronizing = SynchronizeAsync(refresh);
        return _synchronizing;
    }

    private async Task SynchronizeAsync(bool refresh)
    {
        await Task.Yield();
        try
        {
            refresh |= _refreshNeeded;
            if (!_stateLoaded)
            {
                _accepted = await SOFileStorage.RunAsync(_statePath, () =>
                {
                    string json = SOFileStorage.Read(_statePath, false, out string revision);
                    return json == null ? new BackupState() : JsonConvert.DeserializeObject<BackupState>(json) ?? throw new InvalidOperationException("Invalid file acknowledgement.");
                }, _lifetime.Token);
                _stateLoaded = true;
                refresh = true;
            }
            if (refresh)
            {
                Dictionary<SOStoredField, string> unchanged = Fields.ToDictionary(field => field, field => field.Visible);
                string revision = await _channel.GetRevisionAsync(_scope, _lifetime.Token);
                SODataFileRecord remote = revision == null || revision == _accepted.Revision && HasLocalFile ? null : await _channel.ReadAsync(_scope, _lifetime.Token);
                if (revision != null && revision != _accepted.Revision && remote == null) throw new SODataConflictException("The remote file disappeared during refresh. The local file is retained.");
                string localHash = HasLocalFile ? await HashAsync(SnapshotJson) : null;
                if (remote != null)
                {
                    string json = await Task.Run(() => new UTF8Encoding(false, true).GetString(remote.Content));
                    string hash = await HashAsync(json);
                    if (HasLocalFile && localHash != _accepted.Hash && localHash != hash && _accepted.Revision != remote.Revision && !_resolution.HasValue) throw new SODataConflictException("Both the local file and remote backup changed. The local file is retained.");
                    bool restore = _resolution == SOFileConflictResolution.UseRemote || _resolution != SOFileConflictResolution.KeepLocal && (!HasLocalFile || localHash == _accepted.Hash);
                    if (restore && Owner.IsCurrent(this))
                    {
                        _restoring = true;
                        try { await RestoreRemoteAsync(json, unchanged); }
                        finally { _restoring = false; }
                    }
                    await AcknowledgeAsync(remote.Revision, hash);
                }
                else if (revision == null && _accepted.Revision != null)
                {
                    if (_resolution != SOFileConflictResolution.KeepLocal) throw new SODataConflictException("The acknowledged remote file was removed. The local file is retained.");
                    await AcknowledgeAsync(null, null);
                }
                _refreshNeeded = false;
            }
            await FlushLocalAsync();
            if (_queued == null && HasLocalFile && await HashAsync(SnapshotJson) != _accepted.Hash) _queued = SnapshotJson;
            while (_queued != null)
            {
                string json = _queued;
                _queued = null;
                string hash = await HashAsync(json);
                if (hash == _accepted.Hash) continue;
                try
                {
                    byte[] content = await Task.Run(() => Encoding.UTF8.GetBytes(json));
                    SODataFileRecord saved = await _channel.WriteAsync(_scope, content, _accepted.Revision, _lifetime.Token);
                    if (saved == null) throw new InvalidOperationException("Missing remote file acknowledgement.");
                    if (await Task.Run(() => SOFileEnvelope.Revision(saved.Content)) != hash) throw new System.IO.InvalidDataException("The acknowledged file differs from the submitted content.");
                    await AcknowledgeAsync(saved.Revision, hash);
                }
                catch { if (_queued == null) _queued = json; throw; }
            }
            _synchronizedLocal = SnapshotJson;
            if (Owner.IsCurrent(this)) Owner.ClearStorageError();
        }
        catch (Exception error)
        {
            if (Owner.IsCurrent(this)) Owner.Failed(error);
            if (error is SODataConflictException) _refreshNeeded = true;
            if (!(error is SODataConflictException) && !(error is UnauthorizedAccessException) && _retrying.IsCompleted && !_disposed) _retrying = RetryAsync();
            throw;
        }
    }

    private async Task RetryAsync()
    {
        int seconds = 5;
        try
        {
            while (!_disposed && Owner.IsCurrent(this))
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), _lifetime.Token);
                if (_queued == null && !_refreshNeeded) return;
                try { await StartSync(_refreshNeeded); return; }
                catch (Exception error) when (!(error is SODataConflictException) && !(error is UnauthorizedAccessException)) { seconds = Math.Min(seconds * 2, 60); }
            }
        }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception error) { if (Owner.IsCurrent(this)) Owner.Failed(error); }
    }

    private void OnFocus(bool focused)
    {
        if (!focused || !Owner.IsCurrent(this)) return;
        Task work = StartSync(true);
        work.GetAwaiter().OnCompleted(() => { if (work.IsFaulted) work.Exception.Handle(error => true); });
    }

    private static Task<string> HashAsync(string json) => Task.Run(() => SOFileEnvelope.Revision(Encoding.UTF8.GetBytes(json)));

    private async Task AcknowledgeAsync(string revision, string hash)
    {
        if (_accepted.Revision == revision && _accepted.Hash == hash) return;
        BackupState accepted = new BackupState { Revision = revision, Hash = hash };
        await SOFileStorage.RunAsync(_statePath, () => SOFileStorage.Commit(_statePath, JsonConvert.SerializeObject(accepted), null, false, false, CancellationToken.None), CancellationToken.None);
        _accepted = accepted;
    }

    public override void Dispose()
    {
        _disposed = true;
        Application.focusChanged -= OnFocus;
        _lifetime.Cancel();
        _lifetime.Dispose();
        base.Dispose();
    }
}
