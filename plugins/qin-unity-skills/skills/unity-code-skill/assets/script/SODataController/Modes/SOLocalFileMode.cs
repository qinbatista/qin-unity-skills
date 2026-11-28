#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)
#define SO_WINDOWS
#elif UNITY_EDITOR_OSX || (!UNITY_EDITOR && (UNITY_IOS || UNITY_STANDALONE_OSX))
#define SO_DARWIN
#elif UNITY_EDITOR_LINUX || (!UNITY_EDITOR && (UNITY_ANDROID || UNITY_STANDALONE_LINUX))
#define SO_LINUX
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Serialization;

// Whole-domain local snapshots and the native encrypted file format shared with DirectAccess.
internal sealed class SOLocalFileMode : SOStorageMode
{
    private readonly string _path;
    private string _revision;
    private JObject _snapshot;
    private const string Format = "$sodata";
    private static readonly HashSet<SOLocalFileMode> Pending = new HashSet<SOLocalFileMode>();
    private static CancellationTokenSource _clock;
    private static Task _checkpointLoop = Task.CompletedTask;
    private readonly Dictionary<SOStoredField, string> _committed = new Dictionary<SOStoredField, string>();
    private readonly HashSet<SOStoredField> _dirty = new HashSet<SOStoredField>();
    private Batch _writing;

    private sealed class Batch
    {
        internal readonly KeyValuePair<SOStoredField, string>[] Fields;
        internal readonly Task<(string Revision, string Json)> Receipt;

        internal Batch(SOLocalFileMode mode)
        {
            Fields = mode._dirty.Select(field => new KeyValuePair<SOStoredField, string>(field, field.Visible)).ToArray();
            string revision = mode._revision;
            Receipt = Task.Run(() => mode.Commit(Fields, revision));
        }
    }

    internal static string Root
    {
        get
        {
            string path = Application.persistentDataPath;
#if UNITY_EDITOR
            path = Environment.GetEnvironmentVariable("AI_MANAGER_PERSISTENCE_ROOT") ?? path;
#endif
            if (string.IsNullOrEmpty(path) || !Path.IsPathRooted(path)) throw new IOException("Expected an absolute save directory.");
            return path;
        }
    }

    internal SOLocalFileMode(SODataController owner, string scope, string slot) : base(owner)
    {
        string name = owner.SnapshotFileName;
        if (string.IsNullOrWhiteSpace(name) || name.Contains("/") || name.Contains("\\")
            || name == "." || name == ".." || name == ".sodata" || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidDataException("Expected one legacy save file name.");
        _path = owner.PreviousFileName != null && slot == "default" ? Path.Combine(Root, name)
            : Path.Combine(Root, "SOData", "profiles", SOFileEnvelope.Revision(Encoding.UTF8.GetBytes(scope)), name);
    }

    internal override Task OpenAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string text = SOFileStorage.ReadSnapshot(_path, out _revision);
        _snapshot = text == null ? new JObject() : ReadSnapshot(text);
        foreach (var field in Owner.Fields)
        {
            if (_snapshot.TryGetValue(field.Key, out var value)) field.Apply(SOStoredField.Wrap(value));
            else
            {
                value = _snapshot[field.Member.Name];
                if (value == null)
                    foreach (FormerlySerializedAsAttribute alias in field.Member.GetCustomAttributes(typeof(FormerlySerializedAsAttribute), false))
                        if ((value = _snapshot[alias.oldName]) != null) break;
                if (value != null) field.Apply(SOStoredField.Wrap(value));
                _snapshot[field.Key] = SOStoredField.Unwrap(field.Visible);
            }
            _committed.Add(field, field.Visible);
        }
        _snapshot[Format] = 1;
        return Task.CompletedTask;
    }

    private static JObject ReadSnapshot(string text)
    {
        var doc = SOStoredField.ParseObject(text);
        if (doc.TryGetValue(Format, out var version))
        {
            if (version.Type != JTokenType.Integer || (int)version != 1) throw new InvalidDataException("Unsupported SO snapshot version.");
            return doc;
        }
        // Previous writers signed the exact UTF-8 JSON with the signature field set to an empty string.
        var signature = (string)(doc["_hashValue"] ?? doc["checksum"]);
        if (signature == null || signature.Length != 64 ||
            SOFileEnvelope.Revision(Encoding.UTF8.GetBytes(text.Replace(signature, ""))) != signature)
            throw new InvalidDataException("Invalid legacy SO snapshot checksum.");
        if (doc["data"]?.Type == JTokenType.String && doc["checksum"] != null && (int?)doc["version"] == 1)
            return SOStoredField.ParseObject((string)doc["data"]);
        if (doc["checksum"] != null && doc["version"] != null && (int?)doc["version"] != 1 && (int?)doc["version"] != 2)
            throw new InvalidDataException("Unsupported legacy SO snapshot version.");
        doc.Remove("_hashValue");
        doc.Remove("checksum");
        return doc;
    }

