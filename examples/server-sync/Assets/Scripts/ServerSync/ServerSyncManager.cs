using System;
using System.Threading;
using System.Threading.Tasks;

namespace Qin.UnitySamples.ServerSync
{
    public sealed class ServerSyncManager
    {
        private readonly IServerSyncProvider _provider;
        private readonly string _documentKey;
        private readonly Guid _sessionId = Guid.NewGuid();

        public ServerSyncManager(IServerSyncProvider provider, string documentKey)
        {
            if (string.IsNullOrWhiteSpace(documentKey)) throw new ArgumentException("Choose a document key.", nameof(documentKey));

            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _documentKey = documentKey;
        }

        public async Task<SyncSnapshot> LoadAsync(CancellationToken cancellationToken = default)
        {
            SyncDocument document = await _provider.ReadAsync(_documentKey, cancellationToken).ConfigureAwait(false);
            return new SyncSnapshot(_sessionId, document);
        }

        public async Task<SyncSnapshot> SaveAsync(SyncSnapshot basis, string json, CancellationToken cancellationToken = default)
        {
            if (basis.SessionId != _sessionId) throw new ArgumentException("Load the document in this sync session before saving.", nameof(basis));
            if (json == null) throw new ArgumentNullException(nameof(json));

            SyncDocument written = await _provider.WriteAsync(_documentKey, json, basis.Document?.Revision, cancellationToken).ConfigureAwait(false);
            return new SyncSnapshot(_sessionId, written);
        }
    }

    public readonly struct SyncSnapshot
    {
        public Guid SessionId { get; }
        public SyncDocument Document { get; }

        public SyncSnapshot(Guid sessionId, SyncDocument document)
        {
            SessionId = sessionId;
            Document = document;
        }
    }
}
