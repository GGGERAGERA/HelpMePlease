using UnityEngine;

/// <summary>
/// Applies character statistics shared by scene-specific player spawners.
/// It owns no state and never persists scene objects.
/// </summary>
public static class PlayerLoadoutFactory
{
    private const float MetaMoveSpeedPercentPerLevel = 0.03f;

    public static void ApplyCharacterStats(
        GameObject player,
        CharacterData characterData,
        float fallbackMoveSpeed = float.NaN,
        float? moveSpeedOverride = null)
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
            float baseMoveSpeed = moveSpeedOverride ?? (characterData != null
                ? characterData.moveSpeed
                : float.IsNaN(fallbackMoveSpeed)
                    ? movement.AuthoredMoveSpeed
                    : fallbackMoveSpeed);
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
}
