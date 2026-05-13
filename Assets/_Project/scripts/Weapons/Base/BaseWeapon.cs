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
    }

    protected virtual void Update()
    {
        if (Time.timeScale == 0f)
            return;
    }

    protected virtual bool CanAttack()
    {
        float fireRate = weaponData != null ? weaponData.fireRate : 0.5f;
        return Time.time >= lastAttackTime + fireRate;
    }

    public abstract void Attack();

    protected void MarkAttackTime()
    {
        lastAttackTime = Time.time;
    }

    public int GetDamage()
    {
        float weaponDamage = weaponData != null ? weaponData.damage : 10f;
        float playerDamage = PlayerStats.Instance != null ? PlayerStats.Instance.GetDamage() : 0f;

        return Mathf.RoundToInt(weaponDamage + playerDamage + runtimeDamageBonus);
    }

    public float GetRange()
    {
        float weaponRange = weaponData != null ? weaponData.range : 5f;
        return weaponRange + runtimeRangeBonus;
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

}