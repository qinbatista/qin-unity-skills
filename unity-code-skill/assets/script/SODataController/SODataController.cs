using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Serialization;

public enum SODataMode { None, LocalFile, DirectAccess, DirectAccessOnline, RealtimeOnline, LocalFileOnline }

public abstract class SODataController : ScriptableObject
{
    [SerializeField] private SODataMode _storageMode;
    [SerializeField, Tooltip("Stable domain ID within a slot. Empty uses this SO's type name.")]
    private string _storageId;
    [SerializeField, HideInInspector] private string _fileName; // Published scoped snapshot name.
    [NonSerialized] private readonly Dictionary<string, SOStoredField> _fields = new Dictionary<string, SOStoredField>();
    [NonSerialized] private SOStorageMode _active;
    [NonSerialized] private bool _ready;
    [NonSerialized] private bool _closing;
    [NonSerialized] private int _epoch;
    private static int _playEpoch;
    private string _defaults;
    private Task _opening = Task.CompletedTask;
    private Task _retiring = Task.CompletedTask;
    private string _lifetimeKey;
    private static readonly Dictionary<string, Task> Retirements = new Dictionary<string, Task>();
    private readonly Dictionary<string, string> _staged = new Dictionary<string, string>();
    [Serializable] private sealed class StagedValue<T> { public T value; }

    public virtual SODataMode? RequiredMode => null;
    public SODataMode Mode => RequiredMode ?? _storageMode;
    public bool IsReady => _ready;
    public Task Initialization => _opening;
    public Task Retirement => _retiring;
    public event Action<string> DataChanged;
    public event Action<string> SnapshotCommitted;
    public event Action<Exception> StorageFailed;
    public Exception StorageError { get; private set; }

    // Existing unscoped LocalFile domains can import their original file in the default slot.
    protected virtual string LegacyFileName => null;
    internal string PreviousFileName => LegacyFileName;
    internal string SnapshotFileName => LegacyFileName ?? (string.IsNullOrEmpty(_fileName) ? GetType().Name : _fileName);
    internal IEnumerable<SOStoredField> Fields => _fields.Values;

    protected T Read<T>(ref T field)
    {
        EnsureReady();
        return field;
    }

    protected void Write<T>(ref T field, T value, [CallerMemberName] string key = null)
    {
        EnsureReady();
        if (_closing) throw new InvalidOperationException("The SO is closing.");
        if (_active == null)
        {
            if (SOStorageMode.Scalar<T>.CanCompare && EqualityComparer<T>.Default.Equals(field, value)) return;
            SOStoredField stored = FindField(key);
            string wire = JsonUtility.ToJson(new StagedValue<T> { value = value });
            if (wire == stored.Visible) return;
            _staged[key] = wire;
            stored.MarkAssigned(wire);
            field = value;
            return;
        }
        _active.Assign(ref field, value, key);
    }

