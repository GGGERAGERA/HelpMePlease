using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance;

    [Header("UI")]
    [SerializeField] private UpgradePanelView upgradePanelView;

    [Header("Available Upgrades")]
    public UpgradeData[] allUpgrades;

    private bool isChoosingUpgrade = false;
    public bool IsChoosingUpgrade => isChoosingUpgrade;

    private UpgradeRoller upgradeRoller;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (upgradePanelView != null)
            upgradePanelView.Hide();

        upgradeRoller = new UpgradeRoller(allUpgrades);
    }

    public void ShowUpgradeChoices()
    {
        if (allUpgrades == null || allUpgrades.Length == 0)
        {
            Debug.LogWarning("UpgradeManager: allUpgrades is empty.");
            return;
        }

        if (upgradePanelView == null)
        {
            Debug.LogWarning("UpgradeManager: upgradePanelView is not assigned.");
            return;
        }

        int playerLevel = ExperienceManager.Instance != null
            ? ExperienceManager.Instance.currentLevel
            : 1;

        List<UpgradeData> choices = upgradeRoller.RollChoices(playerLevel, 3);

        isChoosingUpgrade = true;
        Time.timeScale = 0f;

        upgradePanelView.Show(playerLevel, choices, SelectUpgrade);
    }

    public void SelectUpgrade(UpgradeData upgrade)
    {
        ApplyUpgrade(upgrade);

        if (upgradePanelView != null)
            upgradePanelView.Hide();

        isChoosingUpgrade = false;
        Time.timeScale = 1f;
    }

    private void ApplyUpgrade(UpgradeData upgrade)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning("UpgradeManager: Player not found.");
            return;
        }

        PlayerStats stats = player.GetComponent<PlayerStats>();
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        CharacterMovement2D movement = player.GetComponent<CharacterMovement2D>();
        BaseWeapon[] weapons = player.GetComponentsInChildren<BaseWeapon>(true);
        PlayerPickupRadius pickupRadius = player.GetComponent<PlayerPickupRadius>();
        PlayerCombatModifiers combatModifiers = player.GetComponent<PlayerCombatModifiers>();

        switch (upgrade.upgradeType)
        {
            case UpgradeType.MaxHealthFlat:
                ApplyMaxHealthUpgrade(health, stats, upgrade.value);
                break;

            case UpgradeType.MoveSpeedPercent:
                ApplyMoveSpeedPercentUpgrade(movement, stats, upgrade.value);
                break;
            case UpgradeType.XpPickupRadiusPercent:
                ApplyXpPickupRadiusUpgrade(pickupRadius, upgrade.value);
                break;
            case UpgradeType.WeaponDamagePercent:
                ApplyWeaponDamagePercentUpgrade(weapons, upgrade.value);
                break;

            case UpgradeType.FireRatePercent:
                ApplyFireRateUpgrade(weapons, upgrade.value);
                break;

            case UpgradeType.ExtraShot:
                ApplyProjectileCountUpgrade(weapons, upgrade.value);
                break;

            case UpgradeType.CritChance:
                ApplyCritChanceUpgrade(weapons, upgrade.value);
                break;

            case UpgradeType.CritDamage:
                ApplyCritDamageUpgrade(weapons, upgrade.value);
                break;

            case UpgradeType.KnockbackPercent:
                ApplyKnockbackUpgrade(weapons, upgrade.value);
                break;

            case UpgradeType.EveryFifthAttackExtraShot:
                ApplyEveryFifthAttackExtraShot(combatModifiers);
                break;

            case UpgradeType.HitExplosionChance:
                ApplyHitExplosionChance(combatModifiers, upgrade.value);
                break;

            case UpgradeType.EnemyDeathExplosion:
                ApplyEnemyDeathExplosion(combatModifiers, upgrade.value);
                break;
            case UpgradeType.StationaryFireRateRamp:
                ApplyStationaryFireRateRamp(combatModifiers);
                break;
            case UpgradeType.DoubleDamageWithInaccuracy:
                ApplyDoubleDamageWithInaccuracy(combatModifiers);
                break;
            case UpgradeType.LowHpPower:
                ApplyLowHpPower(combatModifiers);
                break;
            case UpgradeType.RandomExtraShotsChance:
                ApplyRandomExtraShotsChance(combatModifiers, upgrade.value);
                break;
            case UpgradeType.CircularBurst:
                ApplyCircularBurst(combatModifiers);
                break;
            case UpgradeType.NukeEveryTenKills:
                ApplyNukeEveryTenKills(combatModifiers);
                break;

            default:
                Debug.LogWarning("UpgradeManager: upgrade not implemented yet: " + upgrade.upgradeType);
                break;
        }
    }

    private void ApplyMaxHealthUpgrade(PlayerHealth health, PlayerStats stats, float value)
    {
        if (health != null)
        {
            health.maxHealth += value;
            health.currentHealth += value;

            HUDManager.Instance?.SetHealth(
             health.CurrentHealth,
             health.MaxHealth
        );
        }

        if (stats != null)
        {
            stats.maxHealth += value;
            stats.currentHealth += value;
        }
    }


    private void ApplyFireRateUpgrade(BaseWeapon[] weapons, float value)
    {
        foreach (BaseWeapon weapon in weapons)
        {
            if (weapon != null)
                weapon.AddFireRatePercent(value);
        }
    }

    private void ApplyProjectileCountUpgrade(BaseWeapon[] weapons, float value)
    {
        int amount = Mathf.RoundToInt(value);

        foreach (BaseWeapon weapon in weapons)
        {
            if (weapon == null)
                continue;

            weapon.AddProjectileCount(amount);
        }
    }

    private void ApplyMoveSpeedPercentUpgrade(CharacterMovement2D movement, PlayerStats stats, float value)
    {
        if (movement != null)
            movement.speed *= 1f + value;

        if (stats != null)
            stats.moveSpeed *= 1f + value;
    }

    private void ApplyWeaponDamagePercentUpgrade(BaseWeapon[] weapons, float value)
    {
        foreach (BaseWeapon weapon in weapons)
        {
            if (weapon != null)
                weapon.AddDamagePercent(value);
        }
    }

    private void ApplyCritChanceUpgrade(BaseWeapon[] weapons, float value)
    {
        foreach (BaseWeapon weapon in weapons)
        {
            if (weapon != null)
                weapon.AddCritChance(value);
        }
    }

    private void ApplyCritDamageUpgrade(BaseWeapon[] weapons, float value)
    {
        foreach (BaseWeapon weapon in weapons)
        {
            if (weapon != null)
                weapon.AddCritMultiplier(value);
        }
    }

    private void ApplyKnockbackUpgrade(BaseWeapon[] weapons, float value)
    {
        foreach (BaseWeapon weapon in weapons)
        {
            if (weapon != null)
                weapon.AddKnockbackPercent(value);
        }
    }
    private void ApplyXpPickupRadiusUpgrade(PlayerPickupRadius pickupRadius, float value)
    {
        if (pickupRadius != null)
            pickupRadius.AddRadiusPercent(value);
    }

    private void ApplyEveryFifthAttackExtraShot(PlayerCombatModifiers combatModifiers)
    {
        if (combatModifiers != null)
            combatModifiers.everyFifthAttackExtraShot = true;
    }

    private void ApplyHitExplosionChance(PlayerCombatModifiers combatModifiers, float value)
    {
        if (combatModifiers != null)
            combatModifiers.hitExplosionChance += value;
    }

    private void ApplyEnemyDeathExplosion(PlayerCombatModifiers combatModifiers, float value)
    {
        if (combatModifiers == null)
            return;

        combatModifiers.enemyDeathExplosion = true;
        combatModifiers.deathExplosionDamageBonus += value;
    }
    private void ApplyStationaryFireRateRamp(PlayerCombatModifiers combatModifiers)
    {
        if (combatModifiers != null)
            combatModifiers.stationaryFireRateRamp = true;
    }
    private void ApplyDoubleDamageWithInaccuracy(PlayerCombatModifiers combatModifiers)
    {
        if (combatModifiers == null)
            return;

        combatModifiers.doubleDamageWithInaccuracy = true;
        combatModifiers.bonusDamageMultiplier *= 2f;
        combatModifiers.accuracyPenaltyDegrees += 12f;
    }
    private void ApplyLowHpPower(PlayerCombatModifiers combatModifiers)
    {
        if (combatModifiers != null)
            combatModifiers.lowHpPower = true;
    }
    private void ApplyRandomExtraShotsChance(PlayerCombatModifiers combatModifiers, float value)
    {
        if (combatModifiers != null)
            combatModifiers.randomExtraShotsChance += value;
    }
    private void ApplyCircularBurst(PlayerCombatModifiers combatModifiers)
    {
        if (combatModifiers != null)
            combatModifiers.circularBurst = true;
    }
    private void ApplyNukeEveryTenKills(PlayerCombatModifiers combatModifiers)
    {
        if (combatModifiers != null)
            combatModifiers.nukeEveryTenKills = true;
    }
}