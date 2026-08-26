using System.Collections.Generic;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
[DisallowMultipleComponent]
public sealed class ProductionFeelTuningController : MonoBehaviour
{
    public enum Preset
    {
        Production,
        Soft,
        Strong,
        Custom
    }

    private readonly Dictionary<int, WeaponValues> weaponDefaults = new();
    private readonly Dictionary<int, float> flashDefaults = new();
    private readonly Dictionary<int, MovementValues> movementDefaults = new();
    private readonly Dictionary<int, float> cameraDefaults = new();

    private WeaponFxPlayer[] weapons = System.Array.Empty<WeaponFxPlayer>();
    private EnemyWhiteFlash[] flashes = System.Array.Empty<EnemyWhiteFlash>();
    private CharacterMovement2D[] movements =
        System.Array.Empty<CharacterMovement2D>();
    private CameraFollow[] cameras = System.Array.Empty<CameraFollow>();
    private PhysicalCombatFeedbackRuntime physicalRuntime;
    private float nextRefreshTime;

    private bool fireOverride;
    private bool impactOverride;
    private bool flashOverride;
    private bool movementOverride;
    private bool cameraOverride;
    private WeaponValues weaponValues;
    private float flashDuration;
    private MovementValues movementValues;
    private float cameraDamping;

    private bool hitStopEnabled;
    private float normalHitStopDuration;
    private float critHitStopDuration;
    private float killHitStopDuration;
    private bool enemyHitPunchEnabled;
    private float enemyPunchStrength;
    private float enemyPunchDuration;
    private bool enemyVisualKickEnabled;
    private float enemyKickDistance;
    private float enemyKickReturnDuration;
    private bool weaponVisualRecoilEnabled;
    private float weaponRecoilDistance;
    private float weaponRecoilReturnDuration;
    private bool deathPunchEnabled;
    private float deathPunchStrength;
    private float deathPunchDuration;

    public CombatFeelLabSettings Lab { get; } = new();

    public Preset CurrentPreset { get; private set; } = Preset.Production;
    public bool HasWeaponFx => weapons.Length > 0;
    public bool HasHitFlash => flashes.Length > 0;
    public bool HasMovement => movements.Length > 0;
    public bool HasCameraFollow => cameras.Length > 0;

    public float FireShakeMagnitude => GetWeaponValues().FireMagnitude;
    public float FireShakeDuration => GetWeaponValues().FireDuration;
    public float HitShakeMagnitude => GetWeaponValues().HitMagnitude;
    public float HitShakeDuration => GetWeaponValues().HitDuration;
    public float CritShakeMagnitude => GetWeaponValues().CritMagnitude;
    public float CritShakeDuration => GetWeaponValues().CritDuration;
    public float HitFlashDuration => GetFlashDuration();
    public float MoveSpeed => GetMovementValues().Speed;
    public float Acceleration => GetMovementValues().Acceleration;
    public float Deceleration => GetMovementValues().Deceleration;
    public float CameraDamping => GetCameraDamping();
    public bool HitStopEnabled => hitStopEnabled;
    public float NormalHitStopDuration => normalHitStopDuration;
    public float CritHitStopDuration => critHitStopDuration;
    public float KillHitStopDuration => killHitStopDuration;
    public bool EnemyHitPunchEnabled => enemyHitPunchEnabled;
    public float EnemyPunchStrength => enemyPunchStrength;
    public float EnemyPunchDuration => enemyPunchDuration;
    public bool EnemyVisualKickEnabled => enemyVisualKickEnabled;
    public float EnemyKickDistance => enemyKickDistance;
    public float EnemyKickReturnDuration => enemyKickReturnDuration;
    public bool WeaponVisualRecoilEnabled => weaponVisualRecoilEnabled;
    public float WeaponRecoilDistance => weaponRecoilDistance;
    public float WeaponRecoilReturnDuration => weaponRecoilReturnDuration;
    public bool DeathPunchEnabled => deathPunchEnabled;
    public float DeathPunchStrength => deathPunchStrength;
    public float DeathPunchDuration => deathPunchDuration;

