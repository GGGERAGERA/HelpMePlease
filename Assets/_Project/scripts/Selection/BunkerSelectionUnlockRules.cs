using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared data-to-visibility rule for every bunker selection source.</summary>
public static class BunkerSelectionUnlockRules
{
    public static int GetRequiredStationLevel(
        UnlockableContentData unlockData,
        BunkerStationId expectedStation)
    {
        if (unlockData == null || unlockData.unlockedByDefault ||
            unlockData.condition == null ||
            unlockData.condition.type != UnlockConditionType.StationLevelRequirement)
            return 1;

        return unlockData.condition.stationId == expectedStation
            ? Mathf.Max(1, unlockData.condition.requiredAmount)
            : int.MaxValue;
    }

    public static int NormalizeRequiredLevel(int configuredLevel)
    {
        return Mathf.Max(1, configuredLevel);
    }

    public static bool IsVisible(int stationLevel, int requiredStationLevel)
    {
        return stationLevel >= NormalizeRequiredLevel(requiredStationLevel);
    }

    public static string BuildNextUnlockText(
        IReadOnlyList<BunkerSelectionUnlockModel> unlocks,
        int currentLevel,
        int maxLevel)
    {
        if (currentLevel >= maxLevel)
            return LocalizationService.Instance.Get("bunker.all_unlocked");

        int nextLevel = currentLevel + 1;
        var names = new List<string>();
        if (unlocks != null)
        {
            foreach (BunkerSelectionUnlockModel unlock in unlocks)
            {
                if (unlock.RequiredStationLevel == nextLevel &&
                    !string.IsNullOrWhiteSpace(unlock.DisplayName) &&
                    !names.Contains(unlock.DisplayName))
                    names.Add(unlock.DisplayName);
            }
        }
        return names.Count == 0
            ? LocalizationService.Instance.Get("bunker.no_next_unlocks")
            : LocalizationService.Instance.Get("bunker.next_unlocks") + string.Join("\n• ", names);
    }
}
