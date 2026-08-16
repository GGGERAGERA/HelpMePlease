using UnityEngine;

public static class ProductionUpgradeProfiles
{
    public static float DamageMultiplier(int level) =>
        1f + 0.1f * ClampLevel(level);
    public static float MaxHealthBonus(int level) =>
        20f * ClampLevel(level);
    public static float MoveSpeedMultiplier(int level) =>
        1f + 0.1f * ClampLevel(level);
    public static float FireRateMultiplier(int level) =>
        1f + 0.15f * ClampLevel(level);
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
        int fireRate = slots.GetLevel(UpgradeType.FireRatePercent);

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
            weapon =>
            {
                weapon.SetProjectileCountBonus(multishot > 0
                    ? ProductionUpgradeProfiles.MultishotBonus(multishot)
                    : 0);
                weapon.SetFireRateMultiplier(fireRate > 0
                    ? ProductionUpgradeProfiles.FireRateMultiplier(fireRate)
                    : 1f);
            });
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

            case UpgradeType.WeaponDamagePercent:
                RequireCombatModifiers(context).SetRunDamageMultiplier(
                    ProductionUpgradeProfiles.DamageMultiplier(upgradeLevel));
                break;

            case UpgradeType.FireRatePercent:
                ApplyToWeapons(
                    context,
                    weapon => weapon.SetFireRateMultiplier(
                        ProductionUpgradeProfiles.FireRateMultiplier(
                            upgradeLevel)));
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
        public readonly PlayerCombatModifiers CombatModifiers;
        public readonly BaseWeapon[] Weapons;

        public bool IsValid => Player != null;

        public PlayerUpgradeContext(GameObject player)
        {
            Player = player;
            Health = player != null ? player.GetComponent<PlayerHealth>() : null;
            Movement = player != null ? player.GetComponent<CharacterMovement2D>() : null;
            CombatModifiers = player != null ? player.GetComponent<PlayerCombatModifiers>() : null;
            Weapons = player != null ? player.GetComponentsInChildren<BaseWeapon>(true) : null;
        }
    }
}
