using System;
using System.Threading;
using System.Threading.Tasks;

public enum SODataMode
{
    None = 0,
    LocalFile = 1,
    DirectAccess = 2,
    DirectAccessOnline = 3,
    RealtimeOnline = 4
}

public sealed class SODataRecord
{
    public string Value { get; }
    public string Revision { get; }

    public SODataRecord(string value, string revision)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        if (string.IsNullOrEmpty(revision)) throw new ArgumentException("Missing storage revision.", nameof(revision));
        Revision = revision;
    }
}

public sealed class SODataConflictException : InvalidOperationException
{
    public SODataConflictException(string message) : base(message) { }
}

// One provider instance belongs to one authenticated identity/environment or local installation.
public interface ISODataKeyStore
{
    // A confirmed missing key returns null. A failed read throws.
    Task<SODataRecord> ReadAsync(string scope, string key, CancellationToken cancellation);

    // Null expectedRevision means insert only. A stale revision must leave the winner intact.
    Task<SODataRecord> WriteAsync(string scope, string key, string value, string expectedRevision, CancellationToken cancellation);
}

// Acknowledges writes only after the native database or filesystem transaction commits.
public interface ISOLocalDataStore : ISODataKeyStore { }

public interface ISORealtimeDataStore : ISODataKeyStore
{
    // Deliver an initial value (including null) before completion, then ordered changes on the Unity thread.
    // Dispose to detach. The provider owns its transport and the authority to write.
    Task<IDisposable> SubscribeAsync(string scope, string key, Action<SODataRecord> receive, CancellationToken cancellation);
}
