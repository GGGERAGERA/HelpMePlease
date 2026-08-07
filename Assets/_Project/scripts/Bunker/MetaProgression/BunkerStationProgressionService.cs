using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BunkerStationProgressionService : MonoBehaviour
{
    private const string LevelKeyPrefix = "BunkerStationLevel_";
    private const int DefaultLevel = 1;

    public static BunkerStationProgressionService Instance { get; private set; }

    public event Action<BunkerStationId, int> StationLevelChanged;

    private readonly Dictionary<BunkerStationId, BunkerStationProgressionData> dataById = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        LoadConfiguration();
        SanitizeStoredLevels();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool TryGetData(BunkerStationId stationId, out BunkerStationProgressionData data)
    {
        return dataById.TryGetValue(stationId, out data);
    }

    public int GetLevel(BunkerStationId stationId)
    {
        int maxLevel = TryGetData(stationId, out BunkerStationProgressionData data)
            ? data.MaxLevel
            : 3;

        return Mathf.Clamp(GetStoredLevel(stationId), DefaultLevel, maxLevel);
    }

    public static int GetStoredLevel(BunkerStationId stationId)
    {
        return Mathf.Clamp(
            PlayerPrefs.GetInt(GetLevelKey(stationId), DefaultLevel),
            DefaultLevel,
            3);
    }

    public int GetUpgradeCost(BunkerStationId stationId)
    {
        return TryGetData(stationId, out BunkerStationProgressionData data)
            ? data.GetUpgradeCost(GetLevel(stationId))
            : 0;
    }

    public bool CanUpgrade(BunkerStationId stationId)
    {
        if (!TryGetData(stationId, out BunkerStationProgressionData data))
            return false;

        int level = GetLevel(stationId);
        int cost = data.GetUpgradeCost(level);
        return level < data.MaxLevel &&
               cost > 0 &&
               CurrencyManager.Instance != null &&
               CurrencyManager.Instance.TotalGold >= cost;
    }

    public bool TryUpgrade(BunkerStationId stationId)
    {
        if (!TryGetData(stationId, out BunkerStationProgressionData data))
            return false;

        int currentLevel = GetLevel(stationId);
        if (currentLevel >= data.MaxLevel)
            return false;

        int cost = data.GetUpgradeCost(currentLevel);
        if (cost <= 0 || CurrencyManager.Instance == null)
            return false;

        if (!CurrencyManager.Instance.SpendGold(cost))
            return false;

        int newLevel = currentLevel + 1;
        PlayerPrefs.SetInt(GetLevelKey(stationId), newLevel);
        PlayerPrefs.Save();
        StationLevelChanged?.Invoke(stationId, newLevel);
        return true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Add 1000 Gold (Debug)")]
    public void DebugAddGold()
    {
        CurrencyManager.Instance?.AddGold(1000);
    }

    [ContextMenu("Reset Station Levels (Debug)")]
    public void DebugResetStationLevels()
    {
        foreach (BunkerStationId stationId in Enum.GetValues(typeof(BunkerStationId)))
        {
            PlayerPrefs.DeleteKey(GetLevelKey(stationId));
            StationLevelChanged?.Invoke(stationId, DefaultLevel);
        }

        PlayerPrefs.Save();
    }
#endif

    private void LoadConfiguration()
    {
        dataById.Clear();
        BunkerStationProgressionData[] allData =
            Resources.LoadAll<BunkerStationProgressionData>("BunkerProgression");

        foreach (BunkerStationProgressionData data in allData)
        {
            if (data == null)
                continue;

            if (dataById.ContainsKey(data.StationId))
            {
                Debug.LogWarning(
                    $"[BunkerStationProgression] Duplicate configuration for {data.StationId}.",
                    data);
                continue;
            }

            dataById.Add(data.StationId, data);
        }

        if (dataById.Count != 4)
            Debug.LogWarning($"[BunkerStationProgression] Expected 4 station configs, found {dataById.Count}.", this);
    }

    private void SanitizeStoredLevels()
    {
        bool changed = false;
        foreach (BunkerStationId stationId in Enum.GetValues(typeof(BunkerStationId)))
        {
            int stored = PlayerPrefs.GetInt(GetLevelKey(stationId), DefaultLevel);
            int safe = GetLevel(stationId);
            if (stored == safe)
                continue;

            PlayerPrefs.SetInt(GetLevelKey(stationId), safe);
            changed = true;
        }

        if (changed)
            PlayerPrefs.Save();
    }

    private static string GetLevelKey(BunkerStationId stationId)
    {
        return LevelKeyPrefix + stationId;
    }
}
