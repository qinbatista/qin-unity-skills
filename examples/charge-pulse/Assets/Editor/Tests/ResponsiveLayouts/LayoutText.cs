using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Qin.UnitySamples.Tests
{
    sealed class LayoutText
    {
        readonly CultureInfo[] _cultures;
        readonly Dictionary<string, string[]> _entries = new Dictionary<string, string[]>();
        readonly Dictionary<TextElement, (string Key, object[] Arguments)> _bindings = new Dictionary<TextElement, (string, object[])>();
        int _activeLocale;

        public IReadOnlyList<string> Locales { get; }
        public string Locale => Locales[_activeLocale];
        public event Action Changed;
        public IEnumerable<TextElement> BoundElements => _bindings.Keys;

        public LayoutText(TextAsset resource)
        {
            StringTable table = JsonUtility.FromJson<StringTable>(resource.text);
            if (table.locales.Length == 0 || new HashSet<string>(table.locales).Count != table.locales.Length)
                throw new InvalidOperationException("The fixture table needs distinct locale IDs.");

            Locales = Array.AsReadOnly(table.locales);
            _cultures = Array.ConvertAll(table.locales, CultureInfo.GetCultureInfo);
            foreach (Entry entry in table.entries)
            {
                if (entry.values.Length != Locales.Count || Array.Exists(entry.values, string.IsNullOrWhiteSpace))
                    throw new InvalidOperationException($"Complete each locale for {entry.key}.");

                _entries.Add(entry.key, entry.values);
            }
        }

        public string Resolve(string key, params object[] arguments)
        {
            return string.Format(_cultures[_activeLocale], _entries[key][_activeLocale], arguments);
        }

        public void Bind(TextElement element, string key, params object[] arguments)
        {
            string value = Resolve(key, arguments);
            _bindings.Add(element, (key, arguments));
            element.text = value;
        }

        public void SelectLocale(int localeIndex)
        {
            if (_activeLocale == localeIndex) return;
            _activeLocale = localeIndex;
            foreach (KeyValuePair<TextElement, (string Key, object[] Arguments)> binding in _bindings)
                binding.Key.text = Resolve(binding.Value.Key, binding.Value.Arguments);
            Changed?.Invoke();
        }

        [Serializable]
        sealed class StringTable
        {
            public string[] locales = Array.Empty<string>();
            public Entry[] entries = Array.Empty<Entry>();
        }

        [Serializable]
        sealed class Entry
        {
            public string key = string.Empty;
            public string[] values = Array.Empty<string>();
        }
    }
}
