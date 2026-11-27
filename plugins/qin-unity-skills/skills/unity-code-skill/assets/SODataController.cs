using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public abstract partial class SODataController : ScriptableObject, ISerializationCallbackReceiver
{
    [SerializeField] private SODataMode _storageMode = SODataMode.None;
    [SerializeField, Tooltip("Stable domain ID within the profile. Empty uses the concrete type name.")]
    private string _storageId;
    [NonSerialized] private SOKeySession _keys;
    [NonSerialized] private string _scopedDirectory;
    [NonSerialized] private string _snapshotDefaults;

    public virtual SODataMode Mode => _storageMode;
    public event Action<string> DataChanged;

    public string GetStorageScope(string profile)
    {
        if (string.IsNullOrWhiteSpace(profile)) throw new ArgumentException("A profile or match ID is required.", nameof(profile));
        string domain = string.IsNullOrEmpty(_storageId) ? GetType().Name : _storageId;
        return Uri.EscapeDataString(profile) + "/" + Uri.EscapeDataString(domain);
    }

    protected virtual void DefineDataKeys(SODataFields fields) { }
    public virtual void OnEnable() { }
    // code-gate: semantic-boundary=Unity disable retires the active profile and its subscriptions.
    protected virtual void OnDisable() => CloseData();
    public virtual void OnBeforeSerialize() { }
    public virtual void OnAfterDeserialize() { }

    // Call on a runtime clone from the Unity thread. Keep the authored asset as the defaults.
    public async Task OpenAsync(string profile, ISODataKeyStore provider = null, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        if (_keys != null || _scopedDirectory != null) throw new InvalidOperationException("Close the current profile before opening another.");
        if (Mode == SODataMode.None) return;
        string scope = GetStorageScope(profile);
        switch (Mode)
        {
            case SODataMode.LocalFile:
                _snapshotDefaults = JsonUtility.ToJson(this);
                _scopedDirectory = Path.Combine(PersistenceRoot, "SOData", "profiles", GetSHA256(scope));
                try { LoadLocalData(); }
                catch { CloseData(); throw; }
                return;
            case SODataMode.DirectAccess:
                if (provider == null) provider = new SOFileKeyStore(Path.Combine(PersistenceRoot, "SOData", "keys"));
                else if (!(provider is ISOLocalDataStore)) throw new ArgumentException("DirectAccess requires a local transaction store.", nameof(provider));
                break;
            case SODataMode.DirectAccessOnline:
                if (provider == null) throw new ArgumentNullException(nameof(provider));
                break;
            case SODataMode.RealtimeOnline:
                if (!(provider is ISORealtimeDataStore)) throw new ArgumentException("A realtime provider is required.", nameof(provider));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(Mode));
        }

        SODataFields fields = new SODataFields();
        DefineDataKeys(fields);
        fields.CheckCoverage(GetType());
        SOKeySession session = new SOKeySession(provider, scope, fields, NotifyChange);
        _keys = session;
        try { await session.OpenAsync(Mode == SODataMode.RealtimeOnline ? (ISORealtimeDataStore)provider : null, cancellation); }
        catch
        {
            if (ReferenceEquals(_keys, session)) CloseData();
            throw;
        }
    }

    // The existing mutation must synchronously change only the named field or value record.
    // Publish gameplay effects after awaiting the commit.
    public Task CommitChangeAsync(string key, Action change, CancellationToken cancellation = default)
    {
        if (change == null) throw new ArgumentNullException(nameof(change));
        cancellation.ThrowIfCancellationRequested();
        if (Mode == SODataMode.None)
        {
            change();
            NotifyChange(key);
            return Task.CompletedTask;
        }
        if (Mode == SODataMode.LocalFile)
        {
            if (_scopedDirectory == null) throw new InvalidOperationException("Open the local profile before committing.");
            string previous = JsonUtility.ToJson(this);
            try { change(); SaveDataAtLocal(); }
            catch { JsonUtility.FromJsonOverwrite(previous, this); throw; }
            NotifyChange(key);
            return Task.CompletedTask;
        }
        if (_keys == null) throw new InvalidOperationException("Open the data owner before committing a key.");
        return _keys.ChangeAsync(key, change, cancellation);
    }

    private void NotifyChange(string key)
    {
        try { DataChanged?.Invoke(key); }
        catch (Exception error) { Debug.LogException(error); }
    }

    public void CloseData()
    {
        var session = _keys;
        _keys = null;
        session?.Dispose();
        if (_scopedDirectory == null) return;
        _scopedDirectory = null;
        JsonUtility.FromJsonOverwrite(_snapshotDefaults, this);
        _snapshotDefaults = null;
        _snapshotRevision = null;
    }
}
