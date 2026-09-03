using UnityEngine;

/// <summary>
/// Builds the character/weapon parts shared by scene-specific player spawners.
/// It owns no state and never persists scene objects.
/// </summary>
public static class PlayerLoadoutFactory
{
    private const float MetaMoveSpeedPercentPerLevel = 0.03f;

    public static void ApplyCharacterStats(
        GameObject player,
        CharacterData characterData,
        float fallbackMoveSpeed = float.NaN)
    {
        if (player == null)
            return;

        MetaProgressionManager meta = MetaProgressionManager.EnsureExists();
        meta.ReloadFromStorage();

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null && characterData != null)
        {
            health.maxHealth = characterData.maxHealth;
            health.currentHealth = characterData.maxHealth;
        }

        CharacterMovement2D movement =
            player.GetComponent<CharacterMovement2D>();
        if (movement != null)
        {
            float baseMoveSpeed = characterData != null
                ? characterData.moveSpeed
                : float.IsNaN(fallbackMoveSpeed)
                    ? movement.AuthoredMoveSpeed
                    : fallbackMoveSpeed;
            movement.ApplyCalculatedMoveSpeed(CalculateFinalMoveSpeed(baseMoveSpeed, meta));
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal static void ApplyDebugMoveSpeed(CharacterMovement2D movement, float value)
    {
        if (movement != null)
            movement.ApplyCalculatedMoveSpeed(
                Mathf.Max(0f, value) / movement.RunUpgradeMoveSpeedMultiplier);
    }
#endif

    public static float CalculateFinalMoveSpeed(
        CharacterData characterData,
        float fallbackMoveSpeed = 0f)
    {
        MetaProgressionManager meta = MetaProgressionManager.EnsureExists();
        meta.ReloadFromStorage();
        float baseMoveSpeed = characterData != null
            ? characterData.moveSpeed
            : fallbackMoveSpeed;
        return CalculateFinalMoveSpeed(baseMoveSpeed, meta);
    }

    private static float CalculateFinalMoveSpeed(
        float baseMoveSpeed,
        MetaProgressionManager meta)
    {
        float metaMultiplier = 1f +
            meta.MoveSpeedLevel * MetaMoveSpeedPercentPerLevel;
        return Mathf.Max(0f, baseMoveSpeed) * metaMultiplier;
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