    public void Configure()
    {
        physicalRuntime ??= GetComponent<PhysicalCombatFeedbackRuntime>();
        physicalRuntime ??= gameObject.AddComponent<PhysicalCombatFeedbackRuntime>();
        physicalRuntime.Configure(this);
        RefreshTargets();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + 0.5f;
        RefreshTargets();
    }

    public void SetFireShakeMagnitude(float value)
    {
        WeaponValues values = GetWeaponValues();
        values.FireMagnitude = Mathf.Max(0f, value);
        ApplyFireValues(values);
    }

    public void SetFireShakeDuration(float value)
    {
        WeaponValues values = GetWeaponValues();
        values.FireDuration = Mathf.Max(0f, value);
        ApplyFireValues(values);
    }

    public void SetHitShakeMagnitude(float value)
    {
        WeaponValues values = GetWeaponValues();
        values.HitMagnitude = Mathf.Max(0f, value);
        ApplyImpactValues(values);
    }

    public void SetHitShakeDuration(float value)
    {
        WeaponValues values = GetWeaponValues();
        values.HitDuration = Mathf.Max(0f, value);
        ApplyImpactValues(values);
    }

    public void SetCritShakeMagnitude(float value)
    {
        WeaponValues values = GetWeaponValues();
        values.CritMagnitude = Mathf.Max(0f, value);
        ApplyImpactValues(values);
    }

    public void SetCritShakeDuration(float value)
    {
        WeaponValues values = GetWeaponValues();
        values.CritDuration = Mathf.Max(0f, value);
        ApplyImpactValues(values);
    }

    public void SetHitFlashDuration(float value)
    {
        flashDuration = Mathf.Max(0f, value);
        flashOverride = true;
        CurrentPreset = Preset.Custom;
        ApplyFlashOverride();
    }

    public void SetMoveSpeed(float value)
    {
        MovementValues values = GetMovementValues();
        values.Speed = Mathf.Max(0f, value);
        ApplyMovementValues(values);
    }

    public void SetAcceleration(float value)
    {
        MovementValues values = GetMovementValues();
        values.Acceleration = Mathf.Max(0f, value);
        ApplyMovementValues(values);
    }

    public void SetDeceleration(float value)
    {
        MovementValues values = GetMovementValues();
        values.Deceleration = Mathf.Max(0f, value);
        ApplyMovementValues(values);
    }

    public void SetCameraDamping(float value)
    {
        cameraDamping = Mathf.Clamp01(value);
        cameraOverride = true;
        CurrentPreset = Preset.Custom;
        ApplyCameraOverride();
    }

    public void ToggleHitStop() => SetHitStopEnabled(!hitStopEnabled);
    public void SetHitStopEnabled(bool value) => SetPhysicalValue(
        () => hitStopEnabled = value);
    public void SetNormalHitStopDuration(float value) => SetPhysicalValue(
        () => normalHitStopDuration = Mathf.Clamp(value, 0f, 0.1f));
    public void SetCritHitStopDuration(float value) => SetPhysicalValue(
        () => critHitStopDuration = Mathf.Clamp(value, 0f, 0.15f));
    public void SetKillHitStopDuration(float value) => SetPhysicalValue(
        () => killHitStopDuration = Mathf.Clamp(value, 0f, 0.15f));

    public void ToggleEnemyHitPunch() => SetEnemyHitPunchEnabled(
        !enemyHitPunchEnabled);
    public void SetEnemyHitPunchEnabled(bool value) => SetPhysicalValue(
        () => enemyHitPunchEnabled = value);
    public void SetEnemyPunchStrength(float value) => SetPhysicalValue(
        () => enemyPunchStrength = Mathf.Clamp(value, 0f, 0.35f));
    public void SetEnemyPunchDuration(float value) => SetPhysicalValue(
        () => enemyPunchDuration = Mathf.Clamp(value, 0.02f, 0.2f));

    public void ToggleEnemyVisualKick() => SetEnemyVisualKickEnabled(
        !enemyVisualKickEnabled);
    public void SetEnemyVisualKickEnabled(bool value) => SetPhysicalValue(
        () => enemyVisualKickEnabled = value);
    public void SetEnemyKickDistance(float value) => SetPhysicalValue(
        () => enemyKickDistance = Mathf.Clamp(value, 0f, 1f));
    public void SetEnemyKickReturnDuration(float value) => SetPhysicalValue(
        () => enemyKickReturnDuration = Mathf.Clamp(value, 0.02f, 0.2f));

