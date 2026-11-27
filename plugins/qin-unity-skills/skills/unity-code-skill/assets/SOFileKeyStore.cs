using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// One atomic compare-and-swap record per (domain scope, key). No whole-domain scan on a write.
public sealed class SOFileKeyStore : ISOLocalDataStore
{
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
    private readonly string _directory;

    public SOFileKeyStore(string directory)
    {
        // Fail before scheduling work on targets without a native threadpool/filesystem.
        SOFileStorage.RequireNativePlatform();
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory)) throw new ArgumentException("Expected an absolute native key directory.", nameof(directory));
        _directory = Path.GetFullPath(directory);
    }

    public Task<SODataRecord> ReadAsync(string scope, string key, CancellationToken cancellation)
    {
        return Task.Run(() =>
        {
            string value = SOFileStorage.Read(RecordPath(scope, key), false, out string revision);
            return revision == null ? null : new SODataRecord(value, revision);
        }, cancellation);
    }

    public Task<SODataRecord> WriteAsync(string scope, string key, string value, string expectedRevision, CancellationToken cancellation)
    {
        return Task.Run(() =>
        {
            string revision = SOFileStorage.Commit(RecordPath(scope, key), value, expectedRevision, true, false, cancellation);
            return new SODataRecord(value, revision);
        }, cancellation);
    }

    private string RecordPath(string scope, string key)
    {
        if (string.IsNullOrWhiteSpace(scope) || scope.IndexOf('\0') >= 0) throw new ArgumentException("Expected a nonempty SO data scope without a null character.", nameof(scope));
        if (string.IsNullOrWhiteSpace(key) || key.IndexOf('\0') >= 0) throw new ArgumentException("Expected a nonempty SO data key without a null character.", nameof(key));
        byte[] identity = Utf8.GetBytes(scope + "\0" + key);
        return Path.Combine(_directory, SOFileEnvelope.Revision(identity) + ".record");
    }
}
