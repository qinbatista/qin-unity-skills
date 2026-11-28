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
        public bool Loaded;
        public bool Pending;
        public string Desired;
        public Task Tail = Task.CompletedTask;
        public Exception Error;
    }

    private readonly Dictionary<SOStoredField, Entry> _entries = new Dictionary<SOStoredField, Entry>();
    private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
    private readonly ISODataKeyStore _channel;
    private Task _refreshing = Task.CompletedTask;
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
        using CancellationTokenSource opening = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
        SOStoredField[] fields = Fields;
        foreach (SOStoredField field in fields)
            if (!_entries.ContainsKey(field)) _entries.Add(field, new Entry());
        using SemaphoreSlim readers = new SemaphoreSlim(4);
        await Task.WhenAll(fields.Select(async field =>
        {
            await readers.WaitAsync(opening.Token);
            try
            {
                await LoadAsync(field, opening.Token);
                _entries[field].Loaded = true;
                if (_entries[field].Desired != null) StartWrite(field, _entries[field]);
            }
            finally { readers.Release(); }
        }));
    }

    protected virtual async Task LoadAsync(SOStoredField field, CancellationToken token)
    {
        int received = _entries[field].Received;
        SODataRecord record = await _channel.ReadAsync(Scope, field.Key, token);
        if (received == _entries[field].Received) Receive(field, record, true);
    }

    protected void Receive(SOStoredField field, SODataRecord record, bool notify)
    {
        if (Closed) return;
        Entry entry = _entries[field];
        if (entry.Error != null && entry.Tail.IsCompleted && entry.Desired == null)
        {
            entry.Error = null;
            entry.Tail = Task.CompletedTask;
            if (_entries.Values.All(value => value.Error == null) && Owner.IsCurrent(this)) Owner.ClearStorageError();
        }
        if (entry.Received != 0 && entry.Accepted?.Revision == record?.Revision) return;
        entry.Accepted = record;
        entry.Received++;
        if (entry.Pending || entry.Desired != null || !Owner.IsCurrent(this)) return;
        string wire = record?.Value ?? field.Initial;
        if (field.Visible == wire) return;
        field.Apply(wire);
        if (notify) Owner.Changed(field.Key);
    }

    protected override void Save(SOStoredField field, string wire)
    {
        if (!_entries.TryGetValue(field, out Entry entry)) _entries.Add(field, entry = new Entry());
        entry.Desired = wire;
        if (entry.Loaded) StartWrite(field, entry);
    }

    private void StartWrite(SOStoredField field, Entry entry)
    {
        if (!entry.Tail.IsCompleted || Closed) return;
        entry.Tail = SendAsync(field, entry);
        Task work = entry.Tail;
        work.GetAwaiter().OnCompleted(() => { if (work.IsFaulted) work.Exception.Handle(error => true); });
    }

    private async Task SendAsync(SOStoredField field, Entry entry)
    {
        await Task.Yield();
        while (!Closed && entry.Desired != null)
        {
            string wire = entry.Desired;
            entry.Desired = null;
            entry.Pending = true;
            try
            {
                if (entry.Error != null)
                {
                    int refresh = entry.Received;
                    SODataRecord current = await _channel.ReadAsync(Scope, field.Key, _lifetime.Token);
                    if (refresh == entry.Received) { entry.Accepted = current; entry.Received++; }
                }
                int received = entry.Received;
                SODataRecord accepted = entry.Accepted?.Value == wire ? entry.Accepted : await _channel.WriteAsync(Scope, field.Key, wire, entry.Accepted?.Revision, _lifetime.Token);
                if (Closed) return;
                if (accepted == null) throw new InvalidOperationException("Missing storage acknowledgement.");
                if (received == entry.Received) { entry.Accepted = accepted; entry.Received++; }
                entry.Error = null;
            }
            catch (Exception error)
            {
                entry.Error = error;
                if (!Closed)
                {
                    if (error is UnauthorizedAccessException || error is SODataConflictException)
                    {
                        if (error is SODataConflictException)
                        {
                            int refresh = entry.Received;
                            SODataRecord current = await _channel.ReadAsync(Scope, field.Key, _lifetime.Token);
                            if (refresh == entry.Received) { entry.Accepted = current; entry.Received++; }
                        }
                        if (entry.Desired == null && field.Visible == wire && Owner.IsCurrent(this))
                        {
                            string correction = entry.Accepted?.Value ?? field.Initial;
                            if (field.Visible != correction) { field.Apply(correction); Owner.Changed(field.Key); }
                        }
                    }
                    else if (entry.Desired == null) entry.Desired = wire;
                    if (Owner.IsCurrent(this)) Owner.Failed(error);
                }
                throw;
            }
            finally { entry.Pending = false; }
            if (entry.Desired == null && Owner.IsCurrent(this))
            {
                field.Apply(entry.Accepted?.Value ?? field.Initial);
                Owner.ClearStorageError();
                Owner.Changed(field.Key);
            }
        }
    }

    internal override async Task FlushAsync()
    {
        foreach (KeyValuePair<SOStoredField, Entry> pair in _entries)
            if (pair.Value.Desired != null) StartWrite(pair.Key, pair.Value);
        await Task.WhenAll(_entries.Values.Select(entry => entry.Tail));
    }

    internal override Task RefreshAsync()
    {
        if (!_refreshing.IsCompleted) return _refreshing;
        _refreshing = RefreshFieldsAsync();
        return _refreshing;
    }

    private async Task RefreshFieldsAsync()
    {
        await Task.Yield();
        using SemaphoreSlim readers = new SemaphoreSlim(4);
        await Task.WhenAll(_entries.Keys.ToArray().Select(async field =>
        {
            await readers.WaitAsync(_lifetime.Token);
            try { await LoadAsync(field, _lifetime.Token); }
            finally { readers.Release(); }
        }));
    }

    public override void Dispose()
    {
        Closed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