    public string GetStorageScope(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) throw new ArgumentException("A slot is required.", nameof(slot));
        string domain = string.IsNullOrEmpty(_storageId) ? GetType().Name : _storageId;
        return Uri.EscapeDataString(slot) + "/" + Uri.EscapeDataString(domain);
    }

    private void EnsureReady()
    {
        CheckPlaySession();
        if (_active == null)
        {
            if (Mode == SODataMode.DirectAccessOnline || Mode == SODataMode.RealtimeOnline)
            {
                if (_defaults == null) _defaults = JsonUtility.ToJson(this);
                if (_fields.Count == 0) DiscoverFields();
                return;
            }
            Observe(OpenAsync("default"));
        }
    }

    public Task OpenAsync(string slot, ISODataKeyStore provider = null, CancellationToken cancellation = default, ISODataFileStore fileProvider = null)
    {
        CheckPlaySession();
        if (_active != null) throw new InvalidOperationException("Close the current SO slot before opening another.");
        cancellation.ThrowIfCancellationRequested();
        string scope = GetStorageScope(slot);
        SOStorageMode mode;
        switch (Mode)
        {
            case SODataMode.None: mode = new SONoneMode(this); break;
            case SODataMode.LocalFile: mode = new SOLocalFileMode(this, scope, slot); break;
            case SODataMode.DirectAccess: mode = new SODirectAccessMode(this, scope); break;
            case SODataMode.DirectAccessOnline: mode = new SODirectAccessOnlineMode(this, scope, provider); break;
            case SODataMode.RealtimeOnline: mode = new SORealtimeOnlineMode(this, scope, provider); break;
            case SODataMode.LocalFileOnline: mode = new SOLocalFileOnlineMode(this, scope, slot, fileProvider); break;
            default: throw new ArgumentOutOfRangeException(nameof(Mode));
        }
        if (provider != null && Mode != SODataMode.DirectAccessOnline && Mode != SODataMode.RealtimeOnline)
            throw new ArgumentException("Only online modes take a channel.", nameof(provider));
        if (fileProvider != null && Mode != SODataMode.LocalFileOnline) throw new ArgumentException("File providers require LocalFileOnline.", nameof(fileProvider));
        _active = mode;
        StorageError = null;
        _lifetimeKey = SOLocalFileMode.Root + "/" + Mode + "/" + scope;
        Retirements.TryGetValue(_lifetimeKey, out Task previous);
        if (_defaults == null) _defaults = JsonUtility.ToJson(this);
        if (Mode != SODataMode.None && _fields.Count == 0) DiscoverFields();
        mode.BindFields(_fields.Values);
        foreach (KeyValuePair<string, string> staged in _staged) mode.Stage(FindField(staged.Key), staged.Value);
        _staged.Clear();
        _opening = OpenModeAsync(mode, previous, cancellation);
        return _opening;
    }

    private async Task OpenModeAsync(SOStorageMode mode, Task previous, CancellationToken cancellation)
    {
        Exception previousError = StorageError;
        try
        {
            if (!(mode is SONoneMode)) await Task.Yield();
            if (previous != null) await previous;
            cancellation.ThrowIfCancellationRequested();
            await mode.OpenAsync(cancellation);
            if (_active == mode)
            {
                _ready = true;
                if (ReferenceEquals(StorageError, previousError)) ClearStorageError();
            }
        }
        catch (Exception error)
        {
            if (_active == mode) Failed(error);
            throw;
        }
    }

    internal bool IsCurrent(SOStorageMode mode) => _active == mode && this != null;

    private static void Observe(Task work)
    {
        work.GetAwaiter().OnCompleted(() => { if (work.IsFaulted) work.Exception.Handle(error => true); });
    }

    private void DiscoverFields()
    {
        var defaults = SOStoredField.ParseObject(_defaults);
        for (Type type = GetType(); type != typeof(SODataController); type = type.BaseType)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.IsNotSerialized || field.IsInitOnly || (!field.IsPublic && !field.IsDefined(typeof(SerializeField)) && !field.IsDefined(typeof(SerializeReference)))) continue;
                if (!defaults.TryGetValue(field.Name, out var value)) throw new InvalidDataException("Unity cannot serialize " + field.Name);
                var key = field.GetCustomAttribute<SODataKeyAttribute>()?.Key ?? field.Name.TrimStart('_');
                if (string.IsNullOrEmpty(key) || key == "$sodata" || _fields.ContainsKey(key))
                    throw new InvalidDataException("Duplicate or empty SO field key: " + field.Name);
                SOStoredField.CheckType(field, new HashSet<Type>());
                _fields.Add(key, new SOStoredField(this, field, key, value));
            }
        }
    }

    internal SOStoredField FindField(string key)
    {
        if (_fields.TryGetValue(key, out var field)) return field;
        string camel = char.ToLowerInvariant(key[0]) + key.Substring(1);
        if (_fields.TryGetValue(camel, out field)) return field;
        throw new InvalidOperationException("No serialized SO field for property " + key + ". Pass its storage key to Write.");
    }

    public async Task FlushAsync()
    {
        SOStorageMode mode = _active;
        if (mode == null)
        {
            if (_staged.Count != 0) throw new InvalidOperationException("Bind the signed-in slot or spawned match before confirming staged changes.");
            await _retiring;
            return;
        }
        await _opening;
        if (_active == mode) await mode.FlushAsync();
    }

    public async Task RefreshAsync()
    {
        SOStorageMode mode = _active;
        if (mode == null) return;
        if ((_opening.IsFaulted || _opening.IsCanceled) && !(mode is SORealtimeOnlineMode)) _opening = OpenModeAsync(mode, null, CancellationToken.None);
        await _opening;
        if (_active == mode) await mode.RefreshAsync();
    }

    public async Task ResolveFileConflictAsync(SOFileConflictResolution resolution)
    {
        if (!(_active is SOLocalFileOnlineMode files)) throw new InvalidOperationException("A file conflict requires an open LocalFileOnline slot.");
        await _opening;
        if (_active == files) await files.ResolveAsync(resolution);
    }

    public async Task CloseAsync()
    {
        var mode = _active;
        if (mode == null) { await FlushAsync(); return; }
        _closing = true;
        try { await FlushAsync(); }
        catch
        {
            if (_active == mode)
            {
                _closing = false;
            }
            throw;
        }
        if (_active == mode) CloseData(true);
    }

    public void CloseData() => CloseData(false);

    public void CloseData(bool discardLocalChanges)
    {
        var mode = _active;
        if (mode == null && !discardLocalChanges && _staged.Count > 0) return;
        _active = null;
        _closing = _ready = false;
        try
        {
            if (mode != null && !discardLocalChanges)
            {
                TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
                Task prior = Retirements.TryGetValue(_lifetimeKey, out Task existing) ? existing : Task.CompletedTask;
                Task joined = Task.WhenAll(prior, completion.Task);
                Retirements[_lifetimeKey] = joined;
                Observe(joined);
                _retiring = RetireModeAsync(mode, _opening, _lifetimeKey, joined, completion);
                Observe(_retiring);
            }
            else mode?.Dispose();
        }
        finally
        {
            if (_defaults != null) JsonUtility.FromJsonOverwrite(_defaults, this);
            _fields.Clear();
            _staged.Clear();
            _defaults = null;
        }
    }

    private async Task RetireModeAsync(SOStorageMode mode, Task opening, string key, Task joined, TaskCompletionSource<bool> completion)
    {
        try { await opening; await mode.FlushAsync(); completion.TrySetResult(true); await joined; }
        catch (Exception error)
        {
            if (this != null && _active == null) Failed(error);
            completion.TrySetException(error);
            throw;
        }
        finally
        {
            mode.Dispose();
            if (Retirements.TryGetValue(key, out Task current) && current == joined) Retirements.Remove(key);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void StartPlaySession() => _playEpoch++;

    private void CheckPlaySession()
    {
        if (_epoch == _playEpoch) return;
        _epoch = _playEpoch;
        CloseData();
    }

    // code-gate: semantic-boundary=Unity retires the active slot and subscriptions when it disables this SO.
    protected virtual void OnDisable()
    {
        try { CloseData(); }
        catch (Exception error) { Debug.LogException(error); CloseData(true); }
    }
    protected virtual void OnValidate() { if (RequiredMode.HasValue) _storageMode = RequiredMode.Value; }

    internal void Changed(string key)
    {
        try { DataChanged?.Invoke(key); }
        catch (Exception error) { Debug.LogException(error); }
    }

    internal void NotifySnapshotCommitted(string json)
    {
        try { SnapshotCommitted?.Invoke(json); }
        catch (Exception error) { Debug.LogException(error); }
    }

    internal void Failed(Exception error)
    {
        StorageError = error;
        try { StorageFailed?.Invoke(error); }
        catch (Exception listenerError) { Debug.LogException(listenerError); }
    }

    internal void ClearStorageError() => StorageError = null;
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class SODataKeyAttribute : Attribute
{
    public string Key { get; }
    public SODataKeyAttribute(string key) => Key = key;
}

internal sealed class SOStoredField
{
    private readonly SODataController _owner;
    private readonly string _prefix;
    internal readonly FieldInfo Member;
    internal readonly string Key;
    internal readonly string Initial;
    internal string Visible { get; private set; }

    internal SOStoredField(SODataController owner, FieldInfo member, string key, JToken initial)
    {
        _owner = owner;
        Member = member;
        Key = key;
        _prefix = "{" + JsonConvert.ToString(member.Name) + ":";
        Initial = Visible = Wrap(initial);
    }

    internal void MarkAssigned(string wire) => Visible = wire;

    internal static void CheckType(FieldInfo member, HashSet<Type> visited)
    {
        Type type = member.FieldType;
        if (type.IsArray) type = type.GetElementType();
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) type = type.GetGenericArguments()[0];
        if (member.IsDefined(typeof(SerializeReference)) || typeof(UnityEngine.Object).IsAssignableFrom(type))
            throw new InvalidDataException("Persist concrete value data or a stable ID for " + member.Name);
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || !visited.Add(type)) return;
        foreach (var child in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (!child.IsNotSerialized && !child.IsInitOnly && (child.IsPublic || child.IsDefined(typeof(SerializeField)) || child.IsDefined(typeof(SerializeReference))))
                CheckType(child, visited);
    }

    internal static JObject ParseObject(string json)
    {
        using var input = new StringReader(json);
        using var reader = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None };
        var result = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (reader.Read()) throw new InvalidDataException("Unexpected data after the SO record.");
        return result;
    }

    internal static string Wrap(JToken value) => "{\"value\":" + value.ToString(Formatting.None) + "}";
    internal static JToken Unwrap(string wire)
    {
        var box = ParseObject(wire);
        if (box.Count != 1 || !box.TryGetValue("value", out var value)) throw new InvalidDataException("Expected one stored value.");
        return value;
    }

    // Unity writes the backing field directly. Hydration and remote pushes never re-enter the property setter.
    internal void Apply(string wire)
    {
        if (Visible == wire) return;
        JToken value = Validate(wire);
        JsonUtility.FromJsonOverwrite(_prefix + value.ToString(Formatting.None) + "}", _owner);
        Visible = wire;
    }

    internal object DecodeScalar(string wire) => Member.FieldType.IsPrimitive || Member.FieldType.IsEnum || Member.FieldType == typeof(string) ? Validate(wire).ToObject(Member.FieldType) : null;

    internal void Apply(string wire, object scalar)
    {
        if (Visible == wire) return;
        if (!Member.FieldType.IsPrimitive && !Member.FieldType.IsEnum && Member.FieldType != typeof(string)) { Apply(wire); return; }
        Member.SetValue(_owner, scalar);
        Visible = wire;
    }

    private JToken Validate(string wire)
    {
        JToken value = Unwrap(wire);
        var type = Member.FieldType;
        bool valid;
        if (type == typeof(string)) valid = value.Type == JTokenType.String || value.Type == JTokenType.Null;
        else if (type == typeof(bool)) valid = value.Type == JTokenType.Boolean;
        else if (type == typeof(float) || type == typeof(double)) valid = value.Type == JTokenType.Float || value.Type == JTokenType.Integer;
        else if (type.IsPrimitive || type.IsEnum) valid = value.Type == JTokenType.Integer;
        else if (type.IsArray || typeof(System.Collections.IList).IsAssignableFrom(type)) valid = value.Type == JTokenType.Array;
        else valid = value.Type == JTokenType.Object || value.Type == JTokenType.Null;
        if (!valid) throw new InvalidDataException("Incompatible stored value for " + Key);
        return value;
    }
}

