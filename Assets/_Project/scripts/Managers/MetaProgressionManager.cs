using UnityEngine;

public class MetaProgressionManager : MonoBehaviour
{
    public static MetaProgressionManager Instance { get; private set; }

    private const string HpLevelKey = "META_HP_LEVEL";
    private const string DamageLevelKey = "META_DAMAGE_LEVEL";
    private const string MoveSpeedLevelKey = "META_MOVE_SPEED_LEVEL";

    public int HpLevel { get; private set; }
    public int DamageLevel { get; private set; }
    public int MoveSpeedLevel { get; private set; }

    private void Awake()
    {
        Instance = this;
        Load();
    }

    public int GetUpgradeCost(int currentLevel)
    {
        return 25 + currentLevel * 25;
    }

    public void BuyHp()
    {
        HpLevel = TryBuy(HpLevel, HpLevelKey);
    }

    public void BuyDamage()
    {
        DamageLevel = TryBuy(DamageLevel, DamageLevelKey);
    }

    public void BuyMoveSpeed()
    {
        MoveSpeedLevel = TryBuy(MoveSpeedLevel, MoveSpeedLevelKey);
    }

    private int TryBuy(int currentLevel, string key)
    {
        int cost = GetUpgradeCost(currentLevel);

        if (CurrencyManager.Instance == null)
            return currentLevel;

        if (!CurrencyManager.Instance.SpendGold(cost))
            return currentLevel;

        currentLevel++;

        PlayerPrefs.SetInt(key, currentLevel);
        PlayerPrefs.Save();

        return currentLevel;
    }

    private void Load()
    {
        HpLevel = PlayerPrefs.GetInt(HpLevelKey, 0);
        DamageLevel = PlayerPrefs.GetInt(DamageLevelKey, 0);
        MoveSpeedLevel = PlayerPrefs.GetInt(MoveSpeedLevelKey, 0);
    }
}