using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BunkerStationProgressionService : MonoBehaviour
{
    private const string LevelKeyPrefix = "BunkerStationLevel_";
    private const string InvestedKeyPrefix = "BunkerStationInvested_";
    private const int DefaultLevel = 1;

    public static BunkerStationProgressionService Instance { get; private set; }

    public event Action<BunkerStationId, int> StationLevelChanged;
    public event Action<BunkerStationId, int> StationInvestmentChanged;

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
        SanitizeStoredInvestments();
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

    public int GetInvestedGold(BunkerStationId stationId)
    {
        if (!TryGetData(stationId, out BunkerStationProgressionData data))
            return 0;

        int level = GetLevel(stationId);
        if (level >= data.MaxLevel)
            return 0;

        return Mathf.Clamp(
            PlayerPrefs.GetInt(GetInvestedKey(stationId), 0),
            0,
            data.GetUpgradeCost(level));
    }

    public bool CanInvest(BunkerStationId stationId)
    {
        return TryGetData(stationId, out BunkerStationProgressionData data) &&
               GetLevel(stationId) < data.MaxLevel &&
               data.GetUpgradeCost(GetLevel(stationId)) > GetInvestedGold(stationId) &&
               CurrencyManager.Instance != null &&
               CurrencyManager.Instance.TotalGold > 0;
    }

    public bool CanUpgrade(BunkerStationId stationId)
    {
        if (!TryGetData(stationId, out BunkerStationProgressionData data))
            return false;

        int level = GetLevel(stationId);
        int cost = data.GetUpgradeCost(level) - GetInvestedGold(stationId);
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

        int remaining = data.GetUpgradeCost(currentLevel) - GetInvestedGold(stationId);
        return remaining > 0 &&
               CurrencyManager.Instance != null &&
               CurrencyManager.Instance.TotalGold >= remaining &&
               TryInvestGold(stationId, remaining, out _) &&
               GetLevel(stationId) > currentLevel;
    }

    public bool TryInvestGold(
        BunkerStationId stationId,
        int requestedAmount,
        out int actuallyInvested)
    {
        actuallyInvested = 0;
        if (requestedAmount <= 0 ||
            !TryGetData(stationId, out BunkerStationProgressionData data))
            return false;

        int currentLevel = GetLevel(stationId);
        if (currentLevel >= data.MaxLevel || CurrencyManager.Instance == null)
            return false;

        int cost = data.GetUpgradeCost(currentLevel);
        int invested = GetInvestedGold(stationId);
        int remaining = Mathf.Max(0, cost - invested);
        int amount = Mathf.Min(requestedAmount, remaining, CurrencyManager.Instance.TotalGold);
        if (amount <= 0 || !CurrencyManager.Instance.SpendGold(amount))
            return false;

        actuallyInvested = amount;
        invested += amount;
        if (invested < cost)
        {
            PlayerPrefs.SetInt(GetInvestedKey(stationId), invested);
            PlayerPrefs.Save();
            StationInvestmentChanged?.Invoke(stationId, invested);
            return true;
        }

        int newLevel = currentLevel + 1;
        PlayerPrefs.SetInt(GetLevelKey(stationId), newLevel);
        PlayerPrefs.SetInt(GetInvestedKey(stationId), 0);
        PlayerPrefs.Save();
        StationInvestmentChanged?.Invoke(stationId, 0);
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
            PlayerPrefs.DeleteKey(GetInvestedKey(stationId));
            StationInvestmentChanged?.Invoke(stationId, 0);
            StationLevelChanged?.Invoke(stationId, DefaultLevel);
        }

        PlayerPrefs.Save();
    }

    public void DebugSetStationLevel(BunkerStationId stationId, int level)
    {
        if (!TryGetData(stationId, out BunkerStationProgressionData data))
            return;

        int safeLevel = Mathf.Clamp(level, DefaultLevel, data.MaxLevel);
        PlayerPrefs.SetInt(GetLevelKey(stationId), safeLevel);
        PlayerPrefs.SetInt(GetInvestedKey(stationId), 0);
        PlayerPrefs.Save();
        StationInvestmentChanged?.Invoke(stationId, 0);
        StationLevelChanged?.Invoke(stationId, safeLevel);
    }

    public void DebugSetStationInvestment(BunkerStationId stationId, int investedGold)
    {
        if (!TryGetData(stationId, out BunkerStationProgressionData data))
            return;

        int level = GetLevel(stationId);
        int safe = level >= data.MaxLevel
            ? 0
            : Mathf.Clamp(investedGold, 0, data.GetUpgradeCost(level) - 1);
        PlayerPrefs.SetInt(GetInvestedKey(stationId), safe);
        PlayerPrefs.Save();
        StationInvestmentChanged?.Invoke(stationId, safe);
    }

    public void DebugResetStation(BunkerStationId stationId)
    {
        DebugSetStationLevel(stationId, DefaultLevel);
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

    private void SanitizeStoredInvestments()
    {
        bool changed = false;
        foreach (BunkerStationId stationId in Enum.GetValues(typeof(BunkerStationId)))
        {
            int stored = PlayerPrefs.GetInt(GetInvestedKey(stationId), 0);
            int safe = 0;
            if (TryGetData(stationId, out BunkerStationProgressionData data))
            {
                int level = GetLevel(stationId);
                if (level < data.MaxLevel)
                {
                    int cost = data.GetUpgradeCost(level);
                    safe = cost > 0 ? Mathf.Clamp(stored, 0, cost - 1) : 0;
                }
            }
            if (stored == safe)
                continue;

            PlayerPrefs.SetInt(GetInvestedKey(stationId), safe);
            changed = true;
        }

        if (changed)
            PlayerPrefs.Save();
    }

    private static string GetLevelKey(BunkerStationId stationId)
    {
        return LevelKeyPrefix + stationId;
    }

    private static string GetInvestedKey(BunkerStationId stationId)
    {
        return InvestedKeyPrefix + stationId;
    }
}
