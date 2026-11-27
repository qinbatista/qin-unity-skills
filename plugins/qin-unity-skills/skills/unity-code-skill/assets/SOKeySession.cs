using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

internal sealed class SOKeySession : IDisposable
{
    private sealed class Writes
    {
        public string Visible;
        public int Generation;
        public int Pending;
        public Exception Error;
        public Task<Exception> Tail = Task.FromResult<Exception>(null);
    }

    private readonly ISODataKeyStore _store;
    private readonly string _scope;
    private readonly SODataFields _fields;
    private readonly Dictionary<SODataFields.Entry, Writes> _writes = new Dictionary<SODataFields.Entry, Writes>();
    private readonly Action<string> _notify;
    private readonly Action<Exception> _report;
    private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
    private readonly TaskCompletionSource<bool> _closing = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
    private bool _closed;
    private bool _ready;
    private int _applying;

    internal bool WritesThrough => _store is ISOWriteThroughStore;

    internal SOKeySession(ISODataKeyStore store, string scope, SODataFields fields, Action<string> notify, Action<Exception> report)
    {
        _store = store;
        _scope = scope;
        _fields = fields;
        _notify = notify;
        _report = report;
        foreach (var field in fields.Entries.Values)
            _writes.Add(field, new Writes { Visible = field.Authored });
    }

