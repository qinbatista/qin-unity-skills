using System;
using System.Threading;
using UnityEngine;

public sealed class SOStorageWorkloadData : SODataController, ISerializationCallbackReceiver
{
    [SerializeField] private int _value;
    [SerializeField] private string _padding;
    [NonSerialized] private int _serializationPasses;

    public int Value { get => Read(ref _value); set => Write(ref _value, value); }
    public string Padding => Read(ref _padding);
    public int SerializationPasses => Volatile.Read(ref _serializationPasses);

    public void OnBeforeSerialize() => Interlocked.Increment(ref _serializationPasses);
    public void OnAfterDeserialize() { }
}
