using System;
using UnityEngine;

public enum MetaUpgradeType
{
    Hp,
    Damage,
    MoveSpeed,
    XpGain,
    GoldGain,
    PickupRadius
}

public sealed class MetaProgressionManager : MonoBehaviour
{
    public static MetaProgressionManager Instance { get; private set; }

    private const int MaxUpgradeLevel = 10;

    private const string HpLevelKey = "META_HP_LEVEL";
    private const string DamageLevelKey = "META_DAMAGE_LEVEL";
    private const string MoveSpeedLevelKey = "META_MOVE_SPEED_LEVEL";
    private const string XpGainLevelKey = "META_XP_GAIN_LEVEL";
    private const string GoldGainLevelKey = "META_GOLD_GAIN_LEVEL";
    private const string PickupRadiusLevelKey = "META_PICKUP_RADIUS_LEVEL";

    public event Action ProgressChanged;

    public int HpLevel { get; private set; }
    public int DamageLevel { get; private set; }
    public int MoveSpeedLevel { get; private set; }
    public int XpGainLevel { get; private set; }
    public int GoldGainLevel { get; private set; }
    public int PickupRadiusLevel { get; private set; }

    public int MaxLevel => MaxUpgradeLevel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Менеджер обязан быть корневым объектом.
        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);
        ReloadFromStorage();
    }

    public static MetaProgressionManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        MetaProgressionManager existing =
            FindFirstObjectByType<MetaProgressionManager>();

        if (existing != null)
        {
            Instance = existing;
            existing.ReloadFromStorage();
            return existing;
        }

        GameObject root = new("MetaProgressionManager");
        return root.AddComponent<MetaProgressionManager>();
    }

    public void ReloadFromStorage()
    {
        HpLevel = PlayerPrefs.GetInt(HpLevelKey, 0);
        DamageLevel = PlayerPrefs.GetInt(DamageLevelKey, 0);
        MoveSpeedLevel = PlayerPrefs.GetInt(MoveSpeedLevelKey, 0);
        XpGainLevel = PlayerPrefs.GetInt(XpGainLevelKey, 0);
        GoldGainLevel = PlayerPrefs.GetInt(GoldGainLevelKey, 0);
        PickupRadiusLevel = PlayerPrefs.GetInt(PickupRadiusLevelKey, 0);
    }

    public int GetLevel(MetaUpgradeType type)
    {
        return type switch
        {
            MetaUpgradeType.Hp => HpLevel,
            MetaUpgradeType.Damage => DamageLevel,
            MetaUpgradeType.MoveSpeed => MoveSpeedLevel,
            MetaUpgradeType.XpGain => XpGainLevel,
            MetaUpgradeType.GoldGain => GoldGainLevel,
            MetaUpgradeType.PickupRadius => PickupRadiusLevel,
            _ => 0
        };
    }

    public int GetUpgradeCost(MetaUpgradeType type)
    {
        return GetUpgradeCostByLevel(GetLevel(type));
    }

    public int GetUpgradeCostByLevel(int currentLevel)
    {
        if (currentLevel >= MaxUpgradeLevel)
            return 0;

        int[] costs =
        {
            100, 200, 300, 400, 500,
            700, 900, 1200, 1500, 2000
        };

        return costs[Mathf.Clamp(currentLevel, 0, costs.Length - 1)];
    }

    public bool BuyUpgrade(MetaUpgradeType type)
    {
        int currentLevel = GetLevel(type);

        if (currentLevel >= MaxUpgradeLevel)
            return false;

        CurrencyManager currency = CurrencyManager.Instance;

        if (currency == null)
        {
            Debug.LogError("[MetaProgressionManager] CurrencyManager is missing.");
            return false;
        }

        int cost = GetUpgradeCostByLevel(currentLevel);

        if (!currency.SpendGold(cost))
            return false;

        SetLevel(type, currentLevel + 1);
        PlayerPrefs.Save();
        Debug.Log(
    $"[MetaProgressionManager] Saved: " +
    $"HP={PlayerPrefs.GetInt("META_HP_LEVEL")}, " +
    $"DMG={PlayerPrefs.GetInt("META_DAMAGE_LEVEL")}, " +
    $"SPD={PlayerPrefs.GetInt("META_MOVE_SPEED_LEVEL")}"
);

        ProgressChanged?.Invoke();

        Debug.Log(
            $"[MetaProgressionManager] Purchased {type}: " +
            $"{currentLevel} -> {currentLevel + 1}"
        );

        return true;
    }

    private void SetLevel(MetaUpgradeType type, int level)
    {
        level = Mathf.Clamp(level, 0, MaxUpgradeLevel);

        switch (type)
        {
            case MetaUpgradeType.Hp:
                HpLevel = level;
                PlayerPrefs.SetInt(HpLevelKey, level);
                break;

            case MetaUpgradeType.Damage:
                DamageLevel = level;
                PlayerPrefs.SetInt(DamageLevelKey, level);
                break;

            case MetaUpgradeType.MoveSpeed:
                MoveSpeedLevel = level;
                PlayerPrefs.SetInt(MoveSpeedLevelKey, level);
                break;

            case MetaUpgradeType.XpGain:
                XpGainLevel = level;
                PlayerPrefs.SetInt(XpGainLevelKey, level);
                break;

            case MetaUpgradeType.GoldGain:
                GoldGainLevel = level;
                PlayerPrefs.SetInt(GoldGainLevelKey, level);
                break;

            case MetaUpgradeType.PickupRadius:
                PickupRadiusLevel = level;
                PlayerPrefs.SetInt(PickupRadiusLevelKey, level);
                break;
        }
    }
}