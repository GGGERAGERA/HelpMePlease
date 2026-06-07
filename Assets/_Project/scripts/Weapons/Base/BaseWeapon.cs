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


    [SerializeField] protected AudioSource weaponAudioSource;
    [SerializeField] protected int projectileCount = 1;
    [SerializeField] protected int projectilePierce = 0;
    [SerializeField] protected int projectileRicochet = 0;
    [Range(0f, 1f)]
    [SerializeField] protected float critChance = 0.05f;
    [SerializeField] protected float critMultiplier = 2f;

    protected virtual void Start()
    {
        
        if (weaponAudioSource == null)
            weaponAudioSource = GetComponent<AudioSource>();

        if (weaponAudioSource == null)
            weaponAudioSource = gameObject.AddComponent<AudioSource>();

        weaponAudioSource.playOnAwake = false;
        weaponAudioSource.loop = false;
        weaponAudioSource.spatialBlend = 0f;

        if (firePoint == null)
            firePoint = transform;

        ApplyWeaponDataStats();
    }

    protected virtual void Update()
    {
        if (Time.timeScale == 0f)
            return;
    }

    protected virtual bool CanAttack()
    {
        return Time.time >= lastAttackTime + GetFireRate();
    }

    public abstract void Attack();

    protected void MarkAttackTime()
    {
        lastAttackTime = Time.time;
    }

    public int GetDamage()
    {
        float weaponDamage = weaponData != null ? weaponData.damage : 10f;
        return Mathf.RoundToInt(weaponDamage + runtimeDamageBonus);
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
    public float GetFireRate()
    {
        float baseFireRate = weaponData != null ? weaponData.fireRate : 1f;

        return Mathf.Max(0.05f, baseFireRate * fireRateMultiplier);
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
        fireRateMultiplier *= Mathf.Clamp(1f - percent, 0.1f, 10f);
    }
    public void AddCritChance(float amount)
    {
        critChance = Mathf.Clamp01(critChance + amount);
    }

    public void AddCritMultiplier(float amount)
    {
        critMultiplier += amount;
    }
    protected void PlaySound(AudioClip clip)
    {
        if (clip == null || weaponAudioSource == null)
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
    public void AddProjectileCount(int amount)
    {
        projectileCount += amount;
    }
    public void AddPierce(int amount)
    {
        projectilePierce += amount;
    }
    public bool RollCritical()
    {
        return Random.value < critChance;
    }

    public float GetCritMultiplier()
    {
        return critMultiplier;
    }
    public void AddRicochet(int amount)
    {
        projectileRicochet += amount;
    }
    private void ApplyWeaponDataStats()
    {
        if (weaponData == null)
            return;

        projectileCount = Mathf.Max(1, weaponData.bulletsPerShot);
        projectilePierce = Mathf.Max(0, weaponData.pierce);
    }


}