    public void ToggleWeaponVisualRecoil() => SetWeaponVisualRecoilEnabled(
        !weaponVisualRecoilEnabled);
    public void SetWeaponVisualRecoilEnabled(bool value) => SetPhysicalValue(
        () => weaponVisualRecoilEnabled = value);
    public void SetWeaponRecoilDistance(float value) => SetPhysicalValue(
        () => weaponRecoilDistance = Mathf.Clamp(value, 0f, 1f));
    public void SetWeaponRecoilReturnDuration(float value) => SetPhysicalValue(
        () => weaponRecoilReturnDuration = Mathf.Clamp(value, 0.02f, 0.2f));

    public void ToggleDeathPunch() => SetDeathPunchEnabled(!deathPunchEnabled);
    public void SetDeathPunchEnabled(bool value) => SetPhysicalValue(
        () => deathPunchEnabled = value);
    public void SetDeathPunchStrength(float value) => SetPhysicalValue(
        () => deathPunchStrength = Mathf.Clamp(value, 0f, 0.5f));
    public void SetDeathPunchDuration(float value) => SetPhysicalValue(
        () => deathPunchDuration = Mathf.Clamp(value, 0.02f, 0.2f));

    public void ApplyPreset(Preset preset)
    {
        if (preset == Preset.Production)
        {
            ResetAll();
            return;
        }

        if (preset == Preset.Custom)
            return;

        ResetAll();
        RefreshTargets();
        float scale = preset == Preset.Soft ? 0.55f : 2.25f;
        float durationScale = preset == Preset.Soft ? 0.75f : 1.65f;

        if (HasWeaponFx)
        {
            WeaponValues values = GetWeaponValues();
            values.FireMagnitude *= scale;
            values.HitMagnitude *= scale;
            values.CritMagnitude *= scale;
            values.FireDuration *= durationScale;
            values.HitDuration *= durationScale;
            values.CritDuration *= durationScale;
            weaponValues = values;
            fireOverride = true;
            impactOverride = true;
            ApplyFireOverride();
            ApplyImpactOverride();
        }

        if (HasHitFlash)
        {
            flashDuration = GetFlashDuration() * durationScale;
            flashOverride = true;
            ApplyFlashOverride();
        }

        if (HasCameraFollow)
        {
            cameraDamping = Mathf.Clamp01(
                GetCameraDamping() * (preset == Preset.Soft ? 0.65f : 2f));
            cameraOverride = true;
            ApplyCameraOverride();
        }

        if (HasMovement)
        {
            MovementValues values = GetMovementValues();
            values.Speed *= preset == Preset.Soft ? 0.9f : 1.15f;
            values.Acceleration *= preset == Preset.Soft ? 0.65f : 1.8f;
            values.Deceleration *= preset == Preset.Soft ? 0.7f : 1.8f;
            ApplyMovementValues(values, false);
        }

        if (preset == Preset.Soft)
        {
            SetPhysicalPreset(
                0.018f, 0.025f, 0.035f,
                0.06f, 0.07f,
                0.06f, 0.08f,
                0.045f, 0.09f,
                0.10f, 0.08f);
        }
        else
        {
            SetPhysicalPreset(
                0.065f, 0.09f, 0.12f,
                0.30f, 0.13f,
                0.42f, 0.16f,
                0.32f, 0.18f,
                0.45f, 0.16f);
        }

        CurrentPreset = preset;
    }

    public void ResetWeapon()
    {
        fireOverride = false;
        foreach (WeaponFxPlayer target in weapons)
        {
            if (target != null && weaponDefaults.TryGetValue(
                    target.GetInstanceID(), out WeaponValues values))
                values.ApplyFire(target);
        }
        CurrentPreset = Preset.Custom;
    }

    public void ResetHit()
    {
        flashOverride = false;
        foreach (EnemyWhiteFlash target in flashes)
        {
            if (target != null && flashDefaults.TryGetValue(
                    target.GetInstanceID(), out float value))
                target.SetFlashDuration(value);
        }
        CurrentPreset = Preset.Custom;
    }

