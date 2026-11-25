using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Data/Spell Catalog")]
public sealed class SOSpellCatalog : ScriptableObject, ISerializationCallbackReceiver
{
    [Serializable]
    public struct SpellData
    {
        [SerializeField] private string id;
        [SerializeField] private int damage;
        [SerializeField, FormerlySerializedAs("castTime")] private float durationSeconds;

        public string Id => id;
        public int Damage => damage;
        public float DurationSeconds => durationSeconds;
    }

    [SerializeField] private SpellData[] spells = Array.Empty<SpellData>();
    [NonSerialized] private Dictionary<string, SpellData> byId;

    public bool TryGetSpell(string id, out SpellData spell)
    {
        if (byId == null)
        {
            SpellData[] entries = spells ?? Array.Empty<SpellData>();
            Dictionary<string, SpellData> index = new Dictionary<string, SpellData>(entries.Length, StringComparer.Ordinal);
            foreach (SpellData entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Id) || !index.TryAdd(entry.Id, entry))
                    throw new InvalidDataException($"Expected a unique, nonempty spell ID. Found '{entry.Id}'.");
            }
            byId = index;
        }
        spell = default;
        return id != null && byId.TryGetValue(id, out spell);
    }

    private void OnValidate() => byId = null;
    public void OnAfterDeserialize() => byId = null;
    public void OnBeforeSerialize() { }
}
