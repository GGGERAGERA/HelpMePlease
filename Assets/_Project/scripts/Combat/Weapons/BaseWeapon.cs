using UnityEngine;
using UnityEngine.EventSystems;

public enum WeaponShotKind
{
    Standard = 0,
    Rocket = 1,
    Laser = 2
}

public abstract class BaseWeapon : MonoBehaviour
{
    public static event System.Action<Vector2, WeaponShotKind> ShotFired;

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
    [SerializeField] private WeaponTargeting targeting;
    [SerializeField] protected bool rotateToMouse = true;
    [SerializeField] protected bool moveAroundOwner = true;
    [SerializeField] protected Transform owner;
    [SerializeField] protected float orbitRadius = 0.5f;
    [SerializeField] protected float orbitSmoothSpeed = 25f;

    [Header("Idle Orbit")]
    [SerializeField] private bool rotateAroundOwnerWithoutTarget = true;
    [SerializeField] private float idleOrbitDegreesPerSecond = 45f;
    [SerializeField] private float targetDirectionBlendSpeed = 12f;
    [SerializeField] private float initialIdleOrbitAngle;

    private Vector2 lastAimDirection = Vector2.right;
    private bool hasAim;
    private float idleOrbitAngle;
    private bool hadAutomaticTarget;
    private Vector2 lastOwnerPosition;
    private Transform ownerTransform;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool telekinesisDebugStateCaptured;
    private float telekinesisDebugBaseOrbitRadius;
    private bool telekinesisDebugManualPosition;
    private bool telekinesisDebugForceAutomaticControl;
    private bool telekinesisDebugForceManualControl;
    private bool telekinesisDebugExternalPosition;
    private bool telekinesisDebugSecondaryWeapon;
    private float telekinesisDebugRadius = 6f;
    private float telekinesisDebugFollowSpeed = 18f;
    private Vector2 telekinesisDebugPositionTarget;
#endif

    protected float lastAttackTime;

    private bool isInitialized;
    private WeaponControlMode? controlModeOverride;

    protected WeaponRuntimeStats Stats => runtimeStats;
    protected WeaponFxPlayer FxPlayer => fxPlayer;
    public Transform Owner => owner;
    public WeaponControlMode ControlMode =>
        controlModeOverride ?? WeaponControlSettings.CurrentMode;
    public Vector2 AimDirection => lastAimDirection;
    public bool HasAim => hasAim;
    public bool WantsToFire => IsTryingToAttack();
    public int RuntimePierce => runtimeStats != null ? runtimeStats.Pierce : 0;
    public int RuntimeRicochet => runtimeStats != null ? runtimeStats.Ricochet : 0;
    public float RuntimeShotVisualScale => runtimeStats != null
        ? runtimeStats.ShotVisualScale
        : 1f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool IsTelekinesisDebugSecondary =>
        telekinesisDebugSecondaryWeapon;
#endif
    protected virtual WeaponShotKind ShotKind => WeaponShotKind.Standard;
    public virtual WeaponUpgradeCapability UpgradeCapabilities =>
        WeaponUpgradeCapability.None;

