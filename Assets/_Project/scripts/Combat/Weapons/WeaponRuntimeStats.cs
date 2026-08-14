using UnityEngine;

/// <summary>
/// Runtime mutable weapon stats for one spawned weapon instance.
/// WeaponData stays immutable/base config. Upgrades modify this component.
/// </summary>
public sealed class WeaponRuntimeStats : MonoBehaviour
{
    [Header("Base Stats")]
    [SerializeField] private int baseDamage = 10;
    [SerializeField] private float baseShotsPerSecond = 2f;
    [SerializeField] private float baseRange = 10f;
    [SerializeField] private float baseProjectileSpeed = 10f;
    [SerializeField] private int baseProjectileCount = 1;
    [SerializeField] private int basePierce = 0;
    [SerializeField] private int baseRicochet = 0;

    [Header("Runtime Multipliers")]
    [SerializeField] private float damageMultiplier = 1f;
    [SerializeField] private float fireRateMultiplier = 1f;
    [SerializeField] private float knockbackMultiplier = 1f;

    [Header("Weapon Tempo Profile")]
    [SerializeField] private float tempoDamageMultiplier = 1f;
    [SerializeField] private float tempoFireRateMultiplier = 1f;
    [SerializeField] private float shotVisualScale = 1f;

    [Header("Runtime Additive")]
    [SerializeField] private float flatDamageBonus = 0f;
    [SerializeField] private float rangeBonus = 0f;
    [SerializeField] private int projectileCountBonus = 0;
    [SerializeField] private int pierceBonus = 0;
    [SerializeField] private int ricochetBonus = 0;

    [Header("Critical")]
    [Range(0f, 1f)]
    [SerializeField] private float critChance = 0.05f;
    [SerializeField] private float critMultiplier = 2f;

    [Header("Debug")]
    [SerializeField] private float debugFinalShotsPerSecond;
    [SerializeField] private float debugFinalCooldown;
    [SerializeField] private int debugFinalDamage;

    public int ProjectileCount => Mathf.Max(1, baseProjectileCount + projectileCountBonus);
    public int Pierce => Mathf.Max(0, basePierce + pierceBonus);
    public int Ricochet => Mathf.Max(0, baseRicochet + ricochetBonus);
    public float Range => Mathf.Max(0.1f, baseRange + rangeBonus);
    public float ProjectileSpeed => Mathf.Max(0.1f, baseProjectileSpeed);
    public float CritChance => critChance;
    public float CritMultiplier => critMultiplier;
    public float KnockbackMultiplier => knockbackMultiplier;
    public float ShotVisualScale => Mathf.Max(0.1f, shotVisualScale);

