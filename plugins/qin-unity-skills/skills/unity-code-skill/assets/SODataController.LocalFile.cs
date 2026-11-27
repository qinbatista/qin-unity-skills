using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public partial class SODataController
{
    // Unity's default legacy serializer includes these three fields in the signed representation.
    [SerializeField, HideInInspector] private string _fileName;
    [SerializeField, HideInInspector] private string _timeStamp;
    [SerializeField, HideInInspector] private string _hashValue;
    [NonSerialized] private string _snapshotRevision;
    [NonSerialized] private bool _writeBlocked;
    [NonSerialized] private bool _snapshotTypesChecked;
    [NonSerialized] private float _lastAutoSaveTime = float.NegativeInfinity;

    protected string FileName { get => _fileName; set => _fileName = value; }
    protected string LocalChecksum => _hashValue;
    protected virtual bool UsesLegacyPath => false;
    public virtual void SetPathName() { }
    public virtual void InitialValue()
    {
        RestoreOwner(_snapshotDefaults);
    }
    protected virtual void BeforeLocalDataLoad() { }
    protected virtual void AfterLocalDataLoad() { }
    protected virtual void PrepareForLocalSerialization() { }
    protected virtual void RestoreAfterLocalSerialization() { }

    private static string PersistenceRoot
    {
        get
        {
            string root = Application.persistentDataPath;
#if UNITY_EDITOR
            string preview = Environment.GetEnvironmentVariable("AI_MANAGER_PERSISTENCE_ROOT");
            if (!string.IsNullOrEmpty(preview)) root = preview;
#endif
            if (string.IsNullOrEmpty(root) || !Path.IsPathRooted(root))
                throw new IOException("Local storage requires an absolute, writable save directory.");
            return root;
        }
    }

    private string FilePath
    {
        get
        {
            if (_scopedDirectory == null && !UsesLegacyPath) throw new InvalidOperationException("Open the local profile first.");
            if (string.IsNullOrWhiteSpace(_fileName)) SetPathName();
            if (string.IsNullOrWhiteSpace(_fileName)) _fileName = Owner.GetType().Name;
            if (_fileName.Contains("/") || _fileName.Contains("\\") || _fileName == "." || _fileName == ".." ||
                _fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || _fileName == ".sodata")
                throw new InvalidDataException("Expected a single save file name.");
            return Path.Combine(_scopedDirectory ?? PersistenceRoot, _fileName);
        }
    }

    // Exclude the new Inspector configuration from the legacy checksum and from restored data.
    private static readonly Regex InspectorLines = new Regex("(?m)^    \"_(?:storageMode|storageId)\": [^\\r\\n]*(?:\\r?\\n|$)");
    [Serializable]
    private sealed class AttachedSnapshot
    {
        public int version;
        public string data;
        public string checksum;
    }

    protected virtual string SerializeLocalData()
    {
        if (!_snapshotTypesChecked && !UsesLegacyPath)
        {
            SODataFields.CheckSnapshot(Owner.GetType());
            _snapshotTypesChecked = true;
        }
        if (AttachedPlain)
            return JsonUtility.ToJson(new AttachedSnapshot { version = 1, data = JsonUtility.ToJson(Owner), checksum = _hashValue });
        return InspectorLines.Replace(JsonUtility.ToJson(this, true), string.Empty).Replace(",\n}", "\n}");
    }

    protected virtual bool RestoreLocalData(string savedData)
    {
        if (AttachedPlain)
        {
            var snapshot = JsonUtility.FromJson<AttachedSnapshot>(savedData);
            if (snapshot == null || snapshot.version != 1 || snapshot.data == null || JsonUtility.ToJson(snapshot) != savedData)
                throw new InvalidDataException("Unsupported attached SO snapshot.");
            string signature = snapshot.checksum;
            snapshot.checksum = string.Empty;
            if (GetSHA256(JsonUtility.ToJson(snapshot)) != signature) return false;
            SODataFields.CheckSnapshot(Owner.GetType());
            RestoreOwner(snapshot.data);
            _hashValue = signature;
            return true;
        }
        _hashValue = string.Empty;
        JsonUtility.FromJsonOverwrite(savedData, this);
        string stored = _hashValue;
        _hashValue = string.Empty;
        try
        {
            PrepareForLocalSerialization();
            return GetSHA256(SerializeLocalData()) == stored;
        }
        finally
        {
            RestoreAfterLocalSerialization();
            _hashValue = stored;
        }
    }

    protected string GetSHA256(string text)
    {
        using SHA256 algorithm = SHA256.Create();
        return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty).ToLowerInvariant();
    }

    private bool UsesSnapshot()
    {
        if (Mode == SODataMode.None) return false;
        if (Mode != SODataMode.LocalFile) throw new InvalidOperationException("Keyed modes require a keyed commit.");
        return true;
    }

    [ContextMenu("Load")]
    public void LoadLocalData()
    {
        if (!UsesSnapshot()) return;
        _writeBlocked = true;
        string path = FilePath;
        string name = _fileName;
        string storageId = _storageId;
        SODataMode mode = _storageMode;
        string previous = JsonUtility.ToJson(Owner);
        _muting++;
        try
        {
            BeforeLocalDataLoad();
            string data = SOFileStorage.ReadSnapshot(path, out string revision);
            if (data == null) InitialValue();
            else if (!RestoreLocalData(data)) throw new InvalidDataException("Save checksum mismatch at " + path);
            AfterLocalDataLoad();
            _snapshotRevision = revision;
            if (data == null) WriteSnapshot(path, false);
            _writeBlocked = false;
            StorageError = null;
        }
        catch
        {
            RestoreOwner(previous);
            throw;
        }
        finally
        {
            _storageMode = mode;
            _storageId = storageId;
            _fileName = name;
            _muting--;
        }
        if (_scopedDirectory != null) _snapshotAccepted = JsonUtility.ToJson(Owner);
    }

    [ContextMenu("Save")]
    public void SaveDataAtLocal()
    {
        if (!UsesSnapshot()) return;
        if (_writeBlocked) throw new InvalidOperationException("Reload or explicitly reset the unreadable save before writing.");
        WriteSnapshot(FilePath, false);
    }

    private void WriteSnapshot(string path, bool reset)
    {
        string previousTime = _timeStamp;
        string previousHash = _hashValue;
        _muting++;
        try
        {
            string data;
            try
            {
                PrepareForLocalSerialization();
                _timeStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                _hashValue = string.Empty;
                _hashValue = GetSHA256(SerializeLocalData());
                data = SerializeLocalData();
            }
            finally { RestoreAfterLocalSerialization(); }
            _snapshotRevision = reset ? SOFileStorage.WriteSnapshot(path, data) : SOFileStorage.WriteSnapshot(path, data, _snapshotRevision);
            if (_scopedDirectory != null) _snapshotAccepted = JsonUtility.ToJson(Owner);
            StorageError = null;
        }
        catch
        {
            _timeStamp = previousTime;
            _hashValue = previousHash;
            throw;
        }
        finally { _muting--; }
    }

    [ContextMenu("Clean")]
    public void Clean()
    {
        if (!UsesSnapshot()) return;
        string path = FilePath;
        string previous = JsonUtility.ToJson(Owner);
        _writeBlocked = true;
        _muting++;
        try
        {
            InitialValue();
            WriteSnapshot(path, true);
            _writeBlocked = false;
        }
        catch { RestoreOwner(previous); throw; }
        finally { _muting--; }
    }

    protected void RequestAutoSave(float cooldownSeconds = 0.5f)
    {
        if (Mode == SODataMode.None || _writeBlocked || !Application.isPlaying || Time.unscaledTime - _lastAutoSaveTime < cooldownSeconds) return;
        SaveDataAtLocal();
        _lastAutoSaveTime = Time.unscaledTime;
    }

    public void SaveWhenOwnerDestroyed()
    {
        if (Mode == SODataMode.LocalFile && Application.isPlaying && !_writeBlocked) SaveDataAtLocal();
    }
}
