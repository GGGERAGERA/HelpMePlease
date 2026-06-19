using UnityEngine;

public abstract class BaseWeapon : MonoBehaviour
{
    [Header("Base Weapon Settings")]
    public WeaponData weaponData;
    public Transform firePoint;

    protected float lastAttackTime;

    private float runtimeDamageBonus = 0f;
    private float runtimeRangeBonus = 0f;
    private float fireRateMultiplier = 1f;
    private float damageMultiplier = 1f;
    private float knockbackMultiplier = 1f;

    [SerializeField] protected AudioSource weaponAudioSource;

    [SerializeField] protected int projectileCount = 1;
    [SerializeField] protected int projectilePierce = 0;
    [SerializeField] protected int projectileRicochet = 0;

    [Range(0f, 1f)]
    [SerializeField] protected float critChance = 0.05f;

    [SerializeField] protected float critMultiplier = 2f;



    private bool weaponDataApplied;

    protected virtual void Start()
    {
        SetupAudio();

        if (firePoint == null)
            firePoint = transform;

        ApplyWeaponDataStatsOnce();
    }

    protected virtual void Update()
    {
        if (Time.timeScale == 0f)
            return;
    }

    public void Initialize(WeaponData data)
    {
        weaponData = data;
        ApplyWeaponDataStatsOnce();
    }

    private void SetupAudio()
    {
        if (weaponAudioSource == null)
            weaponAudioSource = GetComponent<AudioSource>();

        if (weaponAudioSource == null)
            weaponAudioSource = gameObject.AddComponent<AudioSource>();

        weaponAudioSource.playOnAwake = false;
        weaponAudioSource.loop = false;
        weaponAudioSource.spatialBlend = 0f;
    }

    private void ApplyWeaponDataStatsOnce()
    {
        if (weaponDataApplied)
            return;

        if (weaponData == null)
            return;

        projectileCount = Mathf.Max(1, weaponData.bulletsPerShot);
        projectilePierce = Mathf.Max(0, weaponData.pierce);

        weaponDataApplied = true;
    }

    protected virtual bool CanAttack()
    {
        return Time.time >= lastAttackTime + GetAttackCooldown();
    }

    public abstract void Attack();

    protected void MarkAttackTime()
    {
        lastAttackTime = Time.time;
    }

    public int GetDamage()
    {
        float weaponDamage = weaponData != null ? weaponData.damage : 10f;
        float finalDamage = (weaponDamage + runtimeDamageBonus) * damageMultiplier;

        return Mathf.RoundToInt(finalDamage);
    }

    public float GetRange()
    {
        float weaponRange = weaponData != null ? weaponData.range : 5f;
        return weaponRange + runtimeRangeBonus;
    }

    public float GetProjectileSpeed()
    {
        return weaponData != null ? weaponData.projectileSpeed : 10f;
    }


    public void AddRuntimeDamage(float amount)
    {
        runtimeDamageBonus += amount;
    }

    public void AddRuntimeRange(float amount)
    {
        runtimeRangeBonus += amount;
    }

    public void AddFireRatePercent(float percent)
    {
        fireRateMultiplier *= 1f + percent;
    }

    public void AddCritChance(float amount)
    {
        critChance = Mathf.Clamp01(critChance + amount);
    }

    public void AddCritMultiplier(float amount)
    {
        critMultiplier += amount;
    }

    public void AddProjectileCount(int amount)
    {
        projectileCount = Mathf.Max(1, projectileCount + amount);
        Debug.Log($"{name}: Projectile count = {projectileCount}");
    }

    public void AddPierce(int amount)
    {
        projectilePierce = Mathf.Max(0, projectilePierce + amount);
        Debug.Log($"{name}: Pierce = {projectilePierce}");
    }

    public void AddRicochet(int amount)
    {
        projectileRicochet = Mathf.Max(0, projectileRicochet + amount);
        Debug.Log($"{name}: Ricochet = {projectileRicochet}");
    }

    public bool RollCritical()
    {
        return Random.value < critChance;
    }

    public float GetCritMultiplier()
    {
        return critMultiplier;
    }

    protected void PlaySound(AudioClip clip)
    {
        if (clip == null || weaponAudioSource == null || weaponData == null)
            return;

        weaponAudioSource.pitch = Random.Range(
            weaponData.pitchRange.x,
            weaponData.pitchRange.y
        );

        weaponAudioSource.PlayOneShot(
            clip,
            weaponData.soundVolume
        );
    }

    public void AddDamagePercent(float percent)
    {
        damageMultiplier *= 1f + percent;
    }

    public void AddKnockbackPercent(float percent)
    {
        knockbackMultiplier *= 1f + percent;
    }

    public float GetKnockbackMultiplier()
    {
        return knockbackMultiplier;
    }
    public float GetKnockbackForce(float baseForce)
    {
        return baseForce * knockbackMultiplier;
    }
    public float GetAttackCooldown()
    {
        float baseCooldown = weaponData != null ? weaponData.fireRate : 0.5f;
        baseCooldown = Mathf.Max(0.05f, baseCooldown);

        return baseCooldown / fireRateMultiplier;
    }
}