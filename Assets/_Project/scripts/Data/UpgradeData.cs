using UnityEngine;

public enum UpgradeCategory
{
    Numeric,
    Behavior
}

public enum UpgradeType
{
    WeaponDamagePercent,
    FireRatePercent,
    MaxHealthFlat,
    CritChance,
    XpPickupRadiusPercent,
    MoveSpeedPercent,

    ExtraShot,
    EveryFifthAttackExtraShot,
    HitExplosionChance,
    EnemyDeathExplosion,
    CritDamage,
    KnockbackPercent,

    StationaryFireRateRamp,
    DoubleDamageWithInaccuracy,
    LowHpPower,

    RandomExtraShotsChance,
    CircularBurst,
    NukeEveryTenKills
}

[CreateAssetMenu(fileName = "New UpgradeData", menuName = "Game/Upgrade Data")]
public class UpgradeData : ScriptableObject
{
    [Header("UI")]
    public string upgradeName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Rules")]
    public UpgradeCategory category;
    public int minPlayerLevel = 1;

    [Header("Effect")]
    public UpgradeType upgradeType;

    [Tooltip("Значение эффекта. Например: 0.2 = +20%, 1 = +1 HP, 0.1 = +10%")]
    public float value = 1f;
}
