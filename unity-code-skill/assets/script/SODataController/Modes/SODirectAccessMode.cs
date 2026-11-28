using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// Offline keys are queued immediately. FlushAsync confirms their atomic replacements.
internal sealed class SODirectAccessMode : SOStorageMode
{
    private readonly string _scope;
    private readonly string _root;
    private readonly Dictionary<SOStoredField, string> _revisions = new Dictionary<SOStoredField, string>();
    private readonly Dictionary<SOStoredField, string> _pending = new Dictionary<SOStoredField, string>();
    private readonly Queue<SOStoredField> _order = new Queue<SOStoredField>();
    private Task _writing = Task.CompletedTask;
    private bool _loaded;
    private bool _disposed;

    internal SODirectAccessMode(SODataController owner, string scope) : base(owner)
    {
        _scope = scope;
        _root = SOLocalFileMode.Root;
    }

    private string PathFor(string key) => Path.Combine(_root, "SOData", "keys", SOFileEnvelope.Revision(System.Text.Encoding.UTF8.GetBytes(_scope + "\0" + key)) + ".record");

    internal override async Task OpenAsync(CancellationToken token)
    {
        SOStoredField[] fields = Fields;
        foreach (SOStoredField field in fields)
        {
            var saved = await SOFileStorage.RunAsync(PathFor(field.Key), () =>
            {
                string value = SOFileStorage.Read(PathFor(field.Key), false, out string revision);
                return (Value: value, Revision: revision, Scalar: revision == null ? null : field.DecodeScalar(value));
            }, token);
            if (_disposed) return;
            _revisions[field] = saved.Revision;
            if (_pending.ContainsKey(field)) continue;
            if (saved.Revision != null && field.Visible != saved.Value && Owner.IsCurrent(this))
            {
                field.Apply(saved.Value, saved.Scalar);
                Owner.Changed(field.Key);
            }
        }
        _loaded = true;
        if (_pending.Count > 0) Observe(FlushAsync());
    }

    protected override void Save(SOStoredField field, string wire)
    {
        QueueValue(field, wire);
        if (_loaded) Observe(FlushAsync());
    }

    private static void Observe(Task work) => work.GetAwaiter().OnCompleted(() => { if (work.IsFaulted) work.Exception.Handle(error => true); });

    private void QueueValue(SOStoredField field, string wire)
    {
        if (!_pending.ContainsKey(field)) _order.Enqueue(field);
        _pending[field] = wire;
    }

    internal override Task FlushAsync()
    {
        if (_disposed) return Task.CompletedTask;
        if (!_writing.IsCompleted) return _writing;
        _writing = WritePendingAsync();
        return _writing;
    }

    private async Task WritePendingAsync()
    {
        await Task.Yield();
        while (_loaded && !_disposed && _pending.Count > 0)
        {
            SOStoredField field = _order.Dequeue();
            KeyValuePair<SOStoredField, string> item = new KeyValuePair<SOStoredField, string>(field, _pending[field]);
            _pending.Remove(item.Key);
            try
            {
                string revision = await SOFileStorage.RunAsync(PathFor(item.Key.Key), () => SOFileStorage.Commit(PathFor(item.Key.Key), item.Value, _revisions[item.Key], true, false, CancellationToken.None), CancellationToken.None);
                _revisions[item.Key] = revision;
                if (Owner.IsCurrent(this))
                {
                    Owner.ClearStorageError();
                    if (!_pending.ContainsKey(item.Key) && item.Key.Visible == item.Value) Owner.Changed(item.Key.Key);
                }
            }
            catch (Exception error)
            {
                if (error is SODataConflictException)
                {
                    var latest = await SOFileStorage.RunAsync(PathFor(item.Key.Key), () =>
                    {
                        string value = SOFileStorage.Read(PathFor(item.Key.Key), false, out string revision);
                        return (Value: value ?? item.Key.Initial, Revision: revision);
                    }, CancellationToken.None);
                    _revisions[item.Key] = latest.Revision;
                    if (!_pending.ContainsKey(item.Key) && item.Key.Visible == item.Value && Owner.IsCurrent(this))
                    {
                        item.Key.Apply(latest.Value);
                        Owner.Changed(item.Key.Key);
                    }
                }
                else if (!_pending.ContainsKey(item.Key)) QueueValue(item.Key, item.Value);
                if (Owner.IsCurrent(this)) Owner.Failed(error);
                throw;
            }
        }
    }

    public override void Dispose() { _disposed = true; _pending.Clear(); _order.Clear(); }
}
