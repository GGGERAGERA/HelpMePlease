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

    [Header("Aim / Orbit")]
    [SerializeField] private WeaponControlMode controlMode = WeaponControlMode.Automatic;
    [SerializeField] private WeaponTargeting targeting;
    [SerializeField] protected bool rotateToMouse = true;
    [SerializeField] protected bool moveAroundOwner = true;
    [SerializeField] protected Transform owner;
    [SerializeField] protected float orbitRadius = 0.5f;
    [SerializeField] protected float orbitSmoothSpeed = 25f;

    private Vector2 lastAimDirection = Vector2.right;
    private Vector2 lastOwnerPosition;
    private Transform ownerTransform;

    protected float lastAttackTime;

    private bool isInitialized;

    protected WeaponRuntimeStats Stats => runtimeStats;
    protected WeaponFxPlayer FxPlayer => fxPlayer;
    public Transform Owner => owner;

    protected virtual void Awake()
    {
        EnsureRuntimeStats();
        if (fxPlayer == null)
            fxPlayer = GetComponent<WeaponFxPlayer>();

        if (targeting == null)
            targeting = GetComponent<WeaponTargeting>();

        if (controlMode == WeaponControlMode.Automatic && targeting == null)
        {
            Debug.LogWarning(
                $"[BaseWeapon] Automatic control requires WeaponTargeting on {name}.",
                this
            );
        }
    }

    protected virtual void Start()
    {
        SetupAudio();

        ownerTransform = transform.parent;
        if (ownerTransform != null)
            lastOwnerPosition = ownerTransform.position;

        if (firePoint == null)
            firePoint = transform;

        if (owner == null && transform.parent != null)
            owner = transform.parent;

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

        UpdateAimAndOrbit();
        UpdateStationaryFireRateRamp();

        if (IsTryingToAttack() && CanAttack())
        {
            if (Attack())
                MarkAttackTime();
        }
    }

    public void Initialize(WeaponData data)
    {
        weaponData = data;
        EnsureRuntimeStats();
        runtimeStats.InitializeFromWeaponData(data);
        isInitialized = true;
    }

    protected virtual void UpdateAimAndOrbit()
    {
        if (owner == null)
            return;

        Vector2 aimDirection;

        if (TryGetAimDirectionFromOwner(out Vector2 currentDirection))
        {
            lastAimDirection = currentDirection;
            aimDirection = currentDirection;
        }
        else
        {
            aimDirection = lastAimDirection;
        }

        if (moveAroundOwner)
            MoveAroundOwner(aimDirection);

        if (rotateToMouse)
            RotateWeapon(aimDirection);
    }

    protected bool TryGetAimDirectionFromOwner(out Vector2 direction)
    {
        direction = Vector2.zero;

        if (owner == null)
            return false;

        if (!TryGetAimTargetPosition(out Vector2 targetPosition))
            return false;

        return TryNormalizeDirection(
            targetPosition - (Vector2)owner.position,
            out direction
        );
    }

    protected bool TryGetAimDirectionFromFirePoint(out Vector2 direction)
    {
        if (firePoint == null)
            firePoint = transform;

        direction = Vector2.zero;

        if (!TryGetAimTargetPosition(out Vector2 targetPosition))
            return false;

        return TryNormalizeDirection(
            targetPosition - (Vector2)firePoint.position,
            out direction
        );
    }

    private bool TryGetAimTargetPosition(out Vector2 targetPosition)
    {
        if (controlMode == WeaponControlMode.Manual)
        {
            targetPosition = GetMouseWorldPosition();
            return IsValidVector(targetPosition);
        }

        if (targeting != null && targeting.TryGetTarget(out Transform target))
        {
            targetPosition = target.position;
            return true;
        }

        targetPosition = Vector2.zero;
        return false;
    }

    private bool TryNormalizeDirection(Vector2 value, out Vector2 direction)
    {
        direction = Vector2.zero;

        if (!IsValidVector(value) || value.sqrMagnitude < 0.001f)
            return false;

        direction = value.normalized;
        return true;
    }

    protected Vector2 GetMouseWorldPosition()
    {
        Camera camera = Camera.main;

        if (camera == null)
            return transform.position;

        Vector3 mousePosition = camera.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0f;

        return mousePosition;
    }

    protected void MoveAroundOwner(Vector2 aimDirection)
    {
        Vector2 targetPosition = (Vector2)owner.position + aimDirection * orbitRadius;

        transform.position = Vector2.Lerp(
            transform.position,
            targetPosition,
            orbitSmoothSpeed * Time.deltaTime
        );
    }

    protected void RotateWeapon(Vector2 aimDirection)
    {
        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
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

    public abstract bool Attack();

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
        if (controlMode == WeaponControlMode.Manual)
            return Input.GetMouseButton(0);

        return targeting != null && targeting.HasTarget;
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
    protected WeaponFireContext BuildFireContext(Vector2 origin, Vector2 direction)
    {
        PlayerCombatModifiers modifiers = GetCombatModifiers();

        bool isCritical = RollCritical();
        int damage = GetDamage();

        if (isCritical)
            damage = Mathf.RoundToInt(damage * GetCritMultiplier());

        return new WeaponFireContext(
            this,
            transform.parent,
            firePoint,
            origin,
            direction,
            damage,
            isCritical,
            GetRange(),
            GetProjectileSpeed(),
            GetProjectileCount(),
            GetProjectilePierce(),
            GetProjectileRicochet(),
            0f,
            modifiers,
            FxPlayer
        );
    }

    protected Vector2 ApplyAccuracyPenalty(Vector2 direction)
    {
        PlayerCombatModifiers modifiers = GetCombatModifiers();

        if (modifiers == null || modifiers.accuracyPenaltyDegrees <= 0f)
            return direction;

        float randomAngle = Random.Range(
            -modifiers.accuracyPenaltyDegrees,
            modifiers.accuracyPenaltyDegrees
        );

        return RotateVector(direction, randomAngle);
    }

    protected Vector2 RotateVector(Vector2 vector, float angle)
    {
        float radians = angle * Mathf.Deg2Rad;

        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        ).normalized;
    }

    protected bool IsValidVector(Vector2 value)
    {
        return
            !float.IsNaN(value.x) &&
            !float.IsNaN(value.y) &&
            !float.IsInfinity(value.x) &&
            !float.IsInfinity(value.y);
    }
}
