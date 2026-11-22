using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public abstract class SOLocalData : ScriptableObject, ISerializationCallbackReceiver
{
    [SerializeField, HideInInspector] private string _fileName;
    [SerializeField, HideInInspector] private string _timeStamp;
    [SerializeField, HideInInspector] private string _hashValue;
    [NonSerialized] private bool _writeBlocked;
    [NonSerialized] private float _lastAutoSaveTime = float.NegativeInfinity;

    protected string FileName { get => _fileName; set => _fileName = value; }

    private string FilePath
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_fileName)) SetPathName();
            if (string.IsNullOrWhiteSpace(_fileName)) _fileName = GetType().Name;
            bool hasSeparator = _fileName.Contains("/") || _fileName.Contains("\\");
            if (hasSeparator || _fileName == "." || _fileName == ".." || _fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException("The save name must be a single file name.");
            string root = Application.persistentDataPath;
#if UNITY_EDITOR
            string previewRoot = Environment.GetEnvironmentVariable("AI_MANAGER_PERSISTENCE_ROOT");
            if (!string.IsNullOrEmpty(previewRoot)) root = previewRoot;
#endif
            if (string.IsNullOrEmpty(root) || !Path.IsPathRooted(root)) throw new IOException("Local storage requires an absolute, writable save directory.");
            return Path.Combine(root, _fileName);
        }
    }

    public virtual void OnEnable()
    {
        if (Application.isPlaying) LoadLocalData();
    }

    public virtual void OnBeforeSerialize() { }
    // code-gate: semantic-boundary=Unity deserialization restores the subclass's stable file name.
    public virtual void OnAfterDeserialize() => SetPathName();

    public abstract void SetPathName();
    public abstract void InitialValue();

    protected virtual void BeforeLocalDataLoad() { }
    protected virtual void AfterLocalDataLoad() { }
    protected virtual void PrepareForLocalSerialization() { }
    protected virtual void RestoreAfterLocalSerialization() { }
    protected virtual string SerializeLocalData() => JsonUtility.ToJson(this, true);

    protected virtual bool RestoreLocalData(string savedData)
    {
        _hashValue = string.Empty;
        JsonUtility.FromJsonOverwrite(savedData, this);
        string storedHash = _hashValue;
        _hashValue = string.Empty;
        try
        {
            PrepareForLocalSerialization();
            return storedHash == GetSHA256(SerializeLocalData());
        }
        finally
        {
            RestoreAfterLocalSerialization();
            _hashValue = storedHash;
        }
    }

    protected string GetSHA256(string text)
    {
        using SHA256 algorithm = SHA256.Create();
        return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string ReadSavedData(string path)
    {
        using FileStream stream = File.OpenRead(path);
        if (stream.ReadByte() != 0)
        {
            stream.Position = 0;
            using StreamReader reader = new StreamReader(stream, Encoding.UTF8, true);
            return reader.ReadToEnd();
        }

        // The zero byte opens the legacy stream header. Decode its single Base64 string directly.
        using BinaryReader legacy = new BinaryReader(stream, Encoding.UTF8);
        int rootId = legacy.ReadInt32();
        int headerId = legacy.ReadInt32();
        int majorVersion = legacy.ReadInt32();
        int minorVersion = legacy.ReadInt32();
        if (rootId <= 0 || headerId != -1 || majorVersion != 1 || minorVersion != 0) throw new InvalidDataException("Invalid legacy save header.");
        if (legacy.ReadByte() != 6 || legacy.ReadInt32() != rootId) throw new InvalidDataException("Expected one legacy save string.");
        string encoded = legacy.ReadString();
        if (legacy.ReadByte() != 11 || stream.Position != stream.Length) throw new InvalidDataException("Invalid legacy save ending.");
        return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
    }

    [ContextMenu("Load")]
    public void LoadLocalData()
    {
        _writeBlocked = true;
        string path = FilePath;
        string fileName = _fileName;
        string previous = JsonUtility.ToJson(this);
        string savedData = null;
        try
        {
            BeforeLocalDataLoad();
            try { savedData = ReadSavedData(path); }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }

            if (savedData == null) InitialValue();
            else if (!RestoreLocalData(savedData)) throw new InvalidDataException($"Save checksum mismatch at '{path}'.");
            AfterLocalDataLoad();
        }
        catch
        {
            JsonUtility.FromJsonOverwrite(previous, this);
            throw;
        }
        finally
        {
            _fileName = fileName;
        }
        _writeBlocked = false;
        if (savedData == null) SaveDataAtLocal();
    }

    [ContextMenu("Save")]
    public void SaveDataAtLocal()
    {
        if (_writeBlocked) throw new InvalidOperationException("Reload or explicitly reset the unreadable save before writing.");
        string path = FilePath;
        string directory = Path.GetDirectoryName(path);
        string temporaryPath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        string previousTime = _timeStamp;
        string previousHash = _hashValue;
        try
        {
            string json;
            try
            {
                PrepareForLocalSerialization();
                _timeStamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                _hashValue = string.Empty;
                _hashValue = GetSHA256(SerializeLocalData());
                json = SerializeLocalData();
            }
            finally
            {
                RestoreAfterLocalSerialization();
            }
            Directory.CreateDirectory(directory);
            File.WriteAllText(temporaryPath, json, Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temporaryPath, path, null);
            else File.Move(temporaryPath, path);
        }
        catch
        {
            _timeStamp = previousTime;
            _hashValue = previousHash;
            throw;
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    [ContextMenu("Clean")]
    public void Clean()
    {
        _writeBlocked = true;
        InitialValue();
        _writeBlocked = false;
        SaveDataAtLocal();
    }

    protected void RequestAutoSave(float cooldownSeconds = 0.5f)
    {
        if (_writeBlocked || !Application.isPlaying || Time.unscaledTime - _lastAutoSaveTime < cooldownSeconds) return;
        SaveDataAtLocal();
        _lastAutoSaveTime = Time.unscaledTime;
    }

    public void SaveWhenOwnerDestroyed()
    {
        if (Application.isPlaying && !_writeBlocked) SaveDataAtLocal();
    }
}
