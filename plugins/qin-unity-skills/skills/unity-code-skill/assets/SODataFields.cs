using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;

// Discover the domain once. A hooked field write reads only its own cached entry.
public sealed class SODataFields
{
    internal sealed class Entry
    {
        public string Key;
        public Func<string> Capture;
        public Action<string> Apply;
        public string Authored;
        public SODataRecord Accepted;
        public int Notifications;
        public bool Initialized;
        public bool NeedsCheckpoint;
        public readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
    }

    private readonly Dictionary<string, string> _fields = new Dictionary<string, string>(StringComparer.Ordinal);
    internal readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
    private static readonly HashSet<Type> RegisteredDeclarations = new HashSet<Type>();

    public void Bind<T>(string fieldName, Func<T> read, Action<T> apply, string storageKey = null)
    {
        if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentException("Missing field name.", nameof(fieldName));
        if (read == null || apply == null) throw new ArgumentNullException("A field needs a getter and setter.");
        CheckValue(typeof(T), fieldName, new HashSet<Type>());
        SODataWire.Register<T>();
        SODataWire.TryGet(typeof(T), out SODataWire.Codec codec);
        Add(fieldName, storageKey ?? fieldName, IsIndirect(typeof(T)), () => codec.Encode(read()), wire => apply((T)codec.Decode(wire)));
    }

    internal void Discover(ScriptableObject target)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        foreach (Type declaration in DomainTypes(target.GetType()))
        {
            RegisterDeclaredCodecs(declaration);
            foreach (FieldInfo field in SerializedFields(declaration))
            {
                if (TryGetByField(field.Name, out Entry explicitEntry))
                {
                    // Existing games can retain a stable wire key or an explicit value projection.
                    explicitEntry.NeedsCheckpoint = IsIndirect(field.FieldType);
                    continue;
                }
                CheckField(field, declaration.Name, new HashSet<Type>());
                if (!SODataWire.TryGet(field.FieldType, out SODataWire.Codec codec))
                    throw new InvalidOperationException("Missing generated wire codec for " + declaration.FullName + "." + field.Name + ". Rebuild its declaring assembly with SOData CodeGen installed.");
                Add(field.Name, field.Name, IsIndirect(field.FieldType), () => codec.Encode(field.GetValue(target)), wire => field.SetValue(target, codec.Decode(wire)));
            }
        }
        CheckCoverage(target.GetType());
    }

    private void Add(string name, string key, bool indirect, Func<string> capture, Action<string> apply)
    {
        if (string.IsNullOrWhiteSpace(key) || _fields.ContainsKey(name) || Entries.ContainsKey(key))
            throw new ArgumentException("Each SO data field needs a distinct storage key: " + key);
        Entry entry = new Entry { Key = key, Capture = capture, Apply = apply, Authored = capture(), NeedsCheckpoint = indirect };
        _fields.Add(name, key);
        Entries.Add(key, entry);
    }

    internal bool TryGetByField(string fieldName, out Entry entry)
    {
        entry = null;
        return fieldName != null && _fields.TryGetValue(fieldName, out string key) && Entries.TryGetValue(key, out entry);
    }

    private static void RegisterDeclaredCodecs(Type declaration)
    {
        if (RegisteredDeclarations.Contains(declaration)) return;
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        MethodInfo register = declaration.GetMethod("__SODataRegisterCodecs", flags, null, Type.EmptyTypes, null);
        if (register != null) register.Invoke(null, null);
        RegisteredDeclarations.Add(declaration);
    }

    private static bool IsIndirect(Type type) => !type.IsPrimitive && !type.IsEnum && type != typeof(string);

    internal static bool HasIndirectFields(Type domain)
    {
        foreach (Type declaration in DomainTypes(domain))
            foreach (FieldInfo field in SerializedFields(declaration))
                if (IsIndirect(field.FieldType)) return true;
        return false;
    }

    private static IEnumerable<Type> DomainTypes(Type domain)
    {
        if (domain == null || !typeof(ScriptableObject).IsAssignableFrom(domain)) throw new ArgumentException("Expected a ScriptableObject domain type.", nameof(domain));
        for (Type type = domain; type != typeof(SODataController) && type != typeof(ScriptableObject); type = type.BaseType)
            yield return type;
    }

    internal void CheckCoverage(Type domain)
    {
        HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Type type in DomainTypes(domain))
        {
            foreach (FieldInfo field in SerializedFields(type))
            {
                if (!names.Add(field.Name) || !_fields.ContainsKey(field.Name))
                    throw new InvalidOperationException("Expected one unambiguous serialized data field: " + type.Name + "." + field.Name);
            }
        }
        if (names.Count != _fields.Count)
            throw new InvalidOperationException("A storage binding must name a serialized field on the domain SO.");
    }

    internal static void CheckSnapshot(Type domain)
    {
        HashSet<Type> visiting = new HashSet<Type>();
        foreach (Type type in DomainTypes(domain))
            foreach (FieldInfo field in SerializedFields(type))
                CheckField(field, type.Name, visiting);
    }

    private static IEnumerable<FieldInfo> SerializedFields(Type type)
    {
        const BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (FieldInfo field in type.GetFields(flags))
        {
            if (field.IsInitOnly || field.IsNotSerialized) continue;
            if (field.IsPublic || field.IsDefined(typeof(SerializeField)) || field.IsDefined(typeof(SerializeReference)))
                yield return field;
        }
    }

    private static void CheckField(FieldInfo field, string owner, HashSet<Type> visiting)
    {
        string name = owner + "." + field.Name;
        if (field.IsDefined(typeof(SerializeReference)))
            throw new InvalidOperationException("Give the managed-reference field a stable storage representation: " + name);
        CheckValue(field.FieldType, name, visiting);
    }

    // JsonUtility can silently omit unsupported fields or write process-local Unity instance IDs.
    // Check the declared wire layout once, before a durable write can acknowledge such a value.
    private static void CheckValue(Type type, string name, HashSet<Type> visiting)
    {
        if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return;
        Type element = null;
        if (type.IsArray && type.GetArrayRank() == 1) element = type.GetElementType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) element = type.GetGenericArguments()[0];
        if (element != null && !element.IsArray && !typeof(System.Collections.IList).IsAssignableFrom(element))
        {
            CheckValue(element, name, visiting);
            return;
        }
        if (type.IsArray || type.IsGenericType || type.IsAbstract || !type.IsSerializable ||
            typeof(UnityEngine.Object).IsAssignableFrom(type) || type.Namespace == "System" || !visiting.Add(type))
            throw new InvalidOperationException("Use a serializable value or a stable ID for " + name + " (" + type.Name + ").");
        try
        {
            for (Type owner = type; owner != null && owner != typeof(object); owner = owner.BaseType)
                foreach (FieldInfo field in SerializedFields(owner)) CheckField(field, name, visiting);
        }
        finally { visiting.Remove(type); }
    }
}