    internal async Task OpenAsync(ISORealtimeDataStore realtime, CancellationToken cancellation)
    {
        using var opening = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellation);
        foreach (var field in _fields.Entries.Values)
        {
            opening.Token.ThrowIfCancellationRequested();
            if (realtime == null) Receive(field, await _store.ReadAsync(_scope, field.Key, opening.Token));
            else
            {
                var subscription = await realtime.SubscribeAsync(_scope, field.Key, record => Receive(field, record), opening.Token);
                if (subscription == null) throw new InvalidOperationException("Missing realtime subscription.");
                if (_closed) subscription.Dispose();
                else _subscriptions.Add(subscription);
            }
            opening.Token.ThrowIfCancellationRequested();
            if (!field.Initialized) throw new InvalidOperationException("Missing initial value for " + field.Key);
        }
        foreach (var field in _fields.Entries.Values) Project(field, field.Accepted?.Value ?? field.Authored);
        _ready = true;
    }

    private void Project(SODataFields.Entry field, string value)
    {
        _applying++;
        try
        {
            if (field.Capture() != value) field.Apply(value);
            _writes[field].Visible = value;
        }
        finally { _applying--; }
    }

    private void Receive(SODataFields.Entry field, SODataRecord record)
    {
        if (_closed || (field.Initialized && field.Accepted?.Revision == record?.Revision)) return;
        field.Initialized = true;
        field.Notifications++;
        field.Accepted = record;
        if (!_ready || _writes[field].Pending > 0) return;
        try { Project(field, record?.Value ?? field.Authored); }
        catch (Exception error) { _writes[field].Error = error; _report(error); throw; }
        _notify(field.Key);
    }

    internal void Assigned(string name)
    {
        if (_applying > 0 || _closed || !_fields.TryGetByField(name, out var field)) return;
        CaptureAssignment(field);
    }

    private void CaptureAssignment(SODataFields.Entry field)
    {
        Writes writes = _writes[field];
        if (!_ready || writes.Error != null)
        {
            Project(field, field.Accepted?.Value ?? field.Authored);
            ThrowError(writes.Error);
            throw new InvalidOperationException("Wait for the data owner to finish opening.");
        }
        string value = field.Capture();
        if (value == writes.Visible) return;
        writes.Visible = value;
        var result = Submit(field, value, CancellationToken.None);
        if (result.IsCompleted) ThrowError(result.GetAwaiter().GetResult());
    }

    private Task<Exception> Submit(SODataFields.Entry field, string value, CancellationToken cancellation)
    {
        Writes writes = _writes[field];
        if (_store is ISOWriteThroughStore device)
        {
            try
            {
                field.Accepted = device.Write(_scope, field.Key, value, field.Accepted?.Revision, cancellation)
                    ?? throw new InvalidOperationException("Missing local storage acknowledgement.");
                Project(field, field.Accepted.Value);
            }
            catch (Exception error) { Reject(field, error); throw; }
            _notify(field.Key);
            return writes.Tail;
        }

        // Publish the tail before starting a provider that can acknowledge and re-enter synchronously.
        TaskCompletionSource<bool> start = new TaskCompletionSource<bool>();
        Task<Exception> preceding = writes.Tail;
        writes.Pending++;
        Task<Exception> receipt = WriteQueuedAsync(field, value, ++writes.Generation, preceding, start.Task, cancellation);
        writes.Tail = receipt;
        start.SetResult(true);
        return receipt;
    }

    private async Task<Exception> WriteQueuedAsync(SODataFields.Entry field, string value, int generation,
        Task<Exception> preceding, Task start, CancellationToken cancellation)
    {
        await start;
        Writes writes = _writes[field];
        Exception failure = null;
        try
        {
            using var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellation);
            ThrowError(await preceding);
            operation.Token.ThrowIfCancellationRequested();
            int notifications = field.Notifications;
            var record = await _store.WriteAsync(_scope, field.Key, value, field.Accepted?.Revision, operation.Token);
            if (_closed) throw new OperationCanceledException("The data owner closed before the acknowledgement.");
            if (record == null) throw new InvalidOperationException("Missing provider acknowledgement.");
            // A push arriving during the write already contains the service's accepted projection.
            if (notifications == field.Notifications) field.Accepted = record;
            if (generation == writes.Generation) Project(field, field.Accepted?.Value ?? field.Authored);
        }
        catch (Exception error)
        {
            failure = _closed ? new OperationCanceledException("The data owner closed during the write.", error) : error;
            if (!_closed && writes.Error == null) Reject(field, failure);
        }
        finally
        {
            writes.Pending--;
        }
        if (failure == null && !_closed && generation == writes.Generation) _notify(field.Key);
        return failure;
    }

    private void Reject(SODataFields.Entry field, Exception error)
    {
        _writes[field].Error = error;
        Project(field, field.Accepted?.Value ?? field.Authored);
        _report(error);
    }

    // Compatibility for callers of the previous explicit, acknowledged mutation API.
    internal async Task ChangeAsync(string key, Action change, CancellationToken cancellation)
    {
        if (!_ready || _closed) throw new InvalidOperationException("Open the data owner first.");
        if (!_fields.Entries.TryGetValue(key, out var field)) throw new ArgumentException("Unbound storage key: " + key);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellation);
        await field.Gate.WaitAsync(operation.Token);
        try
        {
            var writes = _writes[field];
            ThrowError(await writes.Tail);
            operation.Token.ThrowIfCancellationRequested();
            ThrowError(writes.Error);
            string previous = field.Capture();
            string next;
            _applying++;
            try { change(); next = field.Capture(); }
            finally
            {
                try { Project(field, previous); }
                finally { _applying--; }
            }
            if (next != previous) ThrowError(await Submit(field, next, operation.Token));
        }
        finally { field.Gate.Release(); }
    }

    internal void CheckFields(bool indirectOnly)
    {
        foreach (var field in _fields.Entries.Values)
        {
            if (_writes[field].Error != null || (indirectOnly && !field.NeedsCheckpoint)) continue;
            CaptureAssignment(field);
        }
    }

    internal async Task AwaitWrites()
    {
        List<Task<Exception>> pending = new List<Task<Exception>>();
        foreach (var writes in _writes.Values) pending.Add(writes.Tail);
        Task<Exception[]> receipts = Task.WhenAll(pending);
        if (!receipts.IsCompleted) await Task.WhenAny(receipts, _closing.Task);
        if (_closed) throw new OperationCanceledException("The storage session closed.");
        foreach (var error in await receipts) ThrowError(error);
        foreach (var writes in _writes.Values) ThrowError(writes.Error);
    }

    private static void ThrowError(Exception error)
    {
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        _closing.TrySetResult(true);
        _lifetime.Cancel();
        try
        {
            foreach (var subscription in _subscriptions) subscription.Dispose();
        }
        finally
        {
            _subscriptions.Clear();
            foreach (var field in _fields.Entries.Values) Project(field, field.Authored);
            _lifetime.Dispose();
        }
    }
}