    protected override void Save(SOStoredField field, string wire)
    {
        if (wire == _committed[field]) _dirty.Remove(field);
        else _dirty.Add(field);
        UpdateSchedule();
    }

    // Only one worker touches this JSON tree. Gameplay continues to edit the SO on the Unity thread.
    private (string Revision, string Json) Commit(KeyValuePair<SOStoredField, string>[] fields, string revision)
    {
        JToken[] originals = new JToken[fields.Length];
        int applied = 0;
        try
        {
            foreach (var item in fields)
            {
                originals[applied] = _snapshot[item.Key.Key];
                _snapshot[item.Key.Key] = SOStoredField.Unwrap(item.Value);
                applied++;
            }
            string json = _snapshot.ToString(Formatting.None);
            return (SOFileStorage.WriteSnapshot(_path, json, revision), json);
        }
        catch
        {
            for (int i = 0; i < applied; i++) _snapshot[fields[i].Key.Key] = originals[i];
            throw;
        }
    }

    private Batch StartBatch()
    {
        if (_writing == null && _dirty.Count > 0) _writing = new Batch(this);
        return _writing;
    }

    private void FinishBatch(Batch batch)
    {
        if (_writing != batch) return;
        string json;
        try
        {
            var result = batch.Receipt.GetAwaiter().GetResult();
            _revision = result.Revision;
            json = result.Json;
        }
        catch (Exception error)
        {
            _writing = null;
            if (error is SODataConflictException) Unschedule();
            else UpdateSchedule();
            Owner.Failed(error);
            throw;
        }
        _writing = null;
        foreach (var item in batch.Fields)
        {
            _committed[item.Key] = item.Value;
            if (item.Key.Visible == item.Value) _dirty.Remove(item.Key);
            else _dirty.Add(item.Key);
        }
        Owner.ClearStorageError();
        UpdateSchedule();
        StopClockIfIdle();
        Owner.NotifySnapshotCommitted(json);
        foreach (var item in batch.Fields) Owner.Changed(item.Key.Key);
    }

    private async Task FlushBatchAsync(Batch batch)
    {
        try { await batch.Receipt; }
        finally { FinishBatch(batch); }
    }

    internal override async Task FlushAsync()
    {
        Batch batch;
        while ((batch = StartBatch()) != null) await FlushBatchAsync(batch);
    }

    internal void FlushBeforeClose()
    {
        Batch batch;
        while ((batch = StartBatch()) != null) FinishBatch(batch);
    }

    private void UpdateSchedule()
    {
        if (_dirty.Count == 0 && _writing == null)
        {
            // Repeated edits and reversions can share the rest of this window without timer churn.
            Pending.Remove(this);
            return;
        }
        if (Owner.StorageError is SODataConflictException) { Unschedule(); return; }
        if (!Pending.Add(this) || !_checkpointLoop.IsCompleted) return;
        _clock = new CancellationTokenSource();
        Application.focusChanged += OnFocus;
        Application.quitting += FlushPending;
        _checkpointLoop = RunClockAsync(_clock.Token);
    }

