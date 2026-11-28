using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

// Offline critical data. Each assignment commits one existing-format encrypted key before returning.
internal sealed class SODirectAccessMode : SOStorageMode
{
    private readonly string _scope;
    private readonly Dictionary<SOStoredField, string> _revisions = new Dictionary<SOStoredField, string>();

    internal SODirectAccessMode(SODataController owner, string scope) : base(owner) => _scope = scope;

    private string PathFor(string key) => Path.Combine(SOLocalFileMode.Root, "SOData", "keys",
        SOFileEnvelope.Revision(System.Text.Encoding.UTF8.GetBytes(_scope + "\0" + key)) + ".record");

    internal override Task OpenAsync(CancellationToken token)
    {
        foreach (var field in Owner.Fields)
        {
            token.ThrowIfCancellationRequested();
            string wire = SOFileStorage.Read(PathFor(field.Key), false, out string revision);
            _revisions.Add(field, revision);
            if (revision != null) field.Apply(wire);
        }
        return Task.CompletedTask;
    }

    protected override void Save(SOStoredField field, string wire)
    {
        _revisions[field] = SOFileStorage.Commit(PathFor(field.Key), wire, _revisions[field], true, false, CancellationToken.None);
        Owner.Changed(field.Key);
    }
}
