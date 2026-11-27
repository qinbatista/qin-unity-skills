using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

// Each generated registration names a real closed type in IL, including List<GameItem> or GameItem[].
// Unity's native serializer handles that exact type in both the Editor and stripped IL2CPP players.
[Preserve]
public static class SODataWire
{
    [Serializable, Preserve]
    private sealed class Box<T>
    {
        [Preserve] public T value;
    }

    public sealed class Codec
    {
        public readonly Func<object, string> Encode;
        public readonly Func<string, object> Decode;

        internal Codec(Func<object, string> encode, Func<string, object> decode)
        {
            Encode = encode;
            Decode = decode;
        }
    }

    private static readonly Dictionary<Type, Codec> Codecs = new Dictionary<Type, Codec>();

    static SODataWire()
    {
        // Plain scalar domains also work when the declaring assembly supplies no generated codecs.
        Register<bool>();
        Register<byte>();
        Register<sbyte>();
        Register<char>();
        Register<short>();
        Register<ushort>();
        Register<int>();
        Register<uint>();
        Register<long>();
        Register<ulong>();
        Register<float>();
        Register<double>();
        Register<string>();
    }

    [Preserve]
    public static void Register<T>()
    {
        lock (Codecs)
        {
            if (!Codecs.ContainsKey(typeof(T))) Codecs.Add(typeof(T), new Codec(Encode<T>, Decode<T>));
        }
    }

    public static bool TryGet(Type type, out Codec codec)
    {
        lock (Codecs) return Codecs.TryGetValue(type, out codec);
    }

    private static string Encode<T>(object value)
    {
        string wire = JsonUtility.ToJson(new Box<T> { value = (T)value });
        // JsonUtility can omit an unsupported type and return an empty object without raising an error.
        if (!wire.StartsWith("{\"value\":", StringComparison.Ordinal)) throw new InvalidDataException("Unity could not serialize the SO data type " + typeof(T).FullName + ".");
        return wire;
    }

    private static object Decode<T>(string wire)
    {
        if (string.IsNullOrEmpty(wire) || !wire.StartsWith("{\"value\":", StringComparison.Ordinal)) throw new InvalidDataException("Expected a stored value for " + typeof(T).FullName + ".");
        Box<T> box;
        try { box = JsonUtility.FromJson<Box<T>>(wire); }
        catch (ArgumentException error) { throw new InvalidDataException("Invalid stored value for " + typeof(T).FullName + ".", error); }
        if (box == null || !string.Equals(JsonUtility.ToJson(box), wire, StringComparison.Ordinal)) throw new InvalidDataException("The stored value does not match " + typeof(T).FullName + ".");
        return box.value;
    }
}