    public void ResetCamera()
    {
        cameraOverride = false;
        foreach (CameraFollow target in cameras)
        {
            if (target != null && cameraDefaults.TryGetValue(
                    target.GetInstanceID(), out float value))
                target.smoothSpeed = value;
        }
        impactOverride = false;
        foreach (WeaponFxPlayer target in weapons)
        {
            if (target != null && weaponDefaults.TryGetValue(
                    target.GetInstanceID(), out WeaponValues values))
                values.ApplyImpact(target);
        }
        CurrentPreset = Preset.Custom;
    }

    public void ResetMovement()
    {
        movementOverride = false;
        foreach (CharacterMovement2D target in movements)
        {
            if (target != null && movementDefaults.TryGetValue(
                    target.GetInstanceID(), out MovementValues values))
                values.Apply(target);
        }
        CurrentPreset = Preset.Custom;
    }

    public void ResetPhysicalFeedback()
    {
        hitStopEnabled = false;
        normalHitStopDuration = 0f;
        critHitStopDuration = 0f;
        killHitStopDuration = 0f;
        enemyHitPunchEnabled = false;
        enemyPunchStrength = 0f;
        enemyPunchDuration = 0f;
        enemyVisualKickEnabled = false;
        enemyKickDistance = 0f;
        enemyKickReturnDuration = 0f;
        weaponVisualRecoilEnabled = false;
        weaponRecoilDistance = 0f;
        weaponRecoilReturnDuration = 0f;
        deathPunchEnabled = false;
        deathPunchStrength = 0f;
        deathPunchDuration = 0f;
        physicalRuntime?.ResetPhysicalFeedback();
        CurrentPreset = Preset.Custom;
    }

    public void ResetAll()
    {
        ResetWeapon();
        ResetHit();
        ResetCamera();
        ResetMovement();
        ResetPhysicalFeedback();
        Lab.ResetAll();
        CurrentPreset = Preset.Production;
    }

    public string GetValuesText()
    {
        string advanced = Lab.GetFullConfig();
        if (advanced != "COMBAT FEEL CONFIG")
            return advanced;

        StringBuilder result = new();
        result.AppendLine("FEEL");
        if (HasWeaponFx)
        {
            result.AppendLine();
            result.AppendLine("WEAPON");
            result.AppendLine($"FireCameraKick = {FireShakeMagnitude:0.###}");
            result.AppendLine($"FireShakeDuration = {FireShakeDuration:0.###}");
        }
        if (HasHitFlash)
        {
            result.AppendLine();
            result.AppendLine("HIT");
            result.AppendLine($"FlashDuration = {HitFlashDuration:0.###}");
        }
        if (HasCameraFollow || HasWeaponFx)
        {
            result.AppendLine();
            result.AppendLine("CAMERA");
            if (HasCameraFollow)
                result.AppendLine($"FollowSmoothness = {CameraDamping:0.###}");
            if (HasWeaponFx)
            {
                result.AppendLine($"HitImpulse = {HitShakeMagnitude:0.###}");
                result.AppendLine($"HitImpulseDuration = {HitShakeDuration:0.###}");
                result.AppendLine($"CritImpulse = {CritShakeMagnitude:0.###}");
                result.AppendLine($"CritImpulseDuration = {CritShakeDuration:0.###}");
            }
        }
        if (HasMovement)
        {
            result.AppendLine();
            result.AppendLine("MOVEMENT");
            result.AppendLine($"Speed = {MoveSpeed:0.###}");
            result.AppendLine($"Acceleration = {Acceleration:0.###}");
            result.AppendLine($"Deceleration = {Deceleration:0.###}");
        }
        result.AppendLine();
        result.AppendLine("PHYSICAL FEEDBACK");
        result.AppendLine($"HitStop = {HitStopEnabled}");
        result.AppendLine($"NormalHitStopDuration = {NormalHitStopDuration:0.###}");
        result.AppendLine($"CritHitStopDuration = {CritHitStopDuration:0.###}");
        result.AppendLine($"KillHitStopDuration = {KillHitStopDuration:0.###}");
        result.AppendLine($"EnemyHitPunch = {EnemyHitPunchEnabled}");
        result.AppendLine($"PunchStrength = {EnemyPunchStrength:0.###}");
        result.AppendLine($"PunchDuration = {EnemyPunchDuration:0.###}");
        result.AppendLine($"EnemyVisualKick = {EnemyVisualKickEnabled}");
        result.AppendLine($"KickDistance = {EnemyKickDistance:0.###}");
        result.AppendLine($"KickReturnDuration = {EnemyKickReturnDuration:0.###}");
        result.AppendLine($"WeaponVisualRecoil = {WeaponVisualRecoilEnabled}");
        result.AppendLine($"RecoilDistance = {WeaponRecoilDistance:0.###}");
        result.AppendLine($"RecoilReturnDuration = {WeaponRecoilReturnDuration:0.###}");
        result.AppendLine($"DeathPunch = {DeathPunchEnabled}");
        result.AppendLine($"DeathPunchStrength = {DeathPunchStrength:0.###}");
        result.AppendLine($"DeathPunchDuration = {DeathPunchDuration:0.###}");
        return result.ToString().TrimEnd();
    }