    private static async Task RunClockAsync(CancellationToken token)
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), token);
                token.ThrowIfCancellationRequested();
                if (Pending.Count == 0) { StopClockIfIdle(); return; }
                foreach (var mode in Pending.ToArray())
                {
                    token.ThrowIfCancellationRequested();
                    var batch = mode.StartBatch();
                    if (batch == null) continue;
                    try { await mode.FlushBatchAsync(batch); }
                    catch (Exception) { /* FinishBatch reports the error and retains dirty values. */ }
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private static void OnFocus(bool focused) { if (!focused) FlushPending(); }

    private static void FlushPending()
    {
        foreach (var mode in Pending.ToArray())
            try { mode.FlushBeforeClose(); }
            catch (Exception error) { Debug.LogException(error); }
    }

    private void Unschedule()
    {
        Pending.Remove(this);
        StopClockIfIdle();
    }

    private static void StopClockIfIdle()
    {
        if (Pending.Count != 0 || _clock == null) return;
        Application.focusChanged -= OnFocus;
        Application.quitting -= FlushPending;
        var clock = _clock;
        _clock = null;
        _checkpointLoop = Task.CompletedTask;
        clock.Cancel();
        clock.Dispose();
    }

    public override void Dispose()
    {
        // A submitted atomic replacement may already be on disk. Join it before the slot reopens.
        if (_writing != null)
        {
            try { _writing.Receipt.GetAwaiter().GetResult(); }
            catch (Exception error) { Owner.Failed(error); }
            _writing = null;
        }
        _dirty.Clear();
        Unschedule();
    }
}

internal static class SOFileEnvelope
{
    public const string Header = "SODATA/1\n";
    private const int DigestSize = 16;
    private const int IVSize = 16;
    private const int TagSize = 32;
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

    public static byte[] NewDeviceKey()
    {
        using RandomNumberGenerator random = RandomNumberGenerator.Create();
        byte[] key = new byte[64]; // Independent AES-256 and HMAC-SHA256 keys.
        random.GetBytes(key);
        return key;
    }

    public static string Revision(byte[] bytes)
    {
        using SHA256 sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
    }

    public static bool HasEnvelope(byte[] bytes) => bytes.Length >= 7 && Encoding.ASCII.GetString(bytes, 0, 7) == "SODATA/";

    public static byte[] Encode(string fileName, string value, byte[] key)
    {
        byte[] payload = Utf8.GetBytes(value);
        byte[] clear = new byte[DigestSize + payload.Length];
        using MD5 md5 = MD5.Create();
        Buffer.BlockCopy(md5.ComputeHash(payload), 0, clear, 0, DigestSize);
        Buffer.BlockCopy(payload, 0, clear, DigestSize, payload.Length);
        using Aes aes = CreateCipher(key);
        aes.GenerateIV();
        using ICryptoTransform encryptor = aes.CreateEncryptor();
        byte[] cipher = encryptor.TransformFinalBlock(clear, 0, clear.Length);
        int signedLength = IVSize + cipher.Length;
        byte[] blob = new byte[signedLength + TagSize];
        Buffer.BlockCopy(aes.IV, 0, blob, 0, IVSize);
        Buffer.BlockCopy(cipher, 0, blob, IVSize, cipher.Length);
        Buffer.BlockCopy(ComputeTag(fileName, key, blob, signedLength), 0, blob, signedLength, TagSize);
        return Utf8.GetBytes(Header + Convert.ToBase64String(blob));
    }

    public static string Decode(string fileName, byte[] bytes, byte[] key)
    {
        try
        {
            string text = Utf8.GetString(bytes);
            if (!text.StartsWith(Header, StringComparison.Ordinal)) throw new InvalidDataException("Unsupported SO data envelope version.");
            string encoded = text.Substring(Header.Length);
            byte[] blob = Convert.FromBase64String(encoded);
            int cipherLength = blob.Length - IVSize - TagSize;
            if (cipherLength < 32 || cipherLength % 16 != 0 || Convert.ToBase64String(blob) != encoded) throw new InvalidDataException("Invalid SO data envelope.");
            int signedLength = IVSize + cipherLength;
            byte[] tag = ComputeTag(fileName, key, blob, signedLength);
            if (!CryptographicOperations.FixedTimeEquals(tag, new ReadOnlySpan<byte>(blob, signedLength, TagSize))) throw new InvalidDataException("SO data authentication failed.");
            using Aes aes = CreateCipher(key);
            byte[] iv = new byte[IVSize];
            Buffer.BlockCopy(blob, 0, iv, 0, IVSize);
            aes.IV = iv;
            using ICryptoTransform decryptor = aes.CreateDecryptor();
            byte[] clear = decryptor.TransformFinalBlock(blob, IVSize, cipherLength);
            if (clear.Length < DigestSize) throw new InvalidDataException("Truncated SO data payload.");
            using MD5 md5 = MD5.Create();
            byte[] digest = md5.ComputeHash(clear, DigestSize, clear.Length - DigestSize);
            if (!CryptographicOperations.FixedTimeEquals(digest, new ReadOnlySpan<byte>(clear, 0, DigestSize))) throw new InvalidDataException("SO data checksum mismatch.");
            return Utf8.GetString(clear, DigestSize, clear.Length - DigestSize);
        }
        catch (Exception error) when (error is FormatException || error is DecoderFallbackException || error is CryptographicException)
        {
            throw new InvalidDataException("Invalid encrypted SO data.", error);
        }
    }

