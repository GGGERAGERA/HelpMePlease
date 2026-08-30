using UnityEngine;

/// <summary>
/// Builds the character/weapon parts shared by scene-specific player spawners.
/// It owns no state and never persists scene objects.
/// </summary>
public static class PlayerLoadoutFactory
{
    public static void ApplyCharacterStats(
        GameObject player,
        CharacterData characterData)
    {
        if (player == null || characterData == null)
            return;

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.maxHealth = characterData.maxHealth;
            health.currentHealth = characterData.maxHealth;
        }

        CharacterMovement2D movement =
            player.GetComponent<CharacterMovement2D>();
        if (movement != null)
            movement.speed = characterData.moveSpeed;
    }

    public static BaseWeapon SpawnWeapon(
        GameObject player,
        WeaponData weaponData,
        CharacterCombatType combatType,
        string weaponPointName)
    {
        if (player == null || weaponData == null)
            return null;

        if (weaponData.weaponPrefab == null)
        {
            Debug.LogWarning(
                $"[PlayerLoadoutFactory] Weapon prefab is missing on " +
                $"{weaponData.name}.");
            return null;
        }

        Transform weaponPoint = string.IsNullOrWhiteSpace(weaponPointName)
            ? null
            : player.transform.Find(weaponPointName);
        weaponPoint ??= player.transform;

        GameObject weapon = Object.Instantiate(
            weaponData.weaponPrefab,
            weaponPoint.position,
            weaponPoint.rotation,
            player.transform);

        BaseWeapon baseWeapon = weapon.GetComponent<BaseWeapon>();
        if (baseWeapon == null)
        {
            Debug.LogWarning(
                "[PlayerLoadoutFactory] Spawned weapon has no BaseWeapon " +
                "component.",
                weapon);
            Object.Destroy(weapon);
            return null;
        }

        baseWeapon.Initialize(weaponData);
        baseWeapon.SetControlModeOverride(GetWeaponControlMode(combatType));
        return baseWeapon;
    }

    private static WeaponControlMode GetWeaponControlMode(
        CharacterCombatType combatType)
    {
        return combatType switch
        {
            CharacterCombatType.AutoFire => WeaponControlMode.AutoAim,
            _ => WeaponControlMode.AutoAim
        };
    }
}
