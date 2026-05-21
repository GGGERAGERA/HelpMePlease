using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance;

    [Header("UI")]
    public GameObject chooseUpgradesPanel;
    public UpgradeButtonUI[] upgradeButtons;

    [Header("Available Upgrades")]
    public UpgradeData[] allUpgrades;

    private bool isChoosingUpgrade = false;
    public bool IsChoosingUpgrade => isChoosingUpgrade;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (chooseUpgradesPanel != null)
            chooseUpgradesPanel.SetActive(false);
    }

    public void ShowUpgradeChoices()
    {
        if (allUpgrades == null || allUpgrades.Length == 0)
        {
            Debug.LogWarning("UpgradeManager: allUpgrades is empty.");
            return;
        }

        if (chooseUpgradesPanel == null)
        {
            Debug.LogWarning("UpgradeManager: chooseUpgradesPanel is not assigned.");
            return;
        }

        isChoosingUpgrade = true;

        Time.timeScale = 0f;
        chooseUpgradesPanel.SetActive(true);

        List<UpgradeData> randomUpgrades = GetRandomUpgrades(3);

        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            if (upgradeButtons[i] == null)
                continue;

            if (i < randomUpgrades.Count)
            {
                upgradeButtons[i].Setup(randomUpgrades[i], this);
            }
            else
            {
                upgradeButtons[i].gameObject.SetActive(false);
            }
        }
    }

    public void SelectUpgrade(UpgradeData upgrade)
    {
        ApplyUpgrade(upgrade);

        if (chooseUpgradesPanel != null)
            chooseUpgradesPanel.SetActive(false);

        isChoosingUpgrade = false;
        Time.timeScale = 1f;
    }

    private List<UpgradeData> GetRandomUpgrades(int count)
    {
        List<UpgradeData> available = new List<UpgradeData>();

        foreach (UpgradeData upgrade in allUpgrades)
        {
            if (upgrade != null)
                available.Add(upgrade);
        }

        List<UpgradeData> result = new List<UpgradeData>();

        while (result.Count < count && available.Count > 0)
        {
            int randomIndex = Random.Range(0, available.Count);
            result.Add(available[randomIndex]);
            available.RemoveAt(randomIndex);
        }

        return result;
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

        switch (upgrade.upgradeType)
        {
            case UpgradeType.MaxHealth:
                ApplyMaxHealthUpgrade(health, stats, upgrade.value);
                break;

            case UpgradeType.Heal:
                ApplyHealUpgrade(health, upgrade.value);
                break;

            case UpgradeType.MoveSpeed:
                ApplyMoveSpeedUpgrade(movement, stats, upgrade.value);
                break;

            case UpgradeType.BaseDamage:
                ApplyBaseDamageUpgrade(stats, upgrade.value);
                break;

            case UpgradeType.DamageMultiplier:
                ApplyDamageMultiplierUpgrade(stats, upgrade.value);
                break;

            case UpgradeType.WeaponDamage:
                ApplyWeaponDamageUpgrade(weapons, upgrade.value);
                break;

            case UpgradeType.FireRatePercent:
                ApplyFireRateUpgrade(weapons, upgrade.value);
                break;

            case UpgradeType.WeaponRange:
                ApplyWeaponRangeUpgrade(weapons, upgrade.value);
                break;

            case UpgradeType.OrbitRadius:
                ApplyOrbitRadiusUpgrade(weapons, upgrade.value);
                break;
            case UpgradeType.ProjectileCount:
                ApplyProjectileCountUpgrade(weapons, upgrade.value);
                break;
            case UpgradeType.ProjectilePierce:
                ApplyPierceUpgrade(weapons, upgrade.value);
                break;
            case UpgradeType.ProjectileRicochet:
                ApplyRicochetUpgrade(weapons, upgrade.value);
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

    private void ApplyHealUpgrade(PlayerHealth health, float value)
    {
        if (health != null)
        {
            health.Heal(Mathf.RoundToInt(value));
        }
    }

    private void ApplyMoveSpeedUpgrade(CharacterMovement2D movement, PlayerStats stats, float value)
    {
        if (movement != null)
            movement.speed += value;

        if (stats != null)
            stats.moveSpeed += value;
    }

    private void ApplyBaseDamageUpgrade(PlayerStats stats, float value)
    {
        if (stats != null)
            stats.IncreaseDamage(Mathf.RoundToInt(value));
    }

    private void ApplyDamageMultiplierUpgrade(PlayerStats stats, float value)
    {
        if (stats != null)
            stats.IncreaseDamageMultiplier(value);
    }

    private void ApplyWeaponDamageUpgrade(BaseWeapon[] weapons, float value)
    {
        foreach (BaseWeapon weapon in weapons)
        {
            if (weapon != null)
                weapon.AddRuntimeDamage(value);
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

    private void ApplyWeaponRangeUpgrade(BaseWeapon[] weapons, float value)
    {
        foreach (BaseWeapon weapon in weapons)
        {
            if (weapon != null)
                weapon.AddRuntimeRange(value);
        }
    }

    private void ApplyOrbitRadiusUpgrade(BaseWeapon[] weapons, float value)
    {
        foreach (BaseWeapon weapon in weapons)
        {
            OrbitalWeapon orbitalWeapon = weapon as OrbitalWeapon;

            if (orbitalWeapon != null)
                orbitalWeapon.orbitRadius += value;
        }
    }
    private void ApplyProjectileCountUpgrade(BaseWeapon[] weapons, float value)
    {
        foreach (BaseWeapon weapon in weapons)
        {
            if (weapon != null)
            {
                weapon.AddProjectileCount(Mathf.RoundToInt(value));
            }
        }
    }
    private void ApplyPierceUpgrade(BaseWeapon[] weapons, float value)
    {
        foreach (BaseWeapon weapon in weapons)
        {
            if (weapon != null)
            {
                weapon.AddPierce(Mathf.RoundToInt(value));
            }
        }
    }
    private void ApplyRicochetUpgrade(BaseWeapon[] weapons, float value)
    {
        foreach (BaseWeapon weapon in weapons)
        {
            if (weapon != null)
            {
                weapon.AddRicochet(Mathf.RoundToInt(value));
            }
        }
    }
}