    public bool SaveTuningPreset(out string message) =>
        CombatFeelTuningPresetStorage.Save(Lab, out message);

    private void SetPhysicalValue(System.Action setter)
    {
        setter?.Invoke();
        CurrentPreset = Preset.Custom;
    }

    private void SetPhysicalPreset(
        float normalStop,
        float critStop,
        float killStop,
        float punchStrength,
        float punchDuration,
        float kickDistance,
        float kickDuration,
        float recoilDistance,
        float recoilDuration,
        float finalPunchStrength,
        float finalPunchDuration)
    {
        hitStopEnabled = true;
        normalHitStopDuration = normalStop;
        critHitStopDuration = critStop;
        killHitStopDuration = killStop;
        enemyHitPunchEnabled = true;
        enemyPunchStrength = punchStrength;
        enemyPunchDuration = punchDuration;
        enemyVisualKickEnabled = true;
        enemyKickDistance = kickDistance;
        enemyKickReturnDuration = kickDuration;
        weaponVisualRecoilEnabled = true;
        weaponRecoilDistance = recoilDistance;
        weaponRecoilReturnDuration = recoilDuration;
        deathPunchEnabled = true;
        deathPunchStrength = finalPunchStrength;
        deathPunchDuration = finalPunchDuration;
    }

    private void RefreshTargets()
    {
        weapons = FindObjectsByType<WeaponFxPlayer>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        flashes = FindObjectsByType<EnemyWhiteFlash>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        movements = FindObjectsByType<CharacterMovement2D>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        cameras = FindObjectsByType<CameraFollow>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (WeaponFxPlayer target in weapons)
        {
            if (target == null) continue;
            int id = target.GetInstanceID();
            if (!weaponDefaults.ContainsKey(id))
                weaponDefaults.Add(id, WeaponValues.From(target));
        }
        foreach (EnemyWhiteFlash target in flashes)
        {
            if (target == null) continue;
            int id = target.GetInstanceID();
            if (!flashDefaults.ContainsKey(id))
                flashDefaults.Add(id, target.FlashDuration);
        }
        foreach (CharacterMovement2D target in movements)
        {
            if (target == null) continue;
            int id = target.GetInstanceID();
            if (!movementDefaults.ContainsKey(id))
                movementDefaults.Add(id, MovementValues.From(target));
        }
        foreach (CameraFollow target in cameras)
        {
            if (target == null) continue;
            int id = target.GetInstanceID();
            if (!cameraDefaults.ContainsKey(id))
                cameraDefaults.Add(id, target.smoothSpeed);
        }

        if (fireOverride) ApplyFireOverride();
        if (impactOverride) ApplyImpactOverride();
        if (flashOverride) ApplyFlashOverride();
        if (movementOverride) ApplyMovementOverride();
        if (cameraOverride) ApplyCameraOverride();
    }

