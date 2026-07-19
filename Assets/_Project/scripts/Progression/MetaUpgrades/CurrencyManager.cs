using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance;

    private const string GoldKey = "TOTAL_GOLD";
    private const int MaxMetaUpgradeLevel = 10;

    private static readonly string[] MetaUpgradeLevelKeys =
    {
        "META_HP_LEVEL",
        "META_DAMAGE_LEVEL",
        "META_MOVE_SPEED_LEVEL",
        "META_XP_GAIN_LEVEL",
        "META_GOLD_GAIN_LEVEL",
        "META_PICKUP_RADIUS_LEVEL"
    };

    public int TotalGold { get; private set; }
    public System.Action<int> OnGoldUpdated;

    private float goldGainMultiplier = 1f;

    [ContextMenu("Добавить 1000 монет (Debug)")]
    public void DebugAddCoins()
    {
        AddGold(1000);
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);
        SanitizeMetaUpgradeLevels();
        LoadGold();
    }

    public void AddGold(int amount)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[CurrencyManager] AddGold called: +{amount}. " +
            $"Before={TotalGold}"
        );
#endif

        int finalAmount = Mathf.RoundToInt(amount * goldGainMultiplier);
        long nextTotal = (long)TotalGold + finalAmount;

        if (nextTotal <= 0)
            TotalGold = 0;
        else if (nextTotal >= int.MaxValue)
            TotalGold = int.MaxValue;
        else
            TotalGold = (int)nextTotal;

        SaveGold();
        OnGoldUpdated?.Invoke(TotalGold);
    }

    public bool SpendGold(int amount)
    {
        if (amount <= 0 || TotalGold < amount)
            return false;

        TotalGold -= amount;
        SaveGold();
        OnGoldUpdated?.Invoke(TotalGold);
        return true;
    }

    private void SaveGold()
    {
        PlayerPrefs.SetInt(GoldKey, TotalGold);
        PlayerPrefs.Save();
    }

    private void LoadGold()
    {
        TotalGold = Mathf.Max(0, PlayerPrefs.GetInt(GoldKey, 0));
    }

    private static void SanitizeMetaUpgradeLevels()
    {
        bool changed = false;

        foreach (string key in MetaUpgradeLevelKeys)
        {
            int storedLevel = PlayerPrefs.GetInt(key, 0);
            int safeLevel = Mathf.Clamp(
                storedLevel,
                0,
                MaxMetaUpgradeLevel
            );

            if (safeLevel == storedLevel)
                continue;

            PlayerPrefs.SetInt(key, safeLevel);
            changed = true;
        }

        if (changed)
            PlayerPrefs.Save();

        MetaProgressionManager.Instance?.ReloadFromStorage();
    }

    public void AddGoldGainPercent(float percent)
    {
        // CurrencyManager persists across MVP reloads while the meta applier is
        // scene-local. Re-applying the same saved bonus must be idempotent.
        goldGainMultiplier = 1f + Mathf.Max(0f, percent);
    }
}
