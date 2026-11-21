using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Qin.UnitySamples.ServerSync
{
    public sealed class MemorySyncProvider : IServerSyncProvider
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, SyncDocument> _documents = new Dictionary<string, SyncDocument>(StringComparer.Ordinal);
        private long _nextRevision;

        public Task<SyncDocument> ReadAsync(string documentKey, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _documents.TryGetValue(documentKey, out SyncDocument document);
                return Task.FromResult(document);
            }
        }

        public Task<SyncDocument> WriteAsync(string documentKey, string json, string expectedRevision, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _documents.TryGetValue(documentKey, out SyncDocument previous);
                if (!string.Equals(previous?.Revision, expectedRevision, StringComparison.Ordinal))
                    return Task.FromException<SyncDocument>(new SyncConflictException());

                string revision = checked(++_nextRevision).ToString(CultureInfo.InvariantCulture);
                SyncDocument written = new SyncDocument(json, revision);
                _documents[documentKey] = written;
                return Task.FromResult(written);
            }
        }
    }
}