    private static Aes CreateCipher(byte[] key)
    {
        byte[] cipherKey = new byte[32];
        Buffer.BlockCopy(key, 0, cipherKey, 0, cipherKey.Length);
        Aes aes = Aes.Create();
        aes.Key = cipherKey;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        return aes;
    }

    private static byte[] ComputeTag(string fileName, byte[] key, byte[] blob, int count)
    {
        byte[] signingKey = new byte[32];
        Buffer.BlockCopy(key, 32, signingKey, 0, signingKey.Length);
        using HMACSHA256 mac = new HMACSHA256(signingKey);
        // Bind both format and leaf name. Copying a valid balance over a different key must fail.
        byte[] label = Utf8.GetBytes(Header + fileName + "\0");
        mac.TransformBlock(label, 0, label.Length, label, 0);
        mac.TransformBlock(blob, 0, count, blob, 0);
        mac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return mac.Hash;
    }

    public static string ReadLegacyJson(byte[] bytes)
    {
        try
        {
            if (bytes.Length > 0 && bytes[0] == 0) bytes = UnwrapLegacyString(bytes);
            string json = Utf8.GetString(bytes);
            if (json.Length > 0 && json[0] == '\uFEFF') json = json.Substring(1);
            if (!json.TrimStart().StartsWith("{", StringComparison.Ordinal)) throw new InvalidDataException("Expected a legacy JSON object.");
            return json;
        }
        catch (Exception error) when (error is EndOfStreamException || error is DecoderFallbackException || error is FormatException)
        {
            throw new InvalidDataException("Invalid legacy SO data.", error);
        }
    }

    private static byte[] UnwrapLegacyString(byte[] bytes)
    {
        // Read just the historical single-string record. No object deserialization takes place.
        using MemoryStream stream = new MemoryStream(bytes, false);
        stream.Position = 1;
        using BinaryReader reader = new BinaryReader(stream, Utf8);
        int rootId = reader.ReadInt32();
        if (rootId <= 0 || reader.ReadInt32() != -1 || reader.ReadInt32() != 1 || reader.ReadInt32() != 0) throw new InvalidDataException("Invalid legacy save header.");
        if (reader.ReadByte() != 6 || reader.ReadInt32() != rootId) throw new InvalidDataException("Expected one legacy save string.");
        byte[] payload = Convert.FromBase64String(reader.ReadString());
        if (reader.ReadByte() != 11 || stream.Position != stream.Length) throw new InvalidDataException("Invalid legacy save ending.");
        return payload;
    }
}

static class SOFileStorage
{
    private static readonly object WriterGate = new object();
    private static readonly byte[] KeyCreatedMarker = { 1 };

    public static string ReadSnapshot(string path) => ReadSnapshot(path, out string revision);
    public static string ReadSnapshot(string path, out string revision) => Read(path, true, out revision);

    public static string WriteSnapshot(string path, string payload) => Commit(path, payload, null, false, true, CancellationToken.None);
    public static string WriteSnapshot(string path, string payload, string expectedRevision) => Commit(path, payload, expectedRevision, true, true, CancellationToken.None);

    public static string Read(string path, bool allowLegacy, out string revision)
    {
        string directory = GetDirectory(path);
        byte[] bytes = ReadIfPresent(path);
        revision = null;
        if (bytes == null) return null;
        string value;
        if (SOFileEnvelope.HasEnvelope(bytes)) value = SOFileEnvelope.Decode(Path.GetFileName(path), bytes, ReadDeviceKey(directory, false));
        else if (allowLegacy) value = SOFileEnvelope.ReadLegacyJson(bytes);
        else throw new InvalidDataException("An individual SO key requires an authenticated record.");
        revision = SOFileEnvelope.Revision(bytes);
        return value;
    }

