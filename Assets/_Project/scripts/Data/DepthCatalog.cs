using System;
using System.Collections.Generic;
using UnityEngine;

public enum DepthAvailability { Locked, Available, UnavailableInBuild }

[CreateAssetMenu(fileName = "DepthCatalog", menuName = "Game/Levels/Depth Catalog")]
public sealed class DepthCatalog : ScriptableObject
{
    public const int SurfaceId = 1;

    [Serializable]
    public sealed class Entry
    {
        public int id;
        public string displayName;
        [Min(0)] public int requiredAccess;
        public bool availableInCurrentBuild;
        [Tooltip("Existing production scene and first sector; leave unimplemented depths unassigned.")]
        public string gameplaySceneName;
        public StageProfileData startingStageProfile;
        [TextArea] public string description;

        public string LocalizedName => LocalizationService.Instance.Get(displayName);
        public string LocalizedDescription => LocalizationService.Instance.Get(description);

        public DepthAvailability GetAvailability(int access)
        {
            if (access < requiredAccess) return DepthAvailability.Locked;
            return availableInCurrentBuild && startingStageProfile != null && !string.IsNullOrEmpty(gameplaySceneName)
                ? DepthAvailability.Available : DepthAvailability.UnavailableInBuild;
        }
    }

    [SerializeField] private Entry[] entries;
    public IReadOnlyList<Entry> Entries => entries;

    public Entry Find(int id)
    {
        foreach (Entry entry in entries)
            if (entry.id == id) return entry;
        return null;
    }

    public static string Numeral(int value) => value switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => value.ToString()
    };
}
