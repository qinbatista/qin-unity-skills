using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed class SODataRecord
{
    public string Value { get; }
    public string Revision { get; }

    public SODataRecord(string value, string revision)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Revision = !string.IsNullOrEmpty(revision) ? revision : throw new ArgumentException("Missing storage revision.");
    }
}

public sealed class SODataConflictException : InvalidOperationException
{
    public SODataConflictException(string message) : base(message) { }
}

public interface ISODataKeyStore
{
    Task<SODataRecord> ReadAsync(string scope, string key, CancellationToken cancellation);
    Task<SODataRecord> WriteAsync(string scope, string key, string value, string expectedRevision, CancellationToken cancellation);
}

internal class SODirectAccessOnlineMode : SOStorageMode
{
    private sealed class Entry
    {
        public SODataRecord Accepted;
        public int Received;
        public int Pending;
        public Task Tail = Task.CompletedTask;
        public Exception Error;
    }

    private readonly Dictionary<SOStoredField, Entry> _entries = new Dictionary<SOStoredField, Entry>();
    private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
    private readonly ISODataKeyStore _channel;
    protected readonly string Scope;
    protected bool Closed { get; private set; }

    internal SODirectAccessOnlineMode(SODataController owner, string scope, ISODataKeyStore channel, bool realtime = false) : base(owner)
    {
#if SO_DATA_UNITY_CLOUD_SAVE
        if (channel == null && !realtime) channel = new UnityCloudSaveKeyStore();
#endif
        _channel = channel ?? throw new ArgumentException("Supply the signed-in storage channel.");
        if (channel is ISOMatchSessionStore && !realtime) throw new ArgumentException("A match channel cannot store persistent account data.");
        Scope = scope;
    }

    internal override async Task OpenAsync(CancellationToken token)
    {
        using var opening = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
        foreach (var field in Owner.Fields)
        {
            _entries.Add(field, new Entry());
            await LoadAsync(field, opening.Token);
            opening.Token.ThrowIfCancellationRequested();
        }
    }

    protected virtual async Task LoadAsync(SOStoredField field, CancellationToken token) =>
        Receive(field, await _channel.ReadAsync(Scope, field.Key, token), false);

    protected void Receive(SOStoredField field, SODataRecord record, bool notify)
    {
        if (Closed) return;
        var entry = _entries[field];
        if (entry.Received != 0 && entry.Accepted?.Revision == record?.Revision) return;
        entry.Accepted = record;
        entry.Received++;
        if (entry.Pending != 0) return;
        field.Apply(record?.Value ?? field.Initial);
        if (notify) Owner.Changed(field.Key);
    }

    protected override void Save(SOStoredField field, string wire)
    {
        var entry = _entries[field];
        if (entry.Error != null) throw new InvalidOperationException("Reopen the SO after a failed write.", entry.Error);
        entry.Pending++;
        TaskCompletionSource<bool> start = new TaskCompletionSource<bool>();
        // Publish the receipt before a synchronous provider or listener can re-enter the setter.
        var receipt = SendAsync(field, entry, wire, entry.Tail, start.Task);
        entry.Tail = receipt;
        start.SetResult(true);
        if (receipt.IsCompleted) receipt.GetAwaiter().GetResult();
    }

    private async Task SendAsync(SOStoredField field, Entry entry, string wire, Task previous, Task start)
    {
        await start;
        try
        {
            await previous;
            _lifetime.Token.ThrowIfCancellationRequested();
            int received = entry.Received;
            var result = await _channel.WriteAsync(Scope, field.Key, wire, entry.Accepted?.Revision, _lifetime.Token);
            if (Closed) throw new OperationCanceledException();
            if (result == null) throw new InvalidOperationException("Missing storage acknowledgement.");
            if (received == entry.Received) entry.Accepted = result;
        }
        catch (Exception error)
        {
            if (!Closed && entry.Error == null)
            {
                entry.Error = error;
                field.Apply(entry.Accepted?.Value ?? field.Initial);
                Owner.Failed(error);
            }
            throw;
        }
        finally { entry.Pending--; }
        if (entry.Pending == 0 && !Closed)
        {
            field.Apply(entry.Accepted?.Value ?? field.Initial);
            Owner.Changed(field.Key);
        }
    }

    internal override Task FlushAsync() => Task.WhenAll(_entries.Values.Select(entry => entry.Tail));

    public override void Dispose()
    {
        Closed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
