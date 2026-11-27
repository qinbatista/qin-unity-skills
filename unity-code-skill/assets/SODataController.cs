using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Storage Controller")]
public partial class SODataController : ScriptableObject, ISerializationCallbackReceiver
{
    [SerializeField, SOStorageMode] private SODataMode _storageMode = SODataMode.None;
    [SerializeField, Tooltip("Stable domain ID within the profile. Empty uses the data object's type name.")]
    private string _storageId;
    [NonSerialized] private ScriptableObject _target;
    [NonSerialized] private SOKeySession _keys;
    [NonSerialized] private string _scopedDirectory;
    [NonSerialized] private string _snapshotDefaults;
    [NonSerialized] private string _snapshotAccepted;
    [NonSerialized] private string _originalFileName;
    [NonSerialized] private int _muting;
    [NonSerialized] private bool _ready;
    [NonSerialized] private bool _closing;

    public virtual SODataMode? RequiredMode => null;
    public virtual SODataMode Mode => RequiredMode ?? _storageMode;
    public Exception StorageError { get; private set; }
    public event Action<string> DataChanged;
    public event Action<Exception> StorageFailed;
    private ScriptableObject Owner => ReferenceEquals(_target, null) ? this : _target;
    private bool AttachedPlain => !ReferenceEquals(Owner, this);

    protected void RequireOpenStorage()
    {
        if (!SODataWriteHooks.IsBound(this)) throw new InvalidOperationException("Open the data profile before changing its value.");
    }

    public string GetStorageScope(string profile)
    {
        if (string.IsNullOrWhiteSpace(profile)) throw new ArgumentException("A profile or match ID is required.", nameof(profile));
        string domain = string.IsNullOrEmpty(_storageId) ? Owner.GetType().Name : _storageId;
        return Uri.EscapeDataString(profile) + "/" + Uri.EscapeDataString(domain);
    }

    // Optional pre-existing key aliases and custom codecs from 1.6. New domains need no overrides.
    protected virtual void DefineDataKeys(SODataFields fields) { }
    public virtual void OnEnable() { }
    // code-gate: semantic-boundary=Unity disable retires the active profile and its subscriptions.
    protected virtual void OnDisable() => CloseData();
    public virtual void OnBeforeSerialize() { }
    public virtual void OnAfterDeserialize() { }

    public Task OpenAsync(string profile, ISODataKeyStore provider = null, CancellationToken cancellation = default) =>
        AttachAsync(this, profile, provider, cancellation);

    // Bind the same runtime object that gameplay already reads and writes, including a plain ScriptableObject.
    public async Task AttachAsync(ScriptableObject target, string profile, ISODataKeyStore provider = null,
        CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (!ReferenceEquals(_target, null)) throw new InvalidOperationException("Close the current data owner first.");
        if (target is SODataController domain && domain.RequiredMode is SODataMode required && Mode != required) throw new InvalidOperationException($"{domain.GetType().Name} requires {required}. The storage controller uses {Mode}.");
        if (Mode == SODataMode.None) return;
        SODataWriteHooks.Bind(target, OnFieldAssigned);
        _target = target;
        _originalFileName = _fileName;
        StorageError = null;
        SOKeySession opening = null;
        try
        {
            string scope = GetStorageScope(profile);
            if (Mode == SODataMode.LocalFile)
            {
                _snapshotDefaults = JsonUtility.ToJson(target);
                _scopedDirectory = Path.Combine(PersistenceRoot, "SOData", "profiles", GetSHA256(scope));
                LoadLocalData();
            }
            else
            {
                var store = SelectKeyStore(provider);
                SODataFields fields = new SODataFields();
                if (target is SODataController previous) previous.DefineDataKeys(fields);
                fields.Discover(target);
                opening = new SOKeySession(store, scope, fields, NotifyChange, ReportError);
                _keys = opening;
                await opening.OpenAsync(Mode == SODataMode.RealtimeOnline ? (ISORealtimeDataStore)store : null, cancellation);
                if (!ReferenceEquals(_keys, opening)) throw new OperationCanceledException("The data owner has changed.");
            }
            _ready = true;
            SODataCheckpoints.Track(this, SODataFields.HasIndirectFields(target.GetType()));
        }
        catch
        {
            if (ReferenceEquals(_target, target) && (opening == null || ReferenceEquals(_keys, opening))) CloseData();
            throw;
        }
    }

