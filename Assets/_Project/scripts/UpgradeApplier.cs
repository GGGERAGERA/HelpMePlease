using UnityEngine;

/// <summary>
/// Applies one selected upgrade to the current player runtime state.
/// UpgradeManager should not know how upgrades change health, movement, weapons, or combat flags.
/// </summary>
public sealed class UpgradeApplier : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    public bool Apply(UpgradeData upgrade)
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
                ApplyMaxHealth(context, upgrade.value);
                break;

            case UpgradeType.MoveSpeedPercent:
                ApplyMoveSpeed(context, upgrade.value);
                break;

            case UpgradeType.XpPickupRadiusPercent:
                ApplyXpPickupRadius(context, upgrade.value);
                break;

            case UpgradeType.WeaponDamagePercent:
                ApplyToWeapons(context, weapon => weapon.AddDamagePercent(upgrade.value));
                break;

            case UpgradeType.FireRatePercent:
                ApplyToWeapons(context, weapon => weapon.AddFireRatePercent(upgrade.value));
                break;

            case UpgradeType.ExtraShot:
                ApplyToWeapons(context, weapon => weapon.AddProjectileCount(Mathf.RoundToInt(upgrade.value)));
                break;

            case UpgradeType.CritChance:
                ApplyToWeapons(context, weapon => weapon.AddCritChance(upgrade.value));
                break;

            case UpgradeType.CritDamage:
                ApplyToWeapons(context, weapon => weapon.AddCritMultiplier(upgrade.value));
                break;

            case UpgradeType.KnockbackPercent:
                ApplyToWeapons(context, weapon => weapon.AddKnockbackPercent(upgrade.value));
                break;

            case UpgradeType.EveryFifthAttackExtraShot:
                RequireCombatModifiers(context).everyFifthAttackExtraShot = true;
                break;

            case UpgradeType.HitExplosionChance:
                RequireCombatModifiers(context).hitExplosionChance += upgrade.value;
                break;

            case UpgradeType.EnemyDeathExplosion:
                ApplyEnemyDeathExplosion(context, upgrade.value);
                break;

            case UpgradeType.StationaryFireRateRamp:
                RequireCombatModifiers(context).stationaryFireRateRamp = true;
                break;

            case UpgradeType.DoubleDamageWithInaccuracy:
                ApplyDoubleDamageWithInaccuracy(context);
                break;

            case UpgradeType.LowHpPower:
                RequireCombatModifiers(context).lowHpPower = true;
                break;

            case UpgradeType.RandomExtraShotsChance:
                RequireCombatModifiers(context).randomExtraShotsChance += upgrade.value;
                break;

            case UpgradeType.CircularBurst:
                RequireCombatModifiers(context).circularBurst = true;
                break;

            case UpgradeType.NukeEveryTenKills:
                RequireCombatModifiers(context).nukeEveryTenKills = true;
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

    private void ApplyMaxHealth(PlayerUpgradeContext context, float value)
    {
        if (context.Health == null)
            return;

        context.Health.AddMaxHealth(value);
    }

    private void ApplyMoveSpeed(PlayerUpgradeContext context, float value)
    {
        if (context.Movement == null)
            return;

        context.Movement.AddMoveSpeedPercent(value);
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

    private void ApplyEnemyDeathExplosion(PlayerUpgradeContext context, float value)
    {
        PlayerCombatModifiers modifiers = RequireCombatModifiers(context);
        modifiers.enemyDeathExplosion = true;
        modifiers.deathExplosionDamageBonus += value;
    }

    private void ApplyDoubleDamageWithInaccuracy(PlayerUpgradeContext context)
    {
        PlayerCombatModifiers modifiers = RequireCombatModifiers(context);
        modifiers.doubleDamageWithInaccuracy = true;
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
}
