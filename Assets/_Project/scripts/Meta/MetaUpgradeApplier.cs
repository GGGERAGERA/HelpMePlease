using UnityEngine;

public class MetaUpgradeApplier : MonoBehaviour
{
    [Header("Bonuses Per Level")]
    [SerializeField] private float hpPerLevel = 1f;
    [SerializeField] private float damagePercentPerLevel = 0.05f;
    [SerializeField] private float moveSpeedPercentPerLevel = 0.03f;
    [SerializeField] private float xpGainPercentPerLevel = 0.05f;
    [SerializeField] private float goldGainPercentPerLevel = 0.10f;
    [SerializeField] private float pickupRadiusPercentPerLevel = 0.05f;

    public void ApplyTo(GameObject player, BaseWeapon[] weapons)
    {
        if (player == null)
        {
            Debug.LogWarning("MetaUpgradeApplier: player is null.");
            return;
        }

        if (MetaProgressionManager.Instance == null)
        {
            return;
        }

        MetaProgressionManager meta = MetaProgressionManager.Instance;

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        CharacterMovement2D movement = player.GetComponent<CharacterMovement2D>();

        if (health != null)
            health.AddMaxHealth(meta.HpLevel * hpPerLevel);

        if (movement != null)
            movement.AddMoveSpeedPercent(meta.MoveSpeedLevel * moveSpeedPercentPerLevel);

        if (weapons != null)
        {
            foreach (BaseWeapon weapon in weapons)
            {
                if (weapon == null)
                    continue;

                weapon.AddDamagePercent(meta.DamageLevel * damagePercentPerLevel);
            }
        }

        ApplyXpGain(meta.XpGainLevel * xpGainPercentPerLevel);
        ApplyGoldGain(meta.GoldGainLevel * goldGainPercentPerLevel);
        ApplyPickupRadius(player, meta.PickupRadiusLevel * pickupRadiusPercentPerLevel
);
    }

    private void ApplyXpGain(float percent)
    {
        if (ExperienceManager.Instance != null)
            ExperienceManager.Instance.AddXpGainPercent(percent);
    }

    private void ApplyGoldGain(float percent)
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.AddGoldGainPercent(percent);
    }
    private void ApplyPickupRadius(GameObject player, float percent)
    {
        PlayerPickupRadius pickupRadius =
            player.GetComponent<PlayerPickupRadius>();

        if (pickupRadius != null)
        {
            pickupRadius.AddRadiusPercent(percent);
        }
    }

}