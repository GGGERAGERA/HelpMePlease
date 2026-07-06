using UnityEngine;

public abstract class BaseWeapon : MonoBehaviour
{
    [Header("Base Weapon Settings")]
    public WeaponData weaponData;
    public Transform firePoint;

    [Header("Runtime Stats")]
    [SerializeField] private WeaponRuntimeStats runtimeStats;

    [Header("Audio")]
    [SerializeField] protected AudioSource weaponAudioSource;

    [Header("FX")]
    [SerializeField] private WeaponFxPlayer fxPlayer;

    private Vector2 lastOwnerPosition;
    private Transform ownerTransform;

    protected float lastAttackTime;

    private bool isInitialized;

    protected WeaponRuntimeStats Stats => runtimeStats;
    protected WeaponFxPlayer FxPlayer => fxPlayer;

    protected virtual void Awake()
    {
        EnsureRuntimeStats();
        if (fxPlayer == null)
            fxPlayer = GetComponent<WeaponFxPlayer>();
    }

    protected virtual void Start()
    {
        SetupAudio();

        ownerTransform = transform.parent;
        if (ownerTransform != null)
            lastOwnerPosition = ownerTransform.position;

        if (firePoint == null)
            firePoint = transform;

        if (!isInitialized && weaponData != null)
        {
            runtimeStats.InitializeFromWeaponData(weaponData);
            isInitialized = true;
        }
    }

    protected virtual void Update()
    {
        if (Time.timeScale == 0f)
            return;

        UpdateStationaryFireRateRamp();
    }

    public void Initialize(WeaponData data)
    {
        weaponData = data;
        EnsureRuntimeStats();
        runtimeStats.InitializeFromWeaponData(data);
        isInitialized = true;
    }

    private void EnsureRuntimeStats()
    {
        if (runtimeStats != null)
            return;

        runtimeStats = GetComponent<WeaponRuntimeStats>();

        if (runtimeStats == null)
            runtimeStats = gameObject.AddComponent<WeaponRuntimeStats>();
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
        return runtimeStats.GetDamage(GetCombatModifiers());
    }

    public float GetRange()
    {
        return runtimeStats.Range;
    }

    public float GetProjectileSpeed()
    {
        return runtimeStats.ProjectileSpeed;
    }

    public float GetAttackCooldown()
    {
        float shotsPerSecond = runtimeStats.GetShotsPerSecond(GetCombatModifiers());
        return 1f / Mathf.Max(0.01f, shotsPerSecond);
    }

    protected int GetProjectileCount()
    {
        return runtimeStats.ProjectileCount;
    }

    protected int GetProjectilePierce()
    {
        return runtimeStats.Pierce;
    }

    protected int GetProjectileRicochet()
    {
        return runtimeStats.Ricochet;
    }

    protected PlayerCombatModifiers GetCombatModifiers()
    {
        return GetComponentInParent<PlayerCombatModifiers>();
    }

    public void AddRuntimeDamage(float amount)
    {
        runtimeStats.AddFlatDamage(amount);
    }

    public void AddRuntimeRange(float amount)
    {
        runtimeStats.AddRange(amount);
    }

    public void AddFireRatePercent(float percent)
    {
        runtimeStats.AddFireRatePercent(percent);
        runtimeStats.RefreshDebug(GetCombatModifiers());
    }

    public void AddCritChance(float amount)
    {
        runtimeStats.AddCritChance(amount);
    }

    public void AddCritMultiplier(float amount)
    {
        runtimeStats.AddCritMultiplier(amount);
    }

    public void AddProjectileCount(int amount)
    {
        runtimeStats.AddProjectileCount(amount);
    }

    public void AddPierce(int amount)
    {
        runtimeStats.AddPierce(amount);
    }

    public void AddRicochet(int amount)
    {
        runtimeStats.AddRicochet(amount);
    }

    public bool RollCritical()
    {
        return Random.value < runtimeStats.CritChance;
    }

    public float GetCritMultiplier()
    {
        return runtimeStats.CritMultiplier;
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
        runtimeStats.AddDamagePercent(percent);
    }

    public void AddKnockbackPercent(float percent)
    {
        runtimeStats.AddKnockbackPercent(percent);
    }

    public float GetKnockbackMultiplier()
    {
        return runtimeStats.KnockbackMultiplier;
    }

    public float GetKnockbackForce(float baseForce)
    {
        return baseForce * runtimeStats.KnockbackMultiplier;
    }
    private void UpdateStationaryFireRateRamp()
    {
        PlayerCombatModifiers modifiers = GetComponentInParent<PlayerCombatModifiers>();

        if (modifiers == null)
            return;

        bool isAttacking = IsTryingToAttack();
        bool isMoving = IsOwnerMoving(modifiers.stationaryMoveThreshold);

        modifiers.UpdateStationaryFireRateRamp(
            isAttacking,
            isMoving,
            Time.deltaTime
        );
    }

    protected virtual bool IsTryingToAttack()
    {
        return Input.GetMouseButton(0);
    }

    private bool IsOwnerMoving(float threshold)
    {
        if (ownerTransform == null)
            return false;

        Vector2 currentPosition = ownerTransform.position;
        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);

        float speed = Vector2.Distance(currentPosition, lastOwnerPosition) / deltaTime;
        lastOwnerPosition = currentPosition;

        return speed > threshold;
    }
}
