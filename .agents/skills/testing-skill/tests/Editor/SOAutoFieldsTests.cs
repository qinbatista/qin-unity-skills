using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed partial class UnityFoundationTests
{
    [Serializable]
    private sealed class PreviousKeyBox<T>
    {
        public T value;
    }

    private enum AutoMood : byte { Resting, Mining = 200 }

    [Serializable]
    private struct AutoItem
    {
        public string id;
        public int count;
    }

    private abstract class PlainAutoBase : ScriptableObject
    {
        [SerializeField] private long experience = 17;
        public long Experience => experience;
    }

    private sealed class PlainAutoWallet : PlainAutoBase
    {
        public int money = 37;
        public Bounds region = new Bounds(Vector3.one, new Vector3(2f, 3f, 4f));
        [SerializeField] private List<AutoItem> items = new List<AutoItem> { new AutoItem { id = "ore", count = 3 } };
        [SerializeField] private AutoMood mood = AutoMood.Mining;
        [SerializeField] private AutoItem[] drops;
        [NonSerialized] public int transient = 9;
        public readonly int constant = 7;
        public List<AutoItem> Items => items;
        public AutoItem[] Drops => drops;
        public AutoMood Mood => mood;
    }

    private static string PreviousWire<T>(T value) => JsonUtility.ToJson(new PreviousKeyBox<T> { value = value });

    [Test]
    public void PlainSOFindsPublicPrivateAndInheritedFieldsWithoutRegistration()
    {
        PlainAutoWallet data = ScriptableObject.CreateInstance<PlainAutoWallet>();
        try
        {
            SODataFields fields = new SODataFields();
            fields.Discover(data);
            CollectionAssert.AreEquivalent(new[] { "money", "region", "items", "mood", "drops", "experience" }, fields.Entries.Keys);
            Assert.IsTrue(fields.TryGetByField("money", out SODataFields.Entry money));
            Assert.AreEqual("{\"value\":37}", money.Authored);
            Assert.IsFalse(money.NeedsCheckpoint);
            Assert.IsFalse(fields.Entries["mood"].NeedsCheckpoint);
            Assert.IsTrue(fields.Entries["region"].NeedsCheckpoint);
            Assert.IsTrue(fields.Entries["items"].NeedsCheckpoint);
            Assert.IsTrue(SODataFields.HasIndirectFields(typeof(PlainAutoWallet)));
            Assert.IsFalse(SODataFields.HasIndirectFields(typeof(PlainAutoBase)));
            SODataFields.CheckSnapshot(typeof(PlainAutoWallet));
            fields.Entries["experience"].Apply(PreviousWire(48L));
            Assert.AreEqual(48, data.Experience);
            Assert.IsFalse(fields.TryGetByField("transient", out SODataFields.Entry ignored));
            Assert.AreEqual(9, data.transient);
        }
        finally { Object.DestroyImmediate(data); }
    }

    [Test]
    public void GeneratedClosedCodecsKeepThePreviousNativeWireAndValidateBeforeAssignment()
    {
        PlainAutoWallet data = ScriptableObject.CreateInstance<PlainAutoWallet>();
        try
        {
            SODataFields fields = new SODataFields();
            fields.Discover(data);
            Assert.AreEqual(PreviousWire(data.region), fields.Entries["region"].Capture());
            Assert.AreEqual(PreviousWire(data.Items), fields.Entries["items"].Capture());
            Assert.AreEqual(PreviousWire(data.Drops), fields.Entries["drops"].Capture());
            Assert.AreEqual(PreviousWire(data.Mood), fields.Entries["mood"].Capture());

            List<AutoItem> oldItems = data.Items;
            Assert.Throws<InvalidDataException>(() => fields.Entries["items"].Apply("{\"value\":[{\"id\":\"ore\",\"count\":7,\"unknown\":12}]}"));
            Assert.AreSame(oldItems, data.Items);
            Assert.AreEqual(3, data.Items[0].count);
            Assert.AreEqual(37, data.money);
            AutoItem[] drops = { new AutoItem { id = "rune", count = 6 } };
            fields.Entries["drops"].Apply(PreviousWire(drops));
            Assert.AreEqual("rune", data.Drops[0].id);
            Assert.AreEqual(6, data.Drops[0].count);
            fields.Entries["items"].Apply(PreviousWire(new List<AutoItem>(drops)));
            Assert.AreEqual("rune", data.Items[0].id);
            Assert.AreEqual(37, data.money);
            data.money += 3;
            Assert.AreEqual("{\"value\":40}", fields.Entries["money"].Capture());
        }
        finally { Object.DestroyImmediate(data); }
    }

    [Test]
    public void ExistingExplicitKeySurvivesAutomaticDiscovery()
    {
        PlainAutoWallet data = ScriptableObject.CreateInstance<PlainAutoWallet>();
        try
        {
            SODataFields fields = new SODataFields();
            fields.Bind(nameof(data.money), () => data.money, amount => data.money = amount, "gold-v1");
            fields.Discover(data);
            Assert.IsTrue(fields.TryGetByField(nameof(data.money), out SODataFields.Entry money));
            Assert.AreEqual("gold-v1", money.Key);
            Assert.IsFalse(fields.Entries.ContainsKey("money"));
            money.Apply("{\"value\":50}");
            Assert.AreEqual(50, data.money);
            Assert.AreEqual(PreviousWire(data.Items), fields.Entries["items"].Capture());
            SODataFields collision = new SODataFields();
            collision.Bind(nameof(data.money), () => data.money, amount => data.money = amount, "items");
            Assert.Throws<ArgumentException>(() => collision.Discover(data));
        }
        finally { Object.DestroyImmediate(data); }
    }
}
