using UnityEngine;

public class MetaUpgradeApplier : MonoBehaviour
{
    [Header("Bonuses Per Level")]
    [SerializeField] private float hpPerLevel = 5f;
    [SerializeField] private float damagePerLevel = 1f;
    [SerializeField] private float moveSpeedPerLevel = 0.15f;

    public void ApplyTo(GameObject player, BaseWeapon[] weapons)
    {
        if (player == null)
        {
            Debug.LogWarning("MetaUpgradeApplier: player is null.");
            return;
        }

        if (MetaProgressionManager.Instance == null)
        {
            Debug.LogWarning("MetaUpgradeApplier: MetaProgressionManager not found.");
            return;
        }

        int hpLevel = MetaProgressionManager.Instance.HpLevel;
        int damageLevel = MetaProgressionManager.Instance.DamageLevel;
        int moveSpeedLevel = MetaProgressionManager.Instance.MoveSpeedLevel;

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        CharacterMovement2D movement = player.GetComponent<CharacterMovement2D>();

        if (health != null)
            health.AddMaxHealth(hpLevel * hpPerLevel);

        if (movement != null)
            movement.AddMoveSpeed(moveSpeedLevel * moveSpeedPerLevel);

        if (weapons != null)
        {
            foreach (BaseWeapon weapon in weapons)
            {
                if (weapon != null)
                    weapon.AddRuntimeDamage(damageLevel * damagePerLevel);
            }
        }

        Debug.Log(
            $"Meta applied: HP +{hpLevel * hpPerLevel}, " +
            $"Damage +{damageLevel * damagePerLevel}, " +
            $"MoveSpeed +{moveSpeedLevel * moveSpeedPerLevel}"
        );
    }
}