    protected virtual void Awake()
    {
        EnsureRuntimeStats();
        if (fxPlayer == null)
            fxPlayer = GetComponent<WeaponFxPlayer>();

        if (targeting == null)
            targeting = GetComponent<WeaponTargeting>();

        if (ControlMode == WeaponControlMode.AutoAim && targeting == null)
        {
            Debug.LogWarning(
                $"[BaseWeapon] Auto Aim requires WeaponTargeting on {name}.",
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

        idleOrbitAngle = Mathf.Repeat(initialIdleOrbitAngle, 360f);
        lastAimDirection = DirectionFromAngle(idleOrbitAngle);
        hasAim = true;

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

    public void SetControlModeOverride(WeaponControlMode mode)
    {
        controlModeOverride = mode;
    }

    protected virtual void UpdateAimAndOrbit()
    {
        if (owner == null)
            return;

        Vector2 aimDirection = UsesManualAimInput()
            ? GetManualAimDirection()
            : GetAutomaticAimDirection();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (telekinesisDebugManualPosition)
            UpdateTelekinesisDebugPosition();
        else if (telekinesisDebugExternalPosition)
            UpdateTelekinesisDebugExternalPosition();
        else
#endif
        if (moveAroundOwner)
            MoveAroundOwner(aimDirection);

        if (rotateToMouse)
            RotateWeapon(aimDirection);
    }

    private Vector2 GetManualAimDirection()
    {
        hadAutomaticTarget = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (telekinesisDebugExternalPosition)
        {
            if (firePoint == null)
                firePoint = transform;

            if (TryNormalizeDirection(
                    GetMouseWorldPosition() - (Vector2)firePoint.position,
                    out Vector2 remoteDirection))
            {
                hasAim = true;
                lastAimDirection = remoteDirection;
                return remoteDirection;
            }

            hasAim = false;
            return lastAimDirection;
        }
#endif

        if (TryGetAimDirectionFromOwner(out Vector2 currentDirection))
        {
            hasAim = true;
            lastAimDirection = currentDirection;
            return currentDirection;
        }

        hasAim = true;
        return lastAimDirection;
    }

    private Vector2 GetAutomaticAimDirection()
    {
        if (TryGetAimDirectionFromOwner(out Vector2 targetDirection))
        {
            hasAim = true;
            hadAutomaticTarget = true;
            lastAimDirection = BlendDirection(
                lastAimDirection,
                targetDirection,
                targetDirectionBlendSpeed,
                Time.deltaTime
            );
            return lastAimDirection;
        }

        if (hadAutomaticTarget)
        {
            idleOrbitAngle =
                Mathf.Atan2(lastAimDirection.y, lastAimDirection.x) *
                Mathf.Rad2Deg;
            hadAutomaticTarget = false;
        }

        hasAim = false;

        if (!rotateAroundOwnerWithoutTarget)
            return lastAimDirection;

        idleOrbitAngle = Mathf.Repeat(
            idleOrbitAngle + idleOrbitDegreesPerSecond * Time.deltaTime,
            360f
        );
        lastAimDirection = DirectionFromAngle(idleOrbitAngle);
        return lastAimDirection;
    }

    private Vector2 BlendDirection(
        Vector2 current,
        Vector2 target,
        float speed,
        float deltaTime
    )
    {
        if (speed <= 0f)
            return target;

        float currentAngle = Mathf.Atan2(current.y, current.x) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg;
        float blend = 1f - Mathf.Exp(-speed * deltaTime);
        float angle = Mathf.LerpAngle(currentAngle, targetAngle, blend);

        return DirectionFromAngle(angle);
    }

    private Vector2 DirectionFromAngle(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (telekinesisDebugForceManualControl)
        {
            if (firePoint == null)
                firePoint = transform;

            return TryNormalizeDirection(
                GetMouseWorldPosition() - (Vector2)firePoint.position,
                out direction
            );
        }
#endif

        if (UsesManualAimInput())
        {
            direction = lastAimDirection;
            return hasAim;
        }

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
        if (UsesManualAimInput())
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

    private bool UsesManualAimInput()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (telekinesisDebugForceManualControl)
            return true;

        if (telekinesisDebugForceAutomaticControl)
            return false;
#endif

        return ControlMode == WeaponControlMode.Manual;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void UpdateTelekinesisDebugPosition()
    {
        if (owner == null)
            return;

        Vector2 ownerPosition = owner.position;
        Vector2 offset = GetMouseWorldPosition() - ownerPosition;
        Vector2 clampedOffset = Vector2.ClampMagnitude(
            offset,
            telekinesisDebugRadius
        );
        Vector2 targetPosition = ownerPosition + clampedOffset;

        transform.position = Vector2.MoveTowards(
            transform.position,
            targetPosition,
            telekinesisDebugFollowSpeed * Time.deltaTime
        );
    }

    private void UpdateTelekinesisDebugExternalPosition()
    {
        transform.position = Vector2.MoveTowards(
            transform.position,
            telekinesisDebugPositionTarget,
            telekinesisDebugFollowSpeed * Time.deltaTime
        );
    }
#endif

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
        AudioService.Instance?.RouteExternalSource(
            weaponAudioSource,
            AudioCategory.SFX
        );
    }

    protected virtual bool CanAttack()
    {
        return Time.time >= lastAttackTime + GetAttackCooldown();
    }

    public abstract bool Attack();

    /// <summary>
    /// Emits one base attack with current runtime stats without changing the
    /// automatic-fire cooldown.
    /// </summary>
    public bool EmitAttack(Vector2 origin, Vector2 direction)
    {
        if (!IsValidVector(origin) || !IsValidVector(direction) ||
            direction.sqrMagnitude < 0.001f)
        {
            return false;
        }

        Vector2 finalDirection = ApplyAccuracyPenalty(direction.normalized);
        if (!EmitAttack(BuildFireContext(origin, finalDirection)))
            return false;

        ShotFired?.Invoke(origin, ShotKind);
        return true;
    }

    /// <summary>
    /// Emits an externally triggered attack in a supplied direction without
    /// changing the automatic-fire cooldown or applying aim inaccuracy.
    /// </summary>
    public virtual bool TryEmitExternalAttack(Vector2 direction)
    {
        if (!IsValidVector(direction) || direction.sqrMagnitude < 0.001f)
            return false;

        if (firePoint == null)
            firePoint = transform;

        return EmitAttack(BuildFireContext(
            firePoint.position,
            direction.normalized
        ));
    }

    protected abstract bool EmitAttack(WeaponFireContext context);

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

    public void SetPierceBonus(int amount)
    {
        runtimeStats.SetPierceBonus(amount);
    }

    public void SetRicochetBonus(int amount)
    {
        runtimeStats.SetRicochetBonus(amount);
    }

    public void SetTempoProfile(
        float damageMultiplier,
        float fireRateMultiplier,
        float visualScale)
    {
        runtimeStats.SetTempoProfile(
            damageMultiplier,
            fireRateMultiplier,
            visualScale);
        runtimeStats.RefreshDebug(GetCombatModifiers());
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (telekinesisDebugSecondaryWeapon)
            return;
#endif

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
        if (Time.timeScale <= 0f)
            return false;

        if (UsesManualAimInput())
        {
            if (!Input.GetMouseButton(0))
                return false;

            EventSystem eventSystem = EventSystem.current;
            return eventSystem == null ||
                !eventSystem.IsPointerOverGameObject();
        }

        return targeting != null && targeting.HasTarget;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void SetTelekinesisDebugBase()
    {
        CaptureTelekinesisDebugState();
        orbitRadius = telekinesisDebugBaseOrbitRadius;
        telekinesisDebugManualPosition = false;
        telekinesisDebugForceAutomaticControl = false;
        telekinesisDebugForceManualControl = false;
        telekinesisDebugExternalPosition = false;
        telekinesisDebugSecondaryWeapon = false;
        targeting?.ClearTelekinesisDebugOverrides();
    }

    public void SetTelekinesisDebugManual(
        float radius,
        float followSpeed,
        bool secondaryWeapon = false)
    {
        CaptureTelekinesisDebugState();
        orbitRadius = telekinesisDebugBaseOrbitRadius;
        telekinesisDebugRadius = Mathf.Max(0.1f, radius);
        telekinesisDebugFollowSpeed = Mathf.Max(0.1f, followSpeed);
        telekinesisDebugManualPosition = true;
        telekinesisDebugForceAutomaticControl = true;
        telekinesisDebugForceManualControl = false;
        telekinesisDebugExternalPosition = false;
        telekinesisDebugSecondaryWeapon = secondaryWeapon;
        targeting?.ClearTelekinesisDebugOverrides();
    }

    public void SetTelekinesisDebugManualFire(
        float radius,
        float followSpeed)
    {
        CaptureTelekinesisDebugState();
        orbitRadius = telekinesisDebugBaseOrbitRadius;
        telekinesisDebugRadius = Mathf.Max(0.1f, radius);
        telekinesisDebugFollowSpeed = Mathf.Max(0.1f, followSpeed);
        telekinesisDebugManualPosition = true;
        telekinesisDebugForceAutomaticControl = false;
        telekinesisDebugForceManualControl = true;
        telekinesisDebugExternalPosition = false;
        telekinesisDebugSecondaryWeapon = false;
        targeting?.ClearTelekinesisDebugOverrides();
    }

    public void SetTelekinesisDebugAutomatic(
        bool secondaryWeapon,
        bool useWeaponTargetingOrigin = false)
    {
        CaptureTelekinesisDebugState();
        orbitRadius = telekinesisDebugBaseOrbitRadius;
        telekinesisDebugManualPosition = false;
        telekinesisDebugForceAutomaticControl = true;
        telekinesisDebugForceManualControl = false;
        telekinesisDebugExternalPosition = false;
        telekinesisDebugSecondaryWeapon = secondaryWeapon;
        targeting?.ClearTelekinesisDebugOverrides();
        targeting?.SetTelekinesisDebugUseWeaponOrigin(
            useWeaponTargetingOrigin
        );
    }

    public void SetTelekinesisDebugExternalAutoPosition(
        Vector2 targetPosition,
        float followSpeed,
        bool secondaryWeapon,
        bool useWeaponTargetingOrigin)
    {
        CaptureTelekinesisDebugState();
        orbitRadius = telekinesisDebugBaseOrbitRadius;
        telekinesisDebugPositionTarget = targetPosition;
        telekinesisDebugFollowSpeed = Mathf.Max(0.1f, followSpeed);
        telekinesisDebugManualPosition = false;
        telekinesisDebugForceAutomaticControl = true;
        telekinesisDebugForceManualControl = false;
        telekinesisDebugExternalPosition = true;
        telekinesisDebugSecondaryWeapon = secondaryWeapon;
        targeting?.ClearTelekinesisDebugOverrides();
        targeting?.SetTelekinesisDebugUseWeaponOrigin(
            useWeaponTargetingOrigin
        );
    }

    public void SetTelekinesisDebugExternalPosition(
        Vector2 targetPosition,
        float followSpeed,
        bool useWeaponTargetingOrigin)
    {
        CaptureTelekinesisDebugState();
        orbitRadius = telekinesisDebugBaseOrbitRadius;
        telekinesisDebugPositionTarget = targetPosition;
        telekinesisDebugFollowSpeed = Mathf.Max(0.1f, followSpeed);
        telekinesisDebugManualPosition = false;
        telekinesisDebugForceAutomaticControl = false;
        telekinesisDebugForceManualControl = false;
        telekinesisDebugExternalPosition = true;
        telekinesisDebugSecondaryWeapon = false;
        targeting?.ClearTelekinesisDebugOverrides();
        targeting?.SetTelekinesisDebugUseWeaponOrigin(
            useWeaponTargetingOrigin
        );
    }

    public void UpdateTelekinesisDebugPositionTarget(Vector2 targetPosition)
    {
        telekinesisDebugPositionTarget = targetPosition;
    }

    public void SetTelekinesisDebugPriorityTarget(EnemyHealth target)
    {
        targeting?.SetTelekinesisDebugPriorityTarget(target);
    }

    public void CopyRuntimeStatsFrom(BaseWeapon source)
    {
        if (source == null)
            return;

        EnsureRuntimeStats();
        source.EnsureRuntimeStats();
        runtimeStats.CopyFrom(source.runtimeStats);
    }

    public void CopyRuntimeUpgradeModifiersFrom(BaseWeapon source)
    {
        if (source == null)
            return;

        EnsureRuntimeStats();
        source.EnsureRuntimeStats();
        runtimeStats.CopyUpgradeModifiersFrom(source.runtimeStats);
    }

    private void CaptureTelekinesisDebugState()
    {
        if (telekinesisDebugStateCaptured)
            return;

        telekinesisDebugBaseOrbitRadius = orbitRadius;
        telekinesisDebugStateCaptured = true;
    }
#endif

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
            weaponData,
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
            runtimeStats.ShotVisualScale,
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

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        idleOrbitDegreesPerSecond = Mathf.Max(0f, idleOrbitDegreesPerSecond);
        targetDirectionBlendSpeed = Mathf.Max(0f, targetDirectionBlendSpeed);
        initialIdleOrbitAngle = Mathf.Repeat(initialIdleOrbitAngle, 360f);
    }
#endif
}
