#if SO_DATA_UNITY_CLOUD_SAVE
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Internal;
using UnityEngine;

// Immutable player files are published through a conditional Cloud Save head record.
public sealed class UnityCloudSaveFileStore : ISODataFileStore
{
    [Serializable] private sealed class Head { public string file; public string hash; }
    private IPlayerFilesService _files;
    private ISODataKeyStore _heads;
    private readonly Func<string> _authorizedPlayerId;
    private string _playerId;
    public Exception CleanupError { get; private set; }

    public UnityCloudSaveFileStore() => _authorizedPlayerId = () => AuthenticationService.Instance.IsAuthorized ? AuthenticationService.Instance.PlayerId : null;

    public UnityCloudSaveFileStore(IPlayerFilesService files, ISODataKeyStore heads, Func<string> authorizedPlayerId)
    {
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _heads = heads ?? throw new ArgumentNullException(nameof(heads));
        _authorizedPlayerId = authorizedPlayerId ?? throw new ArgumentNullException(nameof(authorizedPlayerId));
        _playerId = _authorizedPlayerId();
    }

    private void CheckPlayer()
    {
        string player = _authorizedPlayerId();
        if (string.IsNullOrEmpty(player)) throw new InvalidOperationException("Authentication is not ready for player-file synchronization.");
        if (_playerId == null) _playerId = player;
        if (player != _playerId) throw new UnauthorizedAccessException("The file session changed. Reopen this player's slot.");
        if (_files == null) _files = CloudSaveService.Instance.Files.Player;
        if (_heads == null) _heads = new UnityCloudSaveKeyStore();
    }

    public async Task<string> GetRevisionAsync(string scope, CancellationToken cancellation)
    {
        CheckPlayer();
        SODataRecord record = await _heads.ReadAsync(scope, "file", cancellation);
        CheckPlayer();
        cancellation.ThrowIfCancellationRequested();
        return record?.Revision;
    }

    public async Task<SODataFileRecord> ReadAsync(string scope, CancellationToken cancellation)
    {
        CheckPlayer();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            cancellation.ThrowIfCancellationRequested();
            SODataRecord record = await _heads.ReadAsync(scope, "file", cancellation);
            CheckPlayer();
            if (record == null) return null;
            Head head = await Task.Run(() => Decode(scope, record.Value));
            CheckPlayer();
            byte[] content;
            try { content = await _files.LoadBytesAsync(head.file); }
            catch (CloudSaveException)
            {
                SODataRecord latest = await _heads.ReadAsync(scope, "file", cancellation);
                if (latest?.Revision != record.Revision) continue;
                throw;
            }
            CheckPlayer();
            cancellation.ThrowIfCancellationRequested();
            string hash = await Task.Run(() => SOFileEnvelope.Revision(content));
            CheckPlayer();
            cancellation.ThrowIfCancellationRequested();
            if (hash != head.hash) throw new System.IO.InvalidDataException("The remote file checksum does not match its acknowledged head.");
            return new SODataFileRecord(content, record.Revision);
        }
        throw new SODataConflictException("The remote file changed repeatedly while it was being read.");
    }

    public async Task<SODataFileRecord> WriteAsync(string scope, byte[] content, string expectedRevision, CancellationToken cancellation)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        CheckPlayer();
        cancellation.ThrowIfCancellationRequested();
        string hash = await Task.Run(() => SOFileEnvelope.Revision(content));
        CheckPlayer();
        SODataRecord previous = await _heads.ReadAsync(scope, "file", cancellation);
        CheckPlayer();
        cancellation.ThrowIfCancellationRequested();
        Head old = previous == null ? null : Decode(scope, previous.Value);
        if (old?.hash == hash) return new SODataFileRecord(content, previous.Revision);
        if (previous?.Revision != expectedRevision) throw new SODataConflictException("The remote file changed. Read its current head before retrying.");
        Head next = new Head { file = Prefix(scope) + Guid.NewGuid().ToString("N"), hash = hash };
        SODataRecord accepted;
        try
        {
            await _files.SaveAsync(next.file, content);
            CheckPlayer();
            accepted = await _heads.WriteAsync(scope, "file", JsonUtility.ToJson(next), expectedRevision, cancellation);
        }
        catch
        {
            SODataRecord current = await _heads.ReadAsync(scope, "file", CancellationToken.None);
            CheckPlayer();
            if (current != null && Decode(scope, current.Value).file == next.file) accepted = current;
            else { await DeleteObsoleteAsync(next.file); throw; }
        }
        CheckPlayer();
        if (old != null) await DeleteObsoleteAsync(old.file);
        CheckPlayer();
        return new SODataFileRecord(content, accepted.Revision);
    }

    private static string Prefix(string scope) => "so_file_v1_" + UnityCloudSaveKeyStore.GetCloudKey(scope, "file") + "_";

    private static Head Decode(string scope, string json)
    {
        Head head = JsonUtility.FromJson<Head>(json);
        if (head == null || head.file == null || !head.file.StartsWith(Prefix(scope), StringComparison.Ordinal) || head.hash?.Length != 64) throw new System.IO.InvalidDataException("Invalid remote file head.");
        return head;
    }

    private async Task DeleteObsoleteAsync(string file)
    {
        try { CheckPlayer(); await _files.DeleteAsync(file); CheckPlayer(); CleanupError = null; }
        catch (Exception error) { CleanupError = error; }
    }
}
#endif
