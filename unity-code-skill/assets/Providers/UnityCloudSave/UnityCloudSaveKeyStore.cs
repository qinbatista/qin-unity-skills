#if SO_DATA_UNITY_CLOUD_SAVE
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Internal;
using Unity.Services.CloudSave.Models;
using PlayerLoadOptions = Unity.Services.CloudSave.Models.Data.Player.LoadOptions;
using PlayerSaveOptions = Unity.Services.CloudSave.Models.Data.Player.SaveOptions;

/// <summary>One signed-in player in one initialized Unity Services environment.</summary>
public sealed class UnityCloudSaveKeyStore : ISODataKeyStore
{
    private const string CreationFence = "so_data_v1_creation_fence";
    private readonly IPlayerDataService _playerData;
    private readonly Func<string> _authorizedPlayerId;
    private readonly string _playerId;

    public UnityCloudSaveKeyStore() : this(CloudSaveService.Instance.Data.Player, CurrentPlayerId) { }

    public UnityCloudSaveKeyStore(IPlayerDataService playerData, Func<string> authorizedPlayerId)
    {
        _playerData = playerData ?? throw new ArgumentNullException(nameof(playerData));
        _authorizedPlayerId = authorizedPlayerId ?? throw new ArgumentNullException(nameof(authorizedPlayerId));
        _playerId = _authorizedPlayerId();
        if (string.IsNullOrWhiteSpace(_playerId)) throw new UnauthorizedAccessException("Sign in before opening the Unity Cloud Save store.");
    }

    private static string CurrentPlayerId() => AuthenticationService.Instance.IsAuthorized ? AuthenticationService.Instance.PlayerId : null;

    public static string GetCloudKey(string scope, string key)
    {
        if (string.IsNullOrWhiteSpace(scope)) throw new ArgumentException("A stable data scope is required.", nameof(scope));
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A stable field key is required.", nameof(key));
        // Length-prefix the scope so different (scope, key) pairs have different inputs.
        string name = scope.Length.ToString(CultureInfo.InvariantCulture) + ":" + scope + key;
        using SHA256 hash = SHA256.Create();
        byte[] digest = hash.ComputeHash(new UTF8Encoding(false, true).GetBytes(name));
        return "so_data_v1_" + BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
    }

    public Task<SODataRecord> ReadAsync(string scope, string key, CancellationToken cancellation)
    {
        return ReadPlayerItemAsync(GetCloudKey(scope, key), cancellation);
    }

    public async Task<SODataRecord> WriteAsync(string scope, string key, string value, string expectedRevision, CancellationToken cancellation)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (expectedRevision != null && expectedRevision.Length == 0) throw new ArgumentException("A known revision must be nonempty.", nameof(expectedRevision));
        string cloudKey = GetCloudKey(scope, key);
        Dictionary<string, SaveItem> batch = new Dictionary<string, SaveItem>();
        if (expectedRevision == null)
        {
            // Read the fence BEFORE checking absence. Every insertion advances it in the same atomic batch.
            SODataRecord fence = await ReadPlayerItemAsync(CreationFence, cancellation);
            string fenceRevision = fence?.Revision;
            if (fence == null)
            {
                // Concurrent bootstrap can only rotate this reserved nonce. No gameplay data is in this write.
                Dictionary<string, SaveItem> seed = new Dictionary<string, SaveItem> { [CreationFence] = new SaveItem(Guid.NewGuid().ToString("N"), null) };
                fenceRevision = await SavePlayerItemsAsync(seed, CreationFence, cancellation);
            }

            if (await ReadPlayerItemAsync(cloudKey, cancellation) != null) throw new SODataConflictException("The cloud item already exists. Read its current revision.");
            batch.Add(CreationFence, new SaveItem(Guid.NewGuid().ToString("N"), fenceRevision));
        }

        batch.Add(cloudKey, new SaveItem(value, expectedRevision));
        string revision = await SavePlayerItemsAsync(batch, cloudKey, cancellation);
        return new SODataRecord(value, revision);
    }

    private async Task<SODataRecord> ReadPlayerItemAsync(string key, CancellationToken cancellation)
    {
        CheckPlayer();
        cancellation.ThrowIfCancellationRequested();
        Dictionary<string, Item> items = await _playerData.LoadAsync(new HashSet<string> { key }, new PlayerLoadOptions());
        CheckPlayer();
        cancellation.ThrowIfCancellationRequested();
        if (!items.TryGetValue(key, out Item item)) return null;
        string value = item.Value.GetAs<string>();
        if (value == null || string.IsNullOrEmpty(item.WriteLock)) throw new InvalidDataException("Unity returned an incomplete Cloud Save item.");
        return new SODataRecord(value, item.WriteLock);
    }

    private async Task<string> SavePlayerItemsAsync(Dictionary<string, SaveItem> batch, string resultKey, CancellationToken cancellation)
    {
        CheckPlayer();
        cancellation.ThrowIfCancellationRequested();
        try
        {
            Dictionary<string, string> revisions = await _playerData.SaveAsync(batch, new PlayerSaveOptions());
            // Once submitted, wait for the acknowledgement even if the original caller cancels.
            CheckPlayer();
            if (!revisions.TryGetValue(resultKey, out string revision) || string.IsNullOrEmpty(revision)) throw new InvalidDataException("Unity did not acknowledge the item revision. Reload before retrying.");
            return revision;
        }
        catch (CloudSaveException exception) when (exception.Reason == CloudSaveExceptionReason.Conflict)
        {
            throw new SODataConflictException("A competing Cloud Save write won. Read the item again before retrying.");
        }
    }

    private void CheckPlayer()
    {
        if (!string.Equals(_playerId, _authorizedPlayerId(), StringComparison.Ordinal)) throw new UnauthorizedAccessException("The Cloud Save session changed. Reopen storage for the signed-in player.");
    }
}
#endif