internal abstract class SOStorageMode : IDisposable
{
    [Serializable] private sealed class Box<T> { public T value; }
    protected readonly SODataController Owner;
    protected SOStoredField[] Fields { get; private set; } = Array.Empty<SOStoredField>();
    protected SOStorageMode(SODataController owner) => Owner = owner;
    public void BindFields(IEnumerable<SOStoredField> fields) => Fields = fields.ToArray();
    public void Stage(SOStoredField field, string wire) { field.MarkAssigned(wire); Save(field, wire); }
    internal virtual Task OpenAsync(CancellationToken token) => Task.CompletedTask;

    internal virtual void Assign<T>(ref T current, T value, string key)
    {
        if (Scalar<T>.CanCompare && EqualityComparer<T>.Default.Equals(current, value)) return;
        var field = Owner.FindField(key);
        string wire = JsonUtility.ToJson(new Box<T> { value = value });
        if (!wire.StartsWith("{\"value\":", StringComparison.Ordinal)) throw new InvalidDataException("Unsupported SO value: " + typeof(T));
        if (wire == field.Visible) return;
        string previous = field.Visible;
        current = value;
        field.MarkAssigned(wire);
        try { Save(field, wire); }
        catch
        {
            if (field.Visible == wire) field.Apply(previous);
            throw;
        }
    }

    // Reassigning a mutated collection must still compare its contents with the last wire value.
    public static class Scalar<T>
    {
        public static readonly bool CanCompare = typeof(T).IsPrimitive || typeof(T).IsEnum || typeof(T) == typeof(string);
    }

    protected virtual void Save(SOStoredField field, string wire) => throw new NotSupportedException();
    internal virtual Task FlushAsync() => Task.CompletedTask;
    internal virtual Task RefreshAsync() => Task.CompletedTask;
    public virtual void Dispose() { }
}
