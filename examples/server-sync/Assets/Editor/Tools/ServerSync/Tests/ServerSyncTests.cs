using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Qin.UnitySamples.ServerSync.Tests
{
    public sealed class ServerSyncTests
    {
        private const string DocumentKey = "player-01/progress slot";

        [UnityTest]
        public IEnumerator OutsideSelectionKeepsProviderDataAndSnapshotsSeparate() => Observe(VerifyOutsideSelection());

        [UnityTest]
        public IEnumerator MemoryRejectsConflictingConcurrentSave() => Observe(VerifyConcurrentWriters(false));

        [UnityTest]
        public IEnumerator HttpRejectsConflictingConcurrentSave() => Observe(VerifyConcurrentWriters(true));

        [UnityTest]
        public IEnumerator LostWriteResponseLeavesTheSavedStateAvailableForReadback() => Observe(VerifyLostWriteResponse());

        private static async Task VerifyOutsideSelection()
        {
            using HttpClient client = new HttpClient(new SimulatedSyncServer());
            Dictionary<string, IServerSyncProvider> providers = new Dictionary<string, IServerSyncProvider>
            {
                ["memory"] = new MemorySyncProvider(),
                ["http"] = new HttpSyncProvider(client, new Uri("http://sync.test/saves"))
            };

            ServerSyncManager memory = new ServerSyncManager(providers["memory"], DocumentKey);
            SyncSnapshot memorySaved = await memory.SaveAsync(await memory.LoadAsync(), "{\"coins\":100}");

            ServerSyncManager http = new ServerSyncManager(providers["http"], DocumentKey);
            SyncSnapshot remoteStart = await http.LoadAsync();
            Assert.That(remoteStart.Document, Is.Null);
            await ExpectFailure<ArgumentException>(() => http.SaveAsync(memorySaved, "{\"coins\":999}"));
            await http.SaveAsync(remoteStart, "{\"coins\":900}");

            ServerSyncManager reselected = new ServerSyncManager(providers["memory"], DocumentKey);
            Assert.That((await reselected.LoadAsync()).Document.Json, Is.EqualTo("{\"coins\":100}"));
            Assert.That((await http.LoadAsync()).Document.Json, Is.EqualTo("{\"coins\":900}"));
        }

        private static async Task VerifyConcurrentWriters(bool useHttp)
        {
            using HttpClient client = new HttpClient(new SimulatedSyncServer());
            IServerSyncProvider provider = useHttp
                ? new HttpSyncProvider(client, new Uri("http://sync.test/saves"))
                : new MemorySyncProvider();
            ServerSyncManager left = new ServerSyncManager(provider, DocumentKey);
            ServerSyncManager right = new ServerSyncManager(provider, DocumentKey);
            await left.SaveAsync(await left.LoadAsync(), "{\"coins\":10}");
            SyncSnapshot leftBasis = await left.LoadAsync();
            SyncSnapshot rightBasis = await right.LoadAsync();
            Assert.That(leftBasis.Document.Revision, Is.EqualTo(rightBasis.Document.Revision));

            Task<bool> leftWrite = Task.Run(() => TrySave(left, leftBasis, "{\"coins\":20}"));
            Task<bool> rightWrite = Task.Run(() => TrySave(right, rightBasis, "{\"coins\":30}"));
            bool[] saved = await Task.WhenAll(leftWrite, rightWrite);

            Assert.That(saved[0] ^ saved[1], Is.True);
            SyncDocument winner = (await left.LoadAsync()).Document;
            Assert.That(winner.Json, Is.EqualTo(saved[0] ? "{\"coins\":20}" : "{\"coins\":30}"));
            Assert.That(winner.Revision, Is.Not.EqualTo(leftBasis.Document.Revision));
        }

        private static async Task VerifyLostWriteResponse()
        {
            SimulatedSyncServer server = new SimulatedSyncServer();
            using HttpClient client = new HttpClient(server);
            ServerSyncManager sync = new ServerSyncManager(new HttpSyncProvider(client, new Uri("http://sync.test/saves")), DocumentKey);
            SyncSnapshot basis = await sync.LoadAsync();
            server.LoseNextWriteResponse = true;

            await ExpectFailure<HttpRequestException>(() => sync.SaveAsync(basis, "{\"coins\":41}"));
            Assert.That(server.CommittedWrites, Is.EqualTo(1));
            Assert.That((await sync.LoadAsync()).Document.Json, Is.EqualTo("{\"coins\":41}"));
            await ExpectFailure<SyncConflictException>(() => sync.SaveAsync(basis, "{\"coins\":99}"));
            Assert.That(server.CommittedWrites, Is.EqualTo(1));
        }

        private static IEnumerator Observe(Task scenario)
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (!scenario.IsCompleted && timer.Elapsed.TotalSeconds < 12)
                yield return null;

            Assert.That(scenario.IsCompleted, Is.True, "The sync scenario must finish.");
            scenario.GetAwaiter().GetResult();
        }

        private static async Task ExpectFailure<TException>(Func<Task> action) where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException)
            {
                return;
            }

            Assert.Fail("Expected " + typeof(TException).Name + ".");
        }

        private static async Task<bool> TrySave(ServerSyncManager sync, SyncSnapshot basis, string json)
        {
            try
            {
                await sync.SaveAsync(basis, json).ConfigureAwait(false);
                return true;
            }
            catch (SyncConflictException)
            {
                return false;
            }
        }

        private sealed class SimulatedSyncServer : HttpMessageHandler
        {
            private const string DocumentQuery = "?document=";
            private readonly object _gate = new object();
            private readonly Dictionary<string, SyncDocument> _documents = new Dictionary<string, SyncDocument>(StringComparer.Ordinal);

            public int CommittedWrites { get; private set; }
            public bool LoseNextWriteResponse { get; set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string query = request.RequestUri.Query;
                if (request.RequestUri.AbsolutePath != "/saves" || !query.StartsWith(DocumentQuery, StringComparison.Ordinal))
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);

                string key = Uri.UnescapeDataString(query.Substring(DocumentQuery.Length));
                string json = request.Method == HttpMethod.Put ? await request.Content.ReadAsStringAsync().ConfigureAwait(false) : null;
                lock (_gate)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _documents.TryGetValue(key, out SyncDocument current);

                    if (request.Method == HttpMethod.Get)
                    {
                        if (current == null) return new HttpResponseMessage(HttpStatusCode.NotFound);

                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(current.Json, Encoding.UTF8, "application/json"),
                            Headers = { ETag = EntityTagHeaderValue.Parse(current.Revision) }
                        };
                    }

                    if (request.Method != HttpMethod.Put) return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);

                    bool matches = current == null
                        ? request.Headers.IfNoneMatch.Contains(EntityTagHeaderValue.Any)
                        : request.Headers.IfMatch.Contains(EntityTagHeaderValue.Parse(current.Revision));
                    if (!matches) return new HttpResponseMessage(HttpStatusCode.PreconditionFailed);

                    CommittedWrites++;
                    string revision = "\"v" + CommittedWrites.ToString(CultureInfo.InvariantCulture) + "\"";
                    _documents[key] = new SyncDocument(json, revision);

                    if (LoseNextWriteResponse)
                    {
                        LoseNextWriteResponse = false;
                        throw new HttpRequestException("Connection closed after the store committed the save.");
                    }

                    return new HttpResponseMessage(HttpStatusCode.NoContent)
                    {
                        Headers = { ETag = EntityTagHeaderValue.Parse(revision) }
                    };
                }
            }
        }
    }
}