    private ISODataKeyStore SelectKeyStore(ISODataKeyStore provider)
    {
        switch (Mode)
        {
            case SODataMode.DirectAccess:
                if (provider == null) return new SOFileKeyStore(Path.Combine(PersistenceRoot, "SOData", "keys"));
                if (provider is ISOLocalDataStore) return provider;
                throw new ArgumentException("DirectAccess requires a local transaction store.", nameof(provider));
            case SODataMode.DirectAccessOnline:
                if (provider is ISOLocalDataStore || provider is ISOMatchSessionStore) throw new ArgumentException("DirectAccessOnline requires a persistent remote key store.", nameof(provider));
                return provider ?? throw new ArgumentNullException(nameof(provider));
            case SODataMode.RealtimeOnline:
                if (provider is ISORealtimeDataStore) return provider;
                throw new ArgumentException("A realtime provider is required.", nameof(provider));
            default:
                throw new ArgumentOutOfRangeException(nameof(Mode));
        }
    }

    private void OnFieldAssigned(string fieldName)
    {
        if (_muting > 0) return;
        if (_keys != null) { _keys.Assigned(fieldName); return; }
        if (_ready) CommitSnapshotChange(fieldName);
    }

    private void CommitSnapshotChange(string fieldName)
    {
        string current = JsonUtility.ToJson(Owner);
        if (current == _snapshotAccepted) return;
        try { SaveDataAtLocal(); }
        catch (Exception error)
        {
            RestoreOwner(_snapshotAccepted);
            ReportError(error);
            throw;
        }
        NotifyChange(fieldName);
    }

    // Compatibility boundary for an explicitly staged and acknowledged 1.6 mutation.
    public Task CommitChangeAsync(string key, Action change, CancellationToken cancellation = default)
    {
        if (change == null) throw new ArgumentNullException(nameof(change));
        cancellation.ThrowIfCancellationRequested();
        if (Mode == SODataMode.None) { change(); NotifyChange(key); return Task.CompletedTask; }
        if (!_ready) throw new InvalidOperationException("Open the data owner before committing.");
        if (_keys != null) return _keys.ChangeAsync(key, change, cancellation);
        string previous = JsonUtility.ToJson(Owner);
        _muting++;
        try { change(); }
        catch { RestoreOwner(previous); throw; }
        finally { _muting--; }
        CommitSnapshotChange(key);
        return Task.CompletedTask;
    }

    // Detect indirect edits (for example List.Add) and await provider writes accepted before this checkpoint.
    public async Task FlushAsync()
    {
        if (Mode == SODataMode.None) return;
        if (!_ready) throw new InvalidOperationException("Open the data owner before flushing.");
        CheckForChanges(false);
        if (_keys != null) await _keys.AwaitWrites();
    }

    internal void CheckForChanges(bool indirectOnly)
    {
        if (!_ready) return;
        if (_target == null) { CloseData(); return; }
        if (_keys != null) _keys.CheckFields(indirectOnly);
        else if (!indirectOnly || StorageError == null) CommitSnapshotChange(null);
    }

    private void RestoreOwner(string json)
    {
        if (json == null || Owner == null) return;
        _muting++;
        try { JsonUtility.FromJsonOverwrite(json, Owner); }
        finally { _muting--; }
    }

    private void NotifyChange(string key)
    {
        try { DataChanged?.Invoke(key); }
        catch (Exception error) { Debug.LogException(error); }
    }

    private void ReportError(Exception error)
    {
        StorageError = error;
        try { StorageFailed?.Invoke(error); }
        catch (Exception observerError) { Debug.LogException(observerError); }
    }

    public void CloseData()
    {
        if (_closing || ReferenceEquals(_target, null)) return;
        _closing = true;
        try
        {
            if (_ready && _target != null && StorageError == null && (_scopedDirectory != null || _keys.WritesThrough))
                CheckForChanges(false);
        }
        finally
        {
            _ready = false;
            SODataCheckpoints.Forget(this);
            SODataWriteHooks.Unbind(_target, OnFieldAssigned);
            var session = _keys;
            _keys = null;
            try
            {
                if (session != null) session.Dispose();
                else if (_scopedDirectory != null) RestoreOwner(_snapshotDefaults);
            }
            finally
            {
                _target = null;
                _scopedDirectory = null;
                _snapshotDefaults = null;
                _snapshotAccepted = null;
                _snapshotRevision = null;
                _snapshotTypesChecked = false;
                _fileName = _originalFileName;
                _closing = false;
            }
        }
    }
}

public sealed class SOStorageModeAttribute : PropertyAttribute { }