    private WeaponValues GetWeaponValues()
    {
        WeaponValues values = weapons.Length > 0 && weapons[0] != null
            ? WeaponValues.From(weapons[0])
            : default;
        if (fireOverride)
        {
            values.FireMagnitude = weaponValues.FireMagnitude;
            values.FireDuration = weaponValues.FireDuration;
        }
        if (impactOverride)
        {
            values.HitMagnitude = weaponValues.HitMagnitude;
            values.HitDuration = weaponValues.HitDuration;
            values.CritMagnitude = weaponValues.CritMagnitude;
            values.CritDuration = weaponValues.CritDuration;
        }
        return values;
    }

    private float GetFlashDuration() => flashOverride
        ? flashDuration
        : flashes.Length > 0 && flashes[0] != null
            ? flashes[0].FlashDuration
            : 0f;

    private MovementValues GetMovementValues() => movementOverride
        ? movementValues
        : movements.Length > 0 && movements[0] != null
            ? MovementValues.From(movements[0])
            : default;

    private float GetCameraDamping() => cameraOverride
        ? cameraDamping
        : cameras.Length > 0 && cameras[0] != null
            ? cameras[0].smoothSpeed
            : 0f;

    private void ApplyFireValues(WeaponValues values)
    {
        weaponValues = values;
        fireOverride = true;
        CurrentPreset = Preset.Custom;
        ApplyFireOverride();
    }

    private void ApplyImpactValues(WeaponValues values)
    {
        weaponValues = values;
        impactOverride = true;
        CurrentPreset = Preset.Custom;
        ApplyImpactOverride();
    }

    private void ApplyMovementValues(MovementValues values, bool custom = true)
    {
        movementValues = values;
        movementOverride = true;
        if (custom) CurrentPreset = Preset.Custom;
        ApplyMovementOverride();
    }

    private void ApplyFireOverride()
    {
        foreach (WeaponFxPlayer target in weapons)
            if (target != null) weaponValues.ApplyFire(target);
    }

    private void ApplyImpactOverride()
    {
        foreach (WeaponFxPlayer target in weapons)
            if (target != null) weaponValues.ApplyImpact(target);
    }

    private void ApplyFlashOverride()
    {
        foreach (EnemyWhiteFlash target in flashes)
            if (target != null) target.SetFlashDuration(flashDuration);
    }

    private void ApplyMovementOverride()
    {
        foreach (CharacterMovement2D target in movements)
            if (target != null) movementValues.Apply(target);
    }

    private void ApplyCameraOverride()
    {
        foreach (CameraFollow target in cameras)
            if (target != null) target.smoothSpeed = cameraDamping;
    }

    private struct WeaponValues
    {
        public float FireMagnitude;
        public float FireDuration;
        public float HitMagnitude;
        public float HitDuration;
        public float CritMagnitude;
        public float CritDuration;

        public static WeaponValues From(WeaponFxPlayer target) => new()
        {
            FireMagnitude = target.FireShakeMagnitude,
            FireDuration = target.FireShakeDuration,
            HitMagnitude = target.HitShakeMagnitude,
            HitDuration = target.HitShakeDuration,
            CritMagnitude = target.CritShakeMagnitude,
            CritDuration = target.CritShakeDuration
        };

        public void ApplyFire(WeaponFxPlayer target)
        {
            target.SetFireShakeMagnitude(FireMagnitude);
            target.SetFireShakeDuration(FireDuration);
        }

        public void ApplyImpact(WeaponFxPlayer target)
        {
            target.SetHitShakeMagnitude(HitMagnitude);
            target.SetHitShakeDuration(HitDuration);
            target.SetCritShakeMagnitude(CritMagnitude);
            target.SetCritShakeDuration(CritDuration);
        }
    }

    private struct MovementValues
    {
        public float Speed;
        public float Acceleration;
        public float Deceleration;

        public static MovementValues From(CharacterMovement2D target) => new()
        {
            Speed = target.speed,
            Acceleration = target.DebugAcceleration,
            Deceleration = target.DebugDeceleration
        };

        public void Apply(CharacterMovement2D target)
        {
            target.SetDebugMoveSpeed(Speed);
            target.SetDebugAcceleration(Acceleration);
            target.SetDebugDeceleration(Deceleration);
        }
    }
}
#endif
