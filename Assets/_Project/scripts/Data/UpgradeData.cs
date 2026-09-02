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
    [System.Obsolete("Serialized ID reserved; legacy upgrade removed.")]
    XpPickupRadiusPercent = 4,
    MoveSpeedPercent = 5,

    [System.Obsolete("Serialized ID reserved; legacy upgrade removed.")]
    ExtraShot = 6,
    [System.Obsolete("Serialized ID reserved; upgrade removed from production.")]
    EveryFifthAttackExtraShot = 7,
    [System.Obsolete("Serialized ID reserved; legacy upgrade removed.")]
    HitExplosionChance = 8,
    [System.Obsolete("Serialized ID reserved; upgrade removed in Phase 7B.")]
    EnemyDeathExplosion = 9,
    [System.Obsolete("Serialized ID reserved; legacy upgrade removed.")]
    CritDamage = 10,
    [System.Obsolete("Serialized ID reserved; legacy upgrade removed.")]
    KnockbackPercent = 11,

    [System.Obsolete("Serialized ID reserved; legacy upgrade removed.")]
    StationaryFireRateRamp = 12,
    [System.Obsolete("Serialized ID reserved; legacy upgrade removed.")]
    DoubleDamageWithInaccuracy = 13,
    [System.Obsolete("Serialized ID reserved; upgrade removed in Phase 7B.")]
    LowHpPower = 14,

    [System.Obsolete("Serialized ID reserved; upgrade removed in Phase 7B.")]
    RandomExtraShotsChance = 15,
    [System.Obsolete("Serialized ID reserved; upgrade removed in Phase 7B.")]
    CircularBurst = 16,
    [System.Obsolete("Serialized ID reserved; upgrade removed in Phase 7B.")]
    NukeEveryTenKills = 17,

    [System.Obsolete("Serialized ID reserved; legacy upgrade removed.")]
    Pierce = 18,
    [System.Obsolete("Serialized ID reserved; legacy upgrade removed.")]
    Ricochet = 19,
    [System.Obsolete("Serialized ID reserved; legacy upgrade removed.")]
    HeavyShot = 20,
    [System.Obsolete("Serialized ID reserved; legacy upgrade removed.")]
    Overclock = 21,

    XpGainPercent = 22,
    AttackSizePercent = 23,
    HpRegeneration = 24,
    Multishot = 25,

    // Runtime-only cards owned by OrbitalRewardProvider. Existing serialized
    // legacy IDs remain unchanged.
    OrbitalReward = 100
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

public static class ProductionUpgradePresentation
{
    public static string GetCardDescription(UpgradeData upgrade)
    {
        if (upgrade == null)
            return string.Empty;

        if (upgrade.upgradeType != UpgradeType.HpRegeneration)
            return upgrade.description;

        RunItemSlots slots = RunStateManager.Instance != null
            ? RunStateManager.Instance.ItemSlots
            : null;
        int currentLevel = slots != null ? slots.GetLevel(upgrade) : 0;
        int nextLevel = Mathf.Clamp(
            currentLevel + 1,
            1,
            RunItemSlots.MaxItemLevel);
        float currentValue = currentLevel > 0
            ? ProductionUpgradeProfiles.RegenerationPerSecond(currentLevel)
            : 0f;
        float nextValue =
            ProductionUpgradeProfiles.RegenerationPerSecond(nextLevel);
        float increment = Mathf.Max(0f, nextValue - currentValue);
        return $"Регенерация +{increment:0.#} HP/с";
    }
}
