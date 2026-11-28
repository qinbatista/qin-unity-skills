using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Serialization;

public enum SODataMode { None, LocalFile, DirectAccess, DirectAccessOnline, RealtimeOnline }

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

    public virtual SODataMode? RequiredMode => null;
    public SODataMode Mode => RequiredMode ?? _storageMode;
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
                throw new InvalidOperationException("Open the signed-in slot or spawned match before accessing its data.");
            OpenAsync("default").GetAwaiter().GetResult();
        }
        if (!_ready) throw new InvalidOperationException("The SO is opening.");
    }

    public async Task OpenAsync(string slot, ISODataKeyStore provider = null, CancellationToken cancellation = default)
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
            default: throw new ArgumentOutOfRangeException(nameof(Mode));
        }
        if (provider != null && Mode <= SODataMode.DirectAccess)
            throw new ArgumentException("Only online modes take a channel.", nameof(provider));
        _active = mode;
        StorageError = null;
        try
        {
            _defaults = JsonUtility.ToJson(this);
            if (Mode != SODataMode.None) DiscoverFields();
            await mode.OpenAsync(cancellation);
            if (_active != mode) throw new OperationCanceledException("This slot has closed.");
            _ready = true;
        }
        catch
        {
            if (_active == mode) CloseData(true);
            throw;
        }
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

    public Task FlushAsync() => _active?.FlushAsync() ?? Task.CompletedTask;

    public async Task CloseAsync()
    {
        var mode = _active;
        if (mode == null) return;
        _closing = true;
        try { await mode.FlushAsync(); }
        catch
        {
            if (_active == mode)
            {
                if (mode is SOLocalFileMode) _closing = false;
                else CloseData(true);
            }
            throw;
        }
        if (_active == mode) CloseData(true);
    }

    public void CloseData() => CloseData(false);

    public void CloseData(bool discardLocalChanges)
    {
        var mode = _active;
        if (!discardLocalChanges && mode is SOLocalFileMode local)
        {
            _closing = true;
            try { local.FlushBeforeClose(); }
            finally { _closing = false; }
            if (_active != mode) return;
        }
        _active = null;
        _closing = _ready = false;
        try { mode?.Dispose(); }
        finally
        {
            if (_defaults != null) JsonUtility.FromJsonOverwrite(_defaults, this);
            _fields.Clear();
            _defaults = null;
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
        var value = Unwrap(wire);
        var type = Member.FieldType;
        bool valid;
        if (type == typeof(string)) valid = value.Type == JTokenType.String || value.Type == JTokenType.Null;
        else if (type == typeof(bool)) valid = value.Type == JTokenType.Boolean;
        else if (type == typeof(float) || type == typeof(double)) valid = value.Type == JTokenType.Float || value.Type == JTokenType.Integer;
        else if (type.IsPrimitive || type.IsEnum) valid = value.Type == JTokenType.Integer;
        else if (type.IsArray || typeof(System.Collections.IList).IsAssignableFrom(type)) valid = value.Type == JTokenType.Array;
        else valid = value.Type == JTokenType.Object || value.Type == JTokenType.Null;
        if (!valid) throw new InvalidDataException("Incompatible stored value for " + Key);
        JsonUtility.FromJsonOverwrite(_prefix + value.ToString(Formatting.None) + "}", _owner);
        Visible = wire;
    }
}

internal abstract class SOStorageMode : IDisposable
{
    [Serializable] private sealed class Box<T> { public T value; }
    protected readonly SODataController Owner;
    protected SOStorageMode(SODataController owner) => Owner = owner;
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
    private static class Scalar<T>
    {
        internal static readonly bool CanCompare = typeof(T).IsPrimitive || typeof(T).IsEnum || typeof(T) == typeof(string);
    }

    protected virtual void Save(SOStoredField field, string wire) => throw new NotSupportedException();
    internal virtual Task FlushAsync() => Task.CompletedTask;
    public virtual void Dispose() { }
}
