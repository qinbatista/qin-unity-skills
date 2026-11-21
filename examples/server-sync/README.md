# Server Sync

This Unity example saves one player document through two provider implementations. The game selects a provider during setup and uses the same load and save calls in both cases.

| Owner | Responsibility |
| --- | --- |
| Game startup or settings | Select the provider and open a session for the player's save slot. |
| [ServerSyncManager.cs](Assets/Scripts/ServerSync/ServerSyncManager.cs) | Bind the session to one provider and document. Return a snapshot and require that snapshot at save time. |
| [IServerSyncProvider.cs](Assets/Scripts/ServerSync/IServerSyncProvider.cs) | Define the read and conditional write contract. |
| [MemorySyncProvider.cs](Assets/Scripts/ServerSync/Providers/MemorySyncProvider.cs) | Demonstrate a locked in-memory document store with numeric revisions. |
| [HttpSyncProvider.cs](Assets/Scripts/ServerSync/Providers/HttpSyncProvider.cs) | Map the contract to an HTTP document endpoint with strong ETags. The application supplies the configured `HttpClient`. |

## Choose the provider

The caller reads the provider key from its own configuration. This example uses the in-memory implementation by default.

```csharp
using HttpClient client = new HttpClient();

Dictionary<string, IServerSyncProvider> providers = new Dictionary<string, IServerSyncProvider>
{
    ["memory"] = new MemorySyncProvider(),
    ["http"] = new HttpSyncProvider(client, new Uri("https://sync.example.com/saves"))
};

string providerKey = "memory";
ServerSyncManager sync = new ServerSyncManager(providers[providerKey], "player-01/progress");
SyncSnapshot loaded = await sync.LoadAsync();
SyncSnapshot saved = await sync.SaveAsync(loaded, "{\"coins\":120}");
```

The game keeps the returned snapshot as the basis for its next save. A provider change opens a new session and loads that provider's document. Each provider owns its own records and revision tokens.

A missing document yields a snapshot with a null `Document`. The next write attempts to create it. An existing document is replaced only when its stored revision still matches the snapshot. `SyncConflictException` tells the caller to load the current document before deciding what to save. After an uncertain write result, read back the stored document before retrying.

## HTTP adapter contract

The configured HTTP endpoint reads and writes raw JSON using the `document` query key. The application's HTTP client supplies any authentication. The server compares the expected revision and commits the update in one atomic operation.

| Operation | Request | Response |
| --- | --- | --- |
| Read | `GET` | `200` with JSON and a strong `ETag`, or `404` for a missing document |
| Create | `PUT` with `If-None-Match: *` | A successful response with the new `ETag`, or `412` when a document already exists |
| Update | `PUT` with `If-Match: <previous ETag>` | A successful response with the new `ETag`, or `412` on a revision conflict |

## Verify provider selection

[ServerSyncTests.cs](Assets/Editor/Tools/ServerSync/Tests/ServerSyncTests.cs) exercises provider selection, independent save records, stale writes from concurrent clients and readback after an uncertain response. The HTTP tests use a simulated service. The source project targets Unity 6000.6.7f1.

Run from the repository root using a project copy in the task-owned Cache and the [Unity CLI workflow](../../unity-code-skill/references/unity-cli.md). Use `py -3` in Windows PowerShell.

```text
python3 -B unity-code-skill/scripts/unity_cli_daily.py test "<task-project>" --non-interactive --no-pager --mode EditMode --filter ServerSyncTests --output "<task-cache>/sync-results.xml" -- -nographics -logFile "<task-cache>/sync-unity.log"
```