    public static string Commit(string path, string payload, string expectedRevision, bool compareRevision, bool allowLegacy, CancellationToken cancellation)
    {
        if (payload == null) throw new ArgumentNullException(nameof(payload));
        string directory = GetDirectory(path);
        string metadata = Path.Combine(directory, ".sodata");
        lock (WriterGate)
        {
            cancellation.ThrowIfCancellationRequested();
            EnsureDirectory(metadata);
            using IDisposable writer = AcquireWriter(metadata, cancellation);
            byte[] previous = ReadIfPresent(path);
            string previousRevision = previous == null ? null : SOFileEnvelope.Revision(previous);
            if (compareRevision && previousRevision != expectedRevision) throw new SODataConflictException("The saved SO data changed. Reload before retrying.");
            bool encrypted = previous != null && SOFileEnvelope.HasEnvelope(previous);
            if (previous != null && !encrypted && !allowLegacy) throw new InvalidDataException("Invalid native SO key record.");
            byte[] key = ReadDeviceKey(directory, !encrypted);
            string fileName = Path.GetFileName(path);
            // A replaced key must not silently re-encrypt previously acknowledged data.
            if (encrypted && compareRevision) SOFileEnvelope.Decode(fileName, previous, key);
            byte[] encoded = SOFileEnvelope.Encode(fileName, payload, key);
            cancellation.ThrowIfCancellationRequested();
            WriteAtomic(metadata, path, encoded, previous != null);
            return SOFileEnvelope.Revision(encoded);
        }
    }

    public static void RequireNativePlatform()
    {
#if !SO_WINDOWS && !SO_DARWIN && !SO_LINUX
        throw new PlatformNotSupportedException("Native SO file storage requires a supported desktop, iOS or Android filesystem.");
#endif
    }

    private static string GetDirectory(string path)
    {
        RequireNativePlatform();
        if (string.IsNullOrEmpty(path) || !Path.IsPathRooted(path) || string.IsNullOrEmpty(Path.GetFileName(path))) throw new ArgumentException("Expected an absolute SO save file path.", nameof(path));
        return Path.GetDirectoryName(Path.GetFullPath(path));
    }

