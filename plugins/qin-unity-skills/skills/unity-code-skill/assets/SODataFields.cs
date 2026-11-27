using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using UnityEngine;

// Bind once when opening a keyed domain. Closed generic codecs also work in AOT builds.
public sealed class SODataFields
{
    [Serializable]
    private sealed class Box<T>
    {
        public T value;
    }

    internal sealed class Entry
    {
        public string Key;
        public Func<string> Capture;
        public Action<string> Apply;
        public string Authored;
        public SODataRecord Accepted;
        public int Notifications;
        public bool Initialized;
        public readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
    }

    private readonly Dictionary<string, string> _fields = new Dictionary<string, string>(StringComparer.Ordinal);
    internal readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

    public void Bind<T>(string fieldName, Func<T> read, Action<T> apply, string storageKey = null)
    {
        if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentException("Missing field name.", nameof(fieldName));
        if (read == null || apply == null) throw new ArgumentNullException("A field needs a getter and setter.");
        string key = storageKey ?? fieldName;
        if (string.IsNullOrWhiteSpace(key) || _fields.ContainsKey(fieldName) || Entries.ContainsKey(key))
            throw new ArgumentException("Each field needs one distinct, stable storage key.");
        CheckValue(typeof(T), fieldName, new HashSet<Type>());
        Entry entry = new Entry
        {
            Key = key,
            Capture = () => JsonUtility.ToJson(new Box<T> { value = read() }),
            Apply = json =>
            {
                var box = JsonUtility.FromJson<Box<T>>(json);
                if (box == null || JsonUtility.ToJson(box) != json)
                    throw new InvalidDataException("Unsupported stored value for " + key);
                apply(box.value);
            }
        };
        entry.Authored = entry.Capture();
        _fields.Add(fieldName, key);
        Entries.Add(key, entry);
    }

    internal void CheckCoverage(Type domain)
    {
        HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
        for (Type type = domain; type != typeof(SODataController); type = type.BaseType)
        {
            foreach (FieldInfo field in SerializedFields(type))
            {
                if (!names.Add(field.Name) || !_fields.ContainsKey(field.Name))
                    throw new InvalidOperationException("Bind each data field exactly once: " + type.Name + "." + field.Name);
            }
        }
        if (names.Count != _fields.Count)
            throw new InvalidOperationException("A storage binding must name a serialized field on the domain SO.");
    }

    internal static void CheckSnapshot(Type domain)
    {
        HashSet<Type> visiting = new HashSet<Type>();
        for (Type type = domain; type != typeof(SODataController); type = type.BaseType)
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
