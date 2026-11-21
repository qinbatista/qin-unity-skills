using System;
using System.Threading;
using System.Threading.Tasks;

namespace Qin.UnitySamples.ServerSync
{
    public interface IServerSyncProvider
    {
        Task<SyncDocument> ReadAsync(string documentKey, CancellationToken cancellationToken);

        // A null expected revision creates the document only if it is absent.
        Task<SyncDocument> WriteAsync(string documentKey, string json, string expectedRevision, CancellationToken cancellationToken);
    }

    public sealed class SyncDocument
    {
        public string Json { get; }
        public string Revision { get; }

        public SyncDocument(string json, string revision)
        {
            if (string.IsNullOrWhiteSpace(revision)) throw new ArgumentException("A saved document needs a revision.", nameof(revision));

            Json = json ?? throw new ArgumentNullException(nameof(json));
            Revision = revision;
        }
    }

    public sealed class SyncConflictException : InvalidOperationException
    {
        public SyncConflictException() : base("The stored document changed. Load its current version before saving again.") { }
    }
}
