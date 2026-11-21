using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Qin.UnitySamples.ServerSync
{
    public sealed class HttpSyncProvider : IServerSyncProvider
    {
        private readonly HttpClient _client;
        private readonly Uri _endpoint;

        public HttpSyncProvider(HttpClient client, Uri endpoint)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            if (endpoint == null || !endpoint.IsAbsoluteUri || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException("Supply an absolute HTTP sync endpoint.", nameof(endpoint));

            _endpoint = endpoint;
        }

        public async Task<SyncDocument> ReadAsync(string documentKey, CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, DocumentUri(documentKey));
            using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;

            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return new SyncDocument(json, ReadRevision(response));
        }

        public async Task<SyncDocument> WriteAsync(string documentKey, string json, string expectedRevision, CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, DocumentUri(documentKey))
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            if (expectedRevision == null)
                request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Any);
            else
                request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(expectedRevision));

            using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.PreconditionFailed) throw new SyncConflictException();

            response.EnsureSuccessStatusCode();
            return new SyncDocument(json, ReadRevision(response));
        }

        private Uri DocumentUri(string documentKey) => new UriBuilder(_endpoint)
        {
            Query = "document=" + Uri.EscapeDataString(documentKey)
        }.Uri;

        private static string ReadRevision(HttpResponseMessage response)
        {
            EntityTagHeaderValue tag = response.Headers.ETag;
            if (tag == null || tag.IsWeak || tag.Equals(EntityTagHeaderValue.Any))
                throw new InvalidOperationException("The sync endpoint must return a strong ETag.");

            return tag.ToString();
        }
    }
}
