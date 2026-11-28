using System;
using UnityEngine;

// External, non-shipping Player fixture. The installed storage template is copied unchanged.
public sealed class SODeviceValue : SODataController
{
    [SerializeField] private int _value = 37;
    [SerializeField] private string _padding = "";
    [NonSerialized] public SODataMode selectedMode;
    public override SODataMode? RequiredMode => selectedMode;
    public int value { get => Read(ref _value); set => Write(ref _value, value); }
    public string padding { get => Read(ref _padding); set => Write(ref _padding, value); }
}
