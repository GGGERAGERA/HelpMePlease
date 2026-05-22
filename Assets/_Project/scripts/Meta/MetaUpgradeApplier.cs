using UnityEngine;

public class MetaUpgradeApplier : MonoBehaviour
{
    [Header("Bonuses Per Level")]
    [SerializeField] private float hpPerLevel = 5f;
    [SerializeField] private float damagePerLevel = 1f;
    [SerializeField] private float moveSpeedPerLevel = 0.15f;
    [SerializeField] private float attackSpeedPerLevel = 0.1f;
    [SerializeField] private float critDamagePerLevel = 0.25f;
    [SerializeField] private float critProbabilityPerLevel = 0.02f;
    [SerializeField] private float piercingPerLevel = 1f;
    [SerializeField] private float multishotPerLevel = 1f;
    [SerializeField] private float ricochetPerLevel = 1f;


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
        int attackSpeedLevel = MetaProgressionManager.Instance.AttackSpeedLevel;
        int critDamageLevel = MetaProgressionManager.Instance.CritDamageLevel;
        int critProbabilityLevel = MetaProgressionManager.Instance.CritProbabilityLevel;
        int piercingLevel = MetaProgressionManager.Instance.PiercingLevel;
        int multishotLevel = MetaProgressionManager.Instance.MultishotLevel;
        int ricochetLevel = MetaProgressionManager.Instance.RicochetLevel;



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
                if (weapon == null)
                    continue;

                weapon.AddRuntimeDamage(damageLevel * damagePerLevel);
                weapon.AddFireRatePercent(attackSpeedLevel * attackSpeedPerLevel);
                weapon.AddCritMultiplier(critDamageLevel * critDamagePerLevel);
                weapon.AddCritChance(critProbabilityLevel * critProbabilityPerLevel);
                weapon.AddPierce(Mathf.RoundToInt(piercingLevel * piercingPerLevel));
                weapon.AddProjectileCount(Mathf.RoundToInt(multishotLevel * multishotPerLevel));
                weapon.AddRicochet(Mathf.RoundToInt(ricochetLevel * ricochetPerLevel));
            }
        }

        Debug.Log(
            $"Meta applied: HP +{hpLevel * hpPerLevel}, " +
            $"Damage +{damageLevel * damagePerLevel}, " +
            $"MoveSpeed +{moveSpeedLevel * moveSpeedPerLevel}" +
            $"AttackSpeed +{attackSpeedLevel * attackSpeedPerLevel}, " +
            $"CritDamage +{critDamageLevel * critDamagePerLevel}, " +
            $"CritChance +{critProbabilityLevel * critProbabilityPerLevel}, " +
            $"Piercing +{piercingLevel * piercingPerLevel}, " +
            $"Multishot +{multishotLevel * multishotPerLevel}, " +
            $"Ricochet +{ricochetLevel * ricochetPerLevel}"


        );
    }
}