using UnityEngine;

public class MetaProgressionManager : MonoBehaviour
{
    public static MetaProgressionManager Instance { get; private set; }

    private const string HpLevelKey = "META_HP_LEVEL";
    private const string DamageLevelKey = "META_DAMAGE_LEVEL";
    private const string MoveSpeedLevelKey = "META_MOVE_SPEED_LEVEL";
    private const string AttackSpeedLevelKey = "META_ATTACK_SPEED_LEVEL";
    private const string CritDamageLevelKey = "META_CRIT_DAMAGE_LEVEL";
    private const string CritChanceLevelKey = "META_CRIT_CHANCE_LEVEL";
    private const string RicochetLevelKey = "META_RICOCHET_LEVEL";
    private const string PiercingLevelKey = "META_PIERCING_LEVEL";
    private const string MultishotLevelKey = "META_MULTISHOT_LEVEL";
    private const string KnockbackLevelKey = "META_KNOCKBACK_LEVEL";

    public int HpLevel { get; private set; }
    public int DamageLevel { get; private set; }
    public int MoveSpeedLevel { get; private set; }
    public int AttackSpeedLevel { get; private set; }
    public int CritDamageLevel { get; private set; }
    public int CritChanceLevel { get; private set; }
    public int RicochetLevel { get; private set; }
    public int PiercingLevel { get; private set; }
    public int MultishotLevel { get; private set; }
    public int KnockbackLevel { get; private set; }


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    public int GetUpgradeCost(int currentLevel)
    {
        return 25 + currentLevel * 25;
    }

    public bool BuyHp()
    {
        int newLevel = TryBuy(HpLevel, HpLevelKey);

        if (newLevel == HpLevel)
            return false;

        HpLevel = newLevel;
        return true;
    }

    public bool BuyDamage()
    {
        int newLevel = TryBuy(DamageLevel, DamageLevelKey);

        if (newLevel == DamageLevel)
            return false;

        DamageLevel = newLevel;
        return true;
    }

    public bool BuyMoveSpeed()
    {
        int newLevel = TryBuy(MoveSpeedLevel, MoveSpeedLevelKey);

        if (newLevel == MoveSpeedLevel)
            return false;

        MoveSpeedLevel = newLevel;
        return true;
    }

    public bool BuyAttackSpeed()
    {
        int newLevel = TryBuy(AttackSpeedLevel, AttackSpeedLevelKey);

        if (newLevel == AttackSpeedLevel)
            return false;

        AttackSpeedLevel = newLevel;
        return true;
    }
    public bool BuyCritDamage()
    {
        int newLevel = TryBuy(CritDamageLevel, CritDamageLevelKey);

        if (newLevel == CritDamageLevel)
            return false;

        CritDamageLevel = newLevel;
        return true;

    }
    public bool BuyCritChance()
    {
        int newLevel = TryBuy(CritChanceLevel, CritChanceLevelKey);

        if (newLevel == CritChanceLevel)
            return false;

        CritChanceLevel = newLevel;
        return true;
    }
    public bool BuyRicochet()
    {
        int newLevel = TryBuy(RicochetLevel, RicochetLevelKey);

        if (newLevel == RicochetLevel)
            return false;

        RicochetLevel = newLevel;
        return true;
    }
    public bool BuyPiercing()
    {
        int newLevel = TryBuy(PiercingLevel, PiercingLevelKey);

        if (newLevel == PiercingLevel)
            return false;

        PiercingLevel = newLevel;
        return true;
    }
    public bool BuyMultishot()
    {
        int newLevel = TryBuy(MultishotLevel, MultishotLevelKey);

        if (newLevel == MultishotLevel)
            return false;

        MultishotLevel = newLevel;
        return true;
    }
    public bool BuyKnockback()
    {
        int newLevel = TryBuy(KnockbackLevel, KnockbackLevelKey);

        if (newLevel == KnockbackLevel)
            return false;

        KnockbackLevel = newLevel;
        return true;
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
        AttackSpeedLevel = PlayerPrefs.GetInt(AttackSpeedLevelKey, 0);
        CritDamageLevel = PlayerPrefs.GetInt(CritDamageLevelKey, 0);
        CritChanceLevel = PlayerPrefs.GetInt(CritChanceLevelKey, 0);
        RicochetLevel = PlayerPrefs.GetInt(RicochetLevelKey, 0);
        PiercingLevel = PlayerPrefs.GetInt(PiercingLevelKey, 0);
        MultishotLevel = PlayerPrefs.GetInt(MultishotLevelKey, 0);
        KnockbackLevel = PlayerPrefs.GetInt(KnockbackLevelKey, 0);  
    }
}