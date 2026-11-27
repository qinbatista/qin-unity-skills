using System;
using System.Collections.Generic;
using UnityEngine;

// Compile this ordinary game type in a separate asmdef with no reference to the storage template.
public sealed class NativeWalletDomain : ScriptableObject
{
    [Serializable]
    public struct Item
    {
        public string id;
        public int count;
    }

    public int money = 37;
    public Bounds region;
    public List<Item> items = new List<Item>();
    public Item[] drops;
    [SerializeField] private string hat = "white";

    public string Hat => hat;
    public void Equip(string id) => hat = id;
}