    private static byte[] ReadIfPresent(string path)
    {
        try
        {
            // A reader may finish the old inode while a writer publishes its replacement.
            using FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using BinaryReader reader = new BinaryReader(file);
            int length = checked((int)file.Length);
            byte[] result = reader.ReadBytes(length);
            if (result.Length != length) throw new EndOfStreamException("Truncated SO data file.");
            return result;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static byte[] ReadDeviceKey(string directory, bool allowCreation)
    {
        string metadata = Path.Combine(directory, ".sodata");
        string keyPath = Path.Combine(metadata, "device.key");
        string markerPath = Path.Combine(metadata, "initialized");
        byte[] key = ReadIfPresent(keyPath);
        if (key == null)
        {
            if (!allowCreation || ReadIfPresent(markerPath) != null) throw new InvalidDataException("The SO device key is missing. Restore the matching .sodata directory.");
            key = SOFileEnvelope.NewDeviceKey();
            WriteAtomic(metadata, keyPath, key, false, true);
        }
        if (key.Length != 64) throw new InvalidDataException("Invalid SO device key.");
        if (allowCreation && ReadIfPresent(markerPath) == null) WriteAtomic(metadata, markerPath, KeyCreatedMarker, false);
        return key;
    }

    private static void EnsureDirectory(string directory)
    {
        if (Directory.Exists(directory)) return;
        string parent = Path.GetDirectoryName(directory);
        if (parent == null) throw new DirectoryNotFoundException(directory);
        EnsureDirectory(parent);
        Directory.CreateDirectory(directory);
        SyncDirectory(parent);
    }

    private static IDisposable AcquireWriter(string metadata, CancellationToken cancellation)
    {
        string path = Path.Combine(metadata, "writer.lock");
        for (int attempt = 0; ; attempt++)
        {
            cancellation.ThrowIfCancellationRequested();
            try { return OpenWriter(path); }
            catch (IOException error) when (attempt < 100 && IsBusy(error))
            {
                // Await short competing commits before reading the expected revision. Never retry a disk error.
                Thread.Sleep(10);
            }
        }
    }

    private static bool IsBusy(IOException error)
    {
        int code = error.HResult & 0xffff;
        return code == 32 || code == 33 || code == 11 || code == 35;
    }

    private static IDisposable OpenWriter(string path)
    {
#if SO_DARWIN || SO_LINUX
        // IL2CPP FileStream handles are runtime objects. Acquire and release the actual POSIX descriptor here.
        const int readWrite = 2;
        int descriptor = open(path, readWrite);
        if (descriptor < 0 && Marshal.GetLastWin32Error() == 2)
        {
            // Leave the lock file in place. Its kernel lock is released when its descriptor closes.
            using (new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite)) { }
            descriptor = open(path, readWrite);
        }
        if (descriptor < 0) throw new IOException("Could not open the SO writer lock (errno " + Marshal.GetLastWin32Error() + ").");
        if (flock(descriptor, 2 | 4) == 0) return new NativeWriterLock(descriptor);
        int error = Marshal.GetLastWin32Error();
        close(descriptor);
        throw new IOException("The SO storage writer lock is unavailable (errno " + error + ").", error);
#else
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
#endif
    }

    private static void WriteAtomic(string metadata, string path, byte[] bytes, bool replace, bool privateKey = false)
    {
        // Only the writer lock owner may use this staging name. An interrupted write is discarded on the next commit.
        string pending = Path.Combine(metadata, "pending");
        File.Delete(pending);
        using (FileStream file = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
#if SO_DARWIN || (SO_LINUX && (!UNITY_ANDROID || UNITY_EDITOR))
            // Android's emulated, application-specific storage enforces access independently of POSIX mode bits.
            if (privateKey && chmod(pending, 384) != 0) throw new IOException("Could not set private SO key permissions (errno " + Marshal.GetLastWin32Error() + ").");
#endif
            file.Write(bytes, 0, bytes.Length);
            file.Flush(true);
        }
#if SO_WINDOWS
        const int writeThrough = 8;
        const int replaceExisting = 1;
        if (!MoveFileExW(pending, path, writeThrough | (replace ? replaceExisting : 0))) throw new IOException("Could not commit SO data (Win32 " + Marshal.GetLastWin32Error() + ").");
#else
        if (replace) File.Replace(pending, path, null);
        else File.Move(pending, path);
        string directory = Path.GetDirectoryName(path);
        SyncDirectory(directory);
        if (directory != metadata) SyncDirectory(metadata);
#endif
    }

    private static void SyncDirectory(string directory)
    {
#if SO_DARWIN || SO_LINUX
        int handle = open(directory, 0);
        if (handle < 0) throw new IOException("Could not open the SO directory for sync (errno " + Marshal.GetLastWin32Error() + ").");
        try
        {
            if (fsync(handle) != 0) throw new IOException("Could not sync the SO directory (errno " + Marshal.GetLastWin32Error() + ").");
#if SO_DARWIN
            // Request a device-cache flush on Apple storage when the volume supports it.
            if (fcntl(handle, 51) != 0)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 22 && error != 45) throw new IOException("Could not flush the SO volume (errno " + error + ").");
            }
#endif
        }
        finally { close(handle); }
#endif
    }

#if SO_DARWIN || SO_LINUX
    private sealed class NativeWriterLock : IDisposable
    {
        private int _descriptor;
        public NativeWriterLock(int descriptor) => _descriptor = descriptor;

        public void Dispose()
        {
            int descriptor = Interlocked.Exchange(ref _descriptor, -1);
            if (descriptor >= 0) close(descriptor);
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    private const string NativeLibrary = "__Internal";
#elif SO_DARWIN
    private const string NativeLibrary = "libSystem.B.dylib";
#else
    private const string NativeLibrary = "libc";
#endif
    [DllImport(NativeLibrary, SetLastError = true)] private static extern int flock(int descriptor, int operation);
#if SO_DARWIN
    [DllImport(NativeLibrary, SetLastError = true)] private static extern int chmod(string path, ushort permissions);
#elif !UNITY_ANDROID || UNITY_EDITOR
    [DllImport(NativeLibrary, SetLastError = true)] private static extern int chmod(string path, uint permissions);
#endif
    [DllImport(NativeLibrary, SetLastError = true)] private static extern int open(string path, int flags);
    [DllImport(NativeLibrary, SetLastError = true)] private static extern int fsync(int descriptor);
    [DllImport(NativeLibrary)] private static extern int close(int descriptor);
#if SO_DARWIN
    [DllImport(NativeLibrary, SetLastError = true)] private static extern int fcntl(int descriptor, int operation);
#endif
#elif SO_WINDOWS
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileExW(string source, string destination, int flags);
#endif
}
