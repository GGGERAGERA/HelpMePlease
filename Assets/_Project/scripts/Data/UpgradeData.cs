using UnityEngine;

public enum UpgradeCategory
{
    Numeric,
    Behavior
}

public enum UpgradeType
{
    WeaponDamagePercent = 0,
    FireRatePercent = 1,
    MaxHealthFlat = 2,
    CritChance = 3,
    XpPickupRadiusPercent = 4,
    MoveSpeedPercent = 5,

    ExtraShot = 6,
    [System.Obsolete("Serialized ID reserved; upgrade removed from production.")]
    EveryFifthAttackExtraShot = 7,
    HitExplosionChance = 8,
    [System.Obsolete("Serialized ID reserved; upgrade removed in Phase 7B.")]
    EnemyDeathExplosion = 9,
    CritDamage = 10,
    KnockbackPercent = 11,

    StationaryFireRateRamp = 12,
    DoubleDamageWithInaccuracy = 13,
    [System.Obsolete("Serialized ID reserved; upgrade removed in Phase 7B.")]
    LowHpPower = 14,

    [System.Obsolete("Serialized ID reserved; upgrade removed in Phase 7B.")]
    RandomExtraShotsChance = 15,
    [System.Obsolete("Serialized ID reserved; upgrade removed in Phase 7B.")]
    CircularBurst = 16,
    [System.Obsolete("Serialized ID reserved; upgrade removed in Phase 7B.")]
    NukeEveryTenKills = 17,

    Pierce = 18,
    Ricochet = 19,
    HeavyShot = 20,
    Overclock = 21
}

[CreateAssetMenu(fileName = "New UpgradeData", menuName = "Game/Upgrade Data")]
public class UpgradeData : ScriptableObject
{
    [Header("Unlock")]
    [Tooltip("Optional. Use StationLevelRequirement to gate this upgrade through the shared unlock service.")]
    public UnlockableContentData unlockData;

    [Header("UI")]
    public string upgradeName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Rules")]
    public UpgradeCategory category;
    public int minPlayerLevel = 1;

    [Tooltip("Capabilities required from the currently equipped weapon.")]
    public WeaponUpgradeCapability requiredWeaponCapabilities;

    [Tooltip("Only one distinct owned upgrade may occupy this group.")]
    public string exclusiveGroup;

    [Header("Effect")]
    public UpgradeType upgradeType;

    [Tooltip("Значение эффекта. Например: 0.2 = +20%, 1 = +1 HP, 0.1 = +10%")]
    public float value = 1f;
}
