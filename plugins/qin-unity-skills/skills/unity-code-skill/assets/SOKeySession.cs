using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

internal sealed class SOKeySession : IDisposable
{
    private readonly ISODataKeyStore _store;
    private readonly string _scope;
    private readonly SODataFields _fields;
    private readonly Action<string> _notify;
    private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
    private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
    private bool _closed;
    private bool _ready;
    private Exception _failure;

    internal SOKeySession(ISODataKeyStore store, string scope, SODataFields fields, Action<string> notify)
    {
        _store = store;
        _scope = scope;
        _fields = fields;
        _notify = notify;
    }

    internal async Task OpenAsync(ISORealtimeDataStore realtime, CancellationToken cancellation)
    {
        using var opening = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellation);
        foreach (SODataFields.Entry field in _fields.Entries.Values)
        {
            opening.Token.ThrowIfCancellationRequested();
            if (realtime == null)
                Receive(field, await _store.ReadAsync(_scope, field.Key, opening.Token));
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
        foreach (var field in _fields.Entries.Values)
            field.Apply(field.Accepted?.Value ?? field.Authored);
        _ready = true;
    }

    private void Receive(SODataFields.Entry field, SODataRecord record)
    {
        if (_closed || _failure != null) return;
        if (field.Initialized && field.Accepted?.Revision == record?.Revision) return;
        if (_ready)
        {
            try { field.Apply(record?.Value ?? field.Authored); }
            catch (Exception error) { _failure = error; throw; }
        }
        field.Initialized = true;
        field.Accepted = record;
        field.Notifications++;
        if (_ready) _notify(field.Key);
    }

    internal async Task ChangeAsync(string key, Action change, CancellationToken cancellation)
    {
        if (!_ready || _closed || _failure != null)
            throw new InvalidOperationException("Open a healthy storage session before writing.", _failure);
        if (!_fields.Entries.TryGetValue(key, out var field)) throw new ArgumentException("Unbound storage key: " + key);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellation);
        var token = operation.Token;
        await field.Gate.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            if (_failure != null) throw new InvalidOperationException("Reopen the failed storage session.", _failure);
            string previous = field.Capture();
            string next;
            try
            {
                change();
                next = field.Capture();
            }
            finally { field.Apply(previous); }
            if (next == previous) return;
            int observed = field.Notifications;
            SODataRecord accepted = await _store.WriteAsync(_scope, key, next, field.Accepted?.Revision, token);
            if (accepted == null) throw new InvalidOperationException("Storage did not acknowledge the write.");
            // An acknowledged write is final; caller cancellation cannot turn it into a failed save.
            // A closed session must never project an old account's completion into the new one.
            if (_closed) throw new OperationCanceledException("The storage session closed during the write.");
            if (observed == field.Notifications) Receive(field, accepted);
        }
        finally { field.Gate.Release(); }
    }

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        _lifetime.Cancel();
        try
        {
            foreach (IDisposable subscription in _subscriptions) subscription.Dispose();
        }
        finally
        {
            _subscriptions.Clear();
            foreach (var field in _fields.Entries.Values) field.Apply(field.Authored);
            _lifetime.Dispose();
        }
    }
}
