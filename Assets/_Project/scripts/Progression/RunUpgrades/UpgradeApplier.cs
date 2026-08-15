using UnityEngine;

public readonly struct WeaponTempoValues
{
    public readonly float DamageMultiplier;
    public readonly float FireRateMultiplier;
    public readonly float VisualScale;

    public WeaponTempoValues(float damage, float fireRate, float visualScale)
    {
        DamageMultiplier = damage;
        FireRateMultiplier = fireRate;
        VisualScale = visualScale;
    }
}

public static class WeaponTempoProfiles
{
    public static WeaponTempoValues Get(UpgradeType type, int upgradeLevel)
    {
        int level = Mathf.Clamp(upgradeLevel, 1, RunItemSlots.MaxItemLevel);

        if (type == UpgradeType.HeavyShot)
        {
            return new WeaponTempoValues(
                level switch { 1 => 1.75f, 2 => 2.25f, _ => 3f },
                level switch { 1 => 0.75f, 2 => 0.65f, _ => 0.55f },
                level switch { 1 => 1.2f, 2 => 1.35f, _ => 1.5f });
        }

        if (type == UpgradeType.Overclock)
        {
            return new WeaponTempoValues(
                level switch { 1 => 0.8f, 2 => 0.7f, _ => 0.6f },
                level switch { 1 => 1.5f, 2 => 1.9f, _ => 2.4f },
                1f);
        }

        return new WeaponTempoValues(1f, 1f, 1f);
    }
}

public static class ProductionUpgradeProfiles
{
    public static float DamageMultiplier(int level) =>
        1f + 0.1f * ClampLevel(level);
    public static float MaxHealthBonus(int level) =>
        20f * ClampLevel(level);
    public static float MoveSpeedMultiplier(int level) =>
        1f + 0.1f * ClampLevel(level);
    public static float XpGainMultiplier(int level) =>
        1f + 0.08f * ClampLevel(level);
    public static float AttackSizeMultiplier(int level) =>
        1f + 0.2f * ClampLevel(level);
    public static float CritChanceBonus(int level) =>
        0.1f * ClampLevel(level);
    public static float RegenerationPerSecond(int level) =>
        ClampLevel(level) switch { 1 => 1f, 2 => 1.5f, _ => 2f };
    public static int MultishotBonus(int level) => ClampLevel(level);

    private static int ClampLevel(int level) =>
        Mathf.Clamp(level, 1, RunItemSlots.MaxItemLevel);
}

/// <summary>
/// Applies one selected upgrade to the current player runtime state.
/// UpgradeManager should not know how upgrades change health, movement, weapons, or combat flags.
/// </summary>
public sealed class UpgradeApplier : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool ApplyDebugProductionBuild(RunItemSlots slots)
    {
        PlayerUpgradeContext context = FindPlayerContext();
        if (!context.IsValid || slots == null)
            return false;

        int damage = slots.GetLevel(UpgradeType.WeaponDamagePercent);
        int maxHealth = slots.GetLevel(UpgradeType.MaxHealthFlat);
        int moveSpeed = slots.GetLevel(UpgradeType.MoveSpeedPercent);
        int xpGain = slots.GetLevel(UpgradeType.XpGainPercent);
        int attackSize = slots.GetLevel(UpgradeType.AttackSizePercent);
        int crit = slots.GetLevel(UpgradeType.CritChance);
        int regeneration = slots.GetLevel(UpgradeType.HpRegeneration);
        int multishot = slots.GetLevel(UpgradeType.Multishot);

        PlayerCombatModifiers modifiers = RequireCombatModifiers(context);
        modifiers.SetRunDamageMultiplier(damage > 0
            ? ProductionUpgradeProfiles.DamageMultiplier(damage)
            : 1f);
        modifiers.SetRunCritChanceBonus(crit > 0
            ? ProductionUpgradeProfiles.CritChanceBonus(crit)
            : 0f);
        modifiers.SetRunAttackSizeMultiplier(attackSize > 0
            ? ProductionUpgradeProfiles.AttackSizeMultiplier(attackSize)
            : 1f);
        context.Health?.SetRunUpgradeMaxHealthBonus(maxHealth > 0
            ? ProductionUpgradeProfiles.MaxHealthBonus(maxHealth)
            : 0f);
        context.Health?.SetRunUpgradeRegeneration(regeneration > 0
            ? ProductionUpgradeProfiles.RegenerationPerSecond(regeneration)
            : 0f);
        context.Movement?.SetRunUpgradeMoveSpeedMultiplier(moveSpeed > 0
            ? ProductionUpgradeProfiles.MoveSpeedMultiplier(moveSpeed)
            : 1f);
        ExperienceManager.Instance?.SetRunUpgradeXpGainMultiplier(xpGain > 0
            ? ProductionUpgradeProfiles.XpGainMultiplier(xpGain)
            : 1f);
        ApplyToWeapons(
            context,
            weapon => weapon.SetProjectileCountBonus(multishot > 0
                ? ProductionUpgradeProfiles.MultishotBonus(multishot)
                : 0));
        return true;
    }
