using System;
using UnityEngine;

public sealed class AnomalyMetaProgressionManager : MonoBehaviour
{
    private const string LevelKeyPrefix = "META_ANOMALY_LEVEL_";
    private const string InvestmentKeyPrefix = "META_ANOMALY_INVESTED_";
    private const int DefaultLevel = 1;

    public static AnomalyMetaProgressionManager Instance { get; private set; }
    public event Action ProgressChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (transform.parent != null)
            transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static AnomalyMetaProgressionManager EnsureExists()
    {
        if (Instance != null)
            return Instance;
        AnomalyMetaProgressionManager existing =
            FindFirstObjectByType<AnomalyMetaProgressionManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }
        return new GameObject("AnomalyMetaProgressionManager")
            .AddComponent<AnomalyMetaProgressionManager>();
    }

    public int GetLevel(AnomalyStabilizerData anomaly)
    {
        if (anomaly == null)
            return 0;
        string key = GetLevelKey(anomaly);
        int stored = PlayerPrefs.GetInt(key, DefaultLevel);
        int level = Mathf.Clamp(stored, DefaultLevel, anomaly.MaxMetaLevel);
        if (stored != level)
        {
            PlayerPrefs.SetInt(key, level);
            PlayerPrefs.Save();
        }
        return level;
    }

    public int GetCurrentLevelCap(AnomalyStabilizerData anomaly)
    {
        return anomaly == null ? 0 : BunkerItemProgressionRules.GetLevelCap(
            BunkerStationId.Anomaly,
            anomaly.MaxMetaLevel);
    }

    public bool CanBuy(AnomalyStabilizerData anomaly)
    {
        if (!CanInvest(anomaly))
            return false;
        int remaining = anomaly.GetMetaUpgradeCost(GetLevel(anomaly)) -
            GetInvestedGold(anomaly);
        return CurrencyManager.Instance.TotalGold >= remaining;
    }

    public int GetInvestedGold(AnomalyStabilizerData anomaly)
    {
        if (anomaly == null)
            return 0;
        int level = GetLevel(anomaly);
        if (level >= anomaly.MaxMetaLevel)
            return 0;
        int cost = anomaly.GetMetaUpgradeCost(level);
        string key = GetInvestmentKey(anomaly);
        int stored = PlayerPrefs.GetInt(key, 0);
        int safe = cost > 0 ? Mathf.Clamp(stored, 0, cost - 1) : 0;
        if (stored != safe)
        {
            PlayerPrefs.SetInt(key, safe);
            PlayerPrefs.Save();
        }
        return safe;
    }

    public bool CanInvest(AnomalyStabilizerData anomaly)
    {
        if (anomaly == null || CurrencyManager.Instance == null)
            return false;
        int level = GetLevel(anomaly);
        int cost = anomaly.GetMetaUpgradeCost(level);
        int stationLevel = BunkerStationProgressionService.Instance != null
            ? BunkerStationProgressionService.Instance.GetLevel(BunkerStationId.Anomaly)
            : BunkerStationProgressionService.GetStoredLevel(BunkerStationId.Anomaly);
        return stationLevel >= anomaly.RequiredStationLevel &&
            level < anomaly.MaxMetaLevel && level < GetCurrentLevelCap(anomaly) &&
            cost > GetInvestedGold(anomaly) && CurrencyManager.Instance.TotalGold > 0;
    }

    public bool TryInvestGold(
        AnomalyStabilizerData anomaly,
        int requestedAmount,
        out int actuallyInvested)
    {
        actuallyInvested = 0;
        if (requestedAmount <= 0 || !CanInvest(anomaly))
            return false;
        int level = GetLevel(anomaly);
        int cost = anomaly.GetMetaUpgradeCost(level);
        int invested = GetInvestedGold(anomaly);
        int amount = Mathf.Min(
            requestedAmount,
            cost - invested,
            CurrencyManager.Instance.TotalGold);
        if (amount <= 0 || !CurrencyManager.Instance.SpendGold(amount))
            return false;

        actuallyInvested = amount;
        invested += amount;
        if (invested < cost)
        {
            PlayerPrefs.SetInt(GetInvestmentKey(anomaly), invested);
            PlayerPrefs.Save();
            ProgressChanged?.Invoke();
            return true;
        }

        PlayerPrefs.SetInt(GetLevelKey(anomaly), level + 1);
        PlayerPrefs.SetInt(GetInvestmentKey(anomaly), 0);
        PlayerPrefs.Save();
        ProgressChanged?.Invoke();
        return true;
    }

    public bool BuyUpgrade(AnomalyStabilizerData anomaly)
    {
        if (!CanBuy(anomaly))
            return false;
        int level = GetLevel(anomaly);
        int remaining = anomaly.GetMetaUpgradeCost(level) - GetInvestedGold(anomaly);
        if (CurrencyManager.Instance.TotalGold < remaining ||
            !TryInvestGold(anomaly, remaining, out _))
            return false;
        return true;
    }

    public float GetEffectValue(AnomalyStabilizerData anomaly)
    {
        return anomaly == null ? 0f : anomaly.GetMetaEffectValue(GetLevel(anomaly));
    }

    private static string GetLevelKey(AnomalyStabilizerData anomaly)
    {
        return LevelKeyPrefix + anomaly.Id;
    }

    private static string GetInvestmentKey(AnomalyStabilizerData anomaly)
    {
        return InvestmentKeyPrefix + anomaly.Id;
    }
}
