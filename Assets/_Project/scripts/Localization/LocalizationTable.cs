using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "LocalizationTable",
    menuName = "Game/Localization/Localization Table"
)]
public sealed class LocalizationTable : ScriptableObject
{
    [Serializable]
    private sealed class Entry
    {
        public string key;
        [TextArea] public string ru;
        [TextArea] public string en;
    }

    [SerializeField] private List<Entry> entries = new();

    private readonly Dictionary<string, Entry> lookup =
        new(StringComparer.Ordinal);

    public bool TryGet(
        string key,
        GameLanguage language,
        out string value
    )
    {
        EnsureLookup();

        if (!string.IsNullOrWhiteSpace(key) &&
            lookup.TryGetValue(key, out Entry entry))
        {
            value = language == GameLanguage.Russian
                ? entry.ru
                : entry.en;
            return true;
        }

        value = key;
        return false;
    }

    public bool ContainsKey(string key)
    {
        EnsureLookup();
        return !string.IsNullOrWhiteSpace(key) && lookup.ContainsKey(key);
    }

    private void OnEnable()
    {
        BuildLookup();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        BuildLookup();
    }
#endif

    private void EnsureLookup()
    {
        if (lookup.Count == 0 && entries.Count > 0)
            BuildLookup();
    }

    private void BuildLookup()
    {
        lookup.Clear();

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];

            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            if (lookup.ContainsKey(entry.key))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    $"[LocalizationTable] Duplicate key '{entry.key}'.",
                    this
                );
#endif
                continue;
            }

            lookup.Add(entry.key, entry);
        }
    }
}
