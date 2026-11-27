#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)
#define SO_WINDOWS
#elif UNITY_EDITOR_OSX || (!UNITY_EDITOR && (UNITY_IOS || UNITY_STANDALONE_OSX))
#define SO_DARWIN
#elif UNITY_EDITOR_LINUX || (!UNITY_EDITOR && (UNITY_ANDROID || UNITY_STANDALONE_LINUX))
#define SO_LINUX
#endif
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

// Use the native filesystem on desktop, iOS and Android. Browser storage needs a transaction acknowledgement.
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
            using FileStream writer = AcquireWriter(metadata, cancellation);
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

    private static FileStream AcquireWriter(string metadata, CancellationToken cancellation)
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

    private static FileStream OpenWriter(string path)
    {
#if SO_DARWIN || SO_LINUX
        FileStream stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        // The native nonblocking lock also protects against a second process or Editor.
        if (flock(stream.SafeFileHandle.DangerousGetHandle().ToInt32(), 2 | 4) == 0) return stream;
        int error = Marshal.GetLastWin32Error();
        stream.Dispose();
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
            if (privateKey && fchmod(file.SafeFileHandle.DangerousGetHandle().ToInt32(), 384) != 0) throw new IOException("Could not set private SO key permissions (errno " + Marshal.GetLastWin32Error() + ").");
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
#if UNITY_IOS && !UNITY_EDITOR
    private const string NativeLibrary = "__Internal";
#elif SO_DARWIN
    private const string NativeLibrary = "libSystem.B.dylib";
#else
    private const string NativeLibrary = "libc";
#endif
    [DllImport(NativeLibrary, SetLastError = true)] private static extern int flock(int descriptor, int operation);
    [DllImport(NativeLibrary, SetLastError = true)] private static extern int fchmod(int descriptor, uint permissions);
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