    public void InitializeFromWeaponData(WeaponData data)
    {
        if (data == null)
            return;

        baseDamage = Mathf.Max(0, data.damage);
        baseShotsPerSecond = Mathf.Max(0.01f, data.fireRate);
        baseRange = Mathf.Max(0.1f, data.range);
        baseProjectileSpeed = Mathf.Max(0.1f, data.projectileSpeed);
        baseProjectileCount = Mathf.Max(1, data.bulletsPerShot);
        basePierce = Mathf.Max(0, data.pierce);
        baseRicochet = 0;

        damageMultiplier = 1f;
        fireRateMultiplier = 1f;
        knockbackMultiplier = 1f;
        tempoDamageMultiplier = 1f;
        tempoFireRateMultiplier = 1f;
        shotVisualScale = 1f;
        flatDamageBonus = 0f;
        rangeBonus = 0f;
        projectileCountBonus = 0;
        pierceBonus = 0;
        ricochetBonus = 0;
        critChance = 0.05f;
        critMultiplier = 2f;

        RefreshDebug(null);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void CopyUpgradeModifiersFrom(WeaponRuntimeStats source)
    {
        if (source == null)
            return;

        damageMultiplier = source.damageMultiplier;
        fireRateMultiplier = source.fireRateMultiplier;
        knockbackMultiplier = source.knockbackMultiplier;
        tempoDamageMultiplier = source.tempoDamageMultiplier;
        tempoFireRateMultiplier = source.tempoFireRateMultiplier;
        shotVisualScale = source.shotVisualScale;
        flatDamageBonus = source.flatDamageBonus;
        rangeBonus = source.rangeBonus;
        projectileCountBonus = source.projectileCountBonus;
        pierceBonus = source.pierceBonus;
        ricochetBonus = source.ricochetBonus;
        critChance = source.critChance;
        critMultiplier = source.critMultiplier;
        RefreshDebug(null);
    }

    public void CopyFrom(WeaponRuntimeStats source)
    {
        if (source == null)
            return;

        baseDamage = source.baseDamage;
        baseShotsPerSecond = source.baseShotsPerSecond;
        baseRange = source.baseRange;
        baseProjectileSpeed = source.baseProjectileSpeed;
        baseProjectileCount = source.baseProjectileCount;
        basePierce = source.basePierce;
        baseRicochet = source.baseRicochet;
        damageMultiplier = source.damageMultiplier;
        fireRateMultiplier = source.fireRateMultiplier;
        knockbackMultiplier = source.knockbackMultiplier;
        tempoDamageMultiplier = source.tempoDamageMultiplier;
        tempoFireRateMultiplier = source.tempoFireRateMultiplier;
        shotVisualScale = source.shotVisualScale;
        flatDamageBonus = source.flatDamageBonus;
        rangeBonus = source.rangeBonus;
        projectileCountBonus = source.projectileCountBonus;
        pierceBonus = source.pierceBonus;
        ricochetBonus = source.ricochetBonus;
        critChance = source.critChance;
        critMultiplier = source.critMultiplier;
        RefreshDebug(null);
    }
#endif

    public int GetDamage(PlayerCombatModifiers modifiers)
    {
        float finalDamage = (baseDamage + flatDamageBonus) *
            damageMultiplier * tempoDamageMultiplier;

        if (modifiers != null)
        {
            finalDamage *= modifiers.bonusDamageMultiplier;
        }

        int rounded = Mathf.Max(0, Mathf.RoundToInt(finalDamage));
        debugFinalDamage = rounded;
        return rounded;
    }

    public float GetShotsPerSecond(PlayerCombatModifiers modifiers)
    {
        float shotsPerSecond = baseShotsPerSecond *
            fireRateMultiplier * tempoFireRateMultiplier;

        if (modifiers != null)
        {
            shotsPerSecond *= Mathf.Max(0.1f, modifiers.bonusFireRateMultiplier);
            shotsPerSecond *= Mathf.Max(0.1f, 1f + modifiers.stationaryFireRateBonus);
        }

        shotsPerSecond = Mathf.Max(0.01f, shotsPerSecond);
        debugFinalShotsPerSecond = shotsPerSecond;
        debugFinalCooldown = 1f / shotsPerSecond;
        return shotsPerSecond;
    }

    public void AddFlatDamage(float amount) => flatDamageBonus += amount;
    public void AddRange(float amount) => rangeBonus += amount;
    public void AddDamagePercent(float percent) => damageMultiplier *= 1f + percent;
    public void AddFireRatePercent(float percent) => fireRateMultiplier *= 1f + percent;
    public void AddKnockbackPercent(float percent) => knockbackMultiplier *= 1f + percent;
    public void AddCritChance(float amount) => critChance = Mathf.Clamp01(critChance + amount);
    public void AddCritMultiplier(float amount) => critMultiplier += amount;
    public void AddProjectileCount(int amount) => projectileCountBonus += amount;
    public void AddPierce(int amount) => pierceBonus += amount;
    public void AddRicochet(int amount) => ricochetBonus += amount;
    public void SetPierceBonus(int amount) => pierceBonus = Mathf.Max(0, amount);
    public void SetRicochetBonus(int amount) => ricochetBonus = Mathf.Max(0, amount);

    public void SetTempoProfile(
        float damage,
        float fireRate,
        float visualScale)
    {
        tempoDamageMultiplier = Mathf.Max(0.01f, damage);
        tempoFireRateMultiplier = Mathf.Max(0.01f, fireRate);
        shotVisualScale = Mathf.Max(0.1f, visualScale);
    }

    public void RefreshDebug(PlayerCombatModifiers modifiers)
    {
        GetDamage(modifiers);
        GetShotsPerSecond(modifiers);
    }
}
