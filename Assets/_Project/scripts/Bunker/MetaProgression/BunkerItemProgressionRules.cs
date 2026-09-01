using UnityEngine;

/// <summary>Domain rules shared by item progression services, never by UI.</summary>
public static class BunkerItemProgressionRules
{
    public static int GetLevelCap(BunkerStationId stationId, int itemMaxLevel)
    {
        int stationLevel = BunkerStationProgressionService.Instance != null
            ? BunkerStationProgressionService.Instance.GetLevel(stationId)
            : BunkerStationProgressionService.GetStoredLevel(stationId);
        return GetLevelCap(stationId, stationLevel, itemMaxLevel);
    }

    public static int GetLevelCap(
        BunkerStationId stationId,
        int stationLevel,
        int itemMaxLevel)
    {
        itemMaxLevel = Mathf.Max(0, itemMaxLevel);
        stationLevel = Mathf.Clamp(stationLevel, 1, 3);
        if (itemMaxLevel == 0)
            return 0;

        if (stationId == BunkerStationId.Upgrades && itemMaxLevel == 10)
            return stationLevel switch { 1 => 3, 2 => 6, _ => 10 };

        return Mathf.Clamp(
            Mathf.CeilToInt(itemMaxLevel * stationLevel / 3f),
            1,
            itemMaxLevel);
    }
}
