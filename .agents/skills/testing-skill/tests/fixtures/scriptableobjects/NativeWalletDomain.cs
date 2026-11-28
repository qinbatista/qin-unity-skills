using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class NativeWalletDomain : SODataController
{
    [Serializable] public struct Item { public string id; public int count; }

    [SerializeField] private int _money = 37;
    [SerializeField] private Bounds _region;
    [SerializeField] private List<Item> _items = new List<Item>();
    [SerializeField] private Item[] _drops;
    [SerializeField] private string _hat = "white";
    public override SODataMode? RequiredMode => SODataMode.DirectAccess;
    public int money { get => Read(ref _money); set => Write(ref _money, value); }
    public Bounds region { get => Read(ref _region); set => Write(ref _region, value); }
    public List<Item> items { get => Read(ref _items); set => Write(ref _items, value); }
    public Item[] drops { get => Read(ref _drops); set => Write(ref _drops, value); }
    public string Hat { get => Read(ref _hat); set => Write(ref _hat, value); }
}