#endif

    public bool Apply(UpgradeData upgrade)
    {
        return Apply(upgrade, 1);
    }

    public bool Apply(UpgradeData upgrade, int upgradeLevel)
    {
        if (upgrade == null)
        {
            Debug.LogWarning("[UpgradeApplier] Cannot apply null upgrade.");
            return false;
        }

        PlayerUpgradeContext context = FindPlayerContext();

        if (!context.IsValid)
        {
            Debug.LogWarning("[UpgradeApplier] Player context not found.");
            return false;
        }

        switch (upgrade.upgradeType)
        {
            case UpgradeType.MaxHealthFlat:
                ApplyMaxHealth(context, upgradeLevel);
                break;

            case UpgradeType.MoveSpeedPercent:
                ApplyMoveSpeed(context, upgradeLevel);
                break;

            case UpgradeType.XpPickupRadiusPercent:
                ApplyXpPickupRadius(context, upgrade.value);
                break;

            case UpgradeType.WeaponDamagePercent:
                RequireCombatModifiers(context).SetRunDamageMultiplier(
                    ProductionUpgradeProfiles.DamageMultiplier(upgradeLevel));
                break;

            case UpgradeType.FireRatePercent:
                ApplyToWeapons(context, weapon => weapon.AddFireRatePercent(upgrade.value));
                break;

            case UpgradeType.ExtraShot:
                ApplyToWeapons(context, weapon => weapon.AddProjectileCount(Mathf.RoundToInt(upgrade.value)));
                break;

            case UpgradeType.CritChance:
                RequireCombatModifiers(context).SetRunCritChanceBonus(
                    ProductionUpgradeProfiles.CritChanceBonus(upgradeLevel));
                break;

            case UpgradeType.XpGainPercent:
                ExperienceManager.Instance?.SetRunUpgradeXpGainMultiplier(
                    ProductionUpgradeProfiles.XpGainMultiplier(upgradeLevel));
                break;

            case UpgradeType.AttackSizePercent:
                RequireCombatModifiers(context).SetRunAttackSizeMultiplier(
                    ProductionUpgradeProfiles.AttackSizeMultiplier(upgradeLevel));
                break;

            case UpgradeType.HpRegeneration:
                context.Health?.SetRunUpgradeRegeneration(
                    ProductionUpgradeProfiles.RegenerationPerSecond(upgradeLevel));
                break;

            case UpgradeType.Multishot:
                ApplyToWeapons(context, weapon => weapon.SetProjectileCountBonus(
                    ProductionUpgradeProfiles.MultishotBonus(upgradeLevel)));
                break;

            case UpgradeType.CritDamage:
                ApplyToWeapons(context, weapon => weapon.AddCritMultiplier(upgrade.value));
                break;

            case UpgradeType.KnockbackPercent:
                ApplyToWeapons(context, weapon => weapon.AddKnockbackPercent(upgrade.value));
                break;

            case UpgradeType.HitExplosionChance:
                RequireCombatModifiers(context).hitExplosionChance += upgrade.value;
                break;

            case UpgradeType.StationaryFireRateRamp:
                ApplyStationaryFireRateRamp(context, upgrade.value);
                break;

            case UpgradeType.DoubleDamageWithInaccuracy:
                ApplyDoubleDamageWithInaccuracy(context);
                break;

            case UpgradeType.Pierce:
                ApplyToCompatibleWeapons(
                    context,
                    WeaponUpgradeCapability.Pierce,
                    weapon => weapon.SetPierceBonus(upgradeLevel));
                break;

            case UpgradeType.Ricochet:
                ApplyToCompatibleWeapons(
                    context,
                    WeaponUpgradeCapability.Ricochet,
                    weapon => weapon.SetRicochetBonus(upgradeLevel));
                break;

            case UpgradeType.HeavyShot:
                ApplyTempoProfile(context, UpgradeType.HeavyShot, upgradeLevel);
                break;

            case UpgradeType.Overclock:
                ApplyTempoProfile(context, UpgradeType.Overclock, upgradeLevel);
                break;

            default:
                Debug.LogWarning($"[UpgradeApplier] Upgrade type is not implemented: {upgrade.upgradeType}");
                return false;
        }

        return true;
    }

    private PlayerUpgradeContext FindPlayerContext()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);

        if (player == null)
            return PlayerUpgradeContext.Empty;

        return new PlayerUpgradeContext(player);
    }

    private void ApplyMaxHealth(PlayerUpgradeContext context, int level)
    {
        if (context.Health == null)
            return;

        context.Health.SetRunUpgradeMaxHealthBonus(
            ProductionUpgradeProfiles.MaxHealthBonus(level));
    }

    private void ApplyMoveSpeed(PlayerUpgradeContext context, int level)
    {
        if (context.Movement == null)
            return;

        context.Movement.SetRunUpgradeMoveSpeedMultiplier(
            ProductionUpgradeProfiles.MoveSpeedMultiplier(level));
    }

    private void ApplyXpPickupRadius(PlayerUpgradeContext context, float value)
    {
        if (context.PickupRadius == null)
            return;

        context.PickupRadius.AddRadiusPercent(value);
    }

    private void ApplyToWeapons(PlayerUpgradeContext context, System.Action<BaseWeapon> apply)
    {
        if (context.Weapons == null || context.Weapons.Length == 0)
            return;

        foreach (BaseWeapon weapon in context.Weapons)
        {
            if (weapon == null)
                continue;

            apply?.Invoke(weapon);
        }
    }

    private void ApplyToCompatibleWeapons(
        PlayerUpgradeContext context,
        WeaponUpgradeCapability capability,
        System.Action<BaseWeapon> apply)
    {
        ApplyToWeapons(context, weapon =>
        {
            if ((weapon.UpgradeCapabilities & capability) == capability)
                apply?.Invoke(weapon);
        });
    }

    private void ApplyTempoProfile(
        PlayerUpgradeContext context,
        UpgradeType type,
        int upgradeLevel)
    {
        WeaponTempoValues values = WeaponTempoProfiles.Get(type, upgradeLevel);

        ApplyToWeapons(
            context,
            weapon => weapon.SetTempoProfile(
                values.DamageMultiplier,
                values.FireRateMultiplier,
                values.VisualScale));
    }

    private void ApplyDoubleDamageWithInaccuracy(PlayerUpgradeContext context)
    {
        PlayerCombatModifiers modifiers = RequireCombatModifiers(context);
        modifiers.bonusDamageMultiplier *= 2f;
        modifiers.accuracyPenaltyDegrees += 12f;
    }

    private PlayerCombatModifiers RequireCombatModifiers(PlayerUpgradeContext context)
    {
        if (context.CombatModifiers != null)
            return context.CombatModifiers;

        return context.Player.AddComponent<PlayerCombatModifiers>();
    }

    private readonly struct PlayerUpgradeContext
    {
        public static PlayerUpgradeContext Empty => new PlayerUpgradeContext(null);

        public readonly GameObject Player;
        public readonly PlayerHealth Health;
        public readonly CharacterMovement2D Movement;
        public readonly PlayerPickupRadius PickupRadius;
        public readonly PlayerCombatModifiers CombatModifiers;
        public readonly BaseWeapon[] Weapons;

        public bool IsValid => Player != null;

        public PlayerUpgradeContext(GameObject player)
        {
            Player = player;
            Health = player != null ? player.GetComponent<PlayerHealth>() : null;
            Movement = player != null ? player.GetComponent<CharacterMovement2D>() : null;
            PickupRadius = player != null ? player.GetComponent<PlayerPickupRadius>() : null;
            CombatModifiers = player != null ? player.GetComponent<PlayerCombatModifiers>() : null;
            Weapons = player != null ? player.GetComponentsInChildren<BaseWeapon>(true) : null;
        }
    }

    private void ApplyStationaryFireRateRamp(PlayerUpgradeContext context, float value)
    {
        PlayerCombatModifiers modifiers = RequireCombatModifiers(context);

        modifiers.stationaryFireRateRamp = true;
        modifiers.stationaryFireRateRampMaxBonus += value;
    }
}
