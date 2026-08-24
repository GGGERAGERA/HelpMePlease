using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
[DisallowMultipleComponent]
public sealed class PhysicalCombatFeedbackRuntime : MonoBehaviour
{
    private sealed class EnemyPose
    {
        public int TargetId;
        public Transform Visual;
        public Vector3 BasePosition;
        public Vector3 BaseScale;
        public Quaternion BaseRotation;
        public Color BaseColor;
        public SpriteRenderer Renderer;
        public Animator Animator;
        public float BaseAnimatorSpeed;
        public Vector3 KickOffset;
        public float HitStartedAt;
        public float PunchStrength;
        public float PunchDuration;
        public float KickDuration;
        public float ReturnDuration;
        public float Overshoot;
        public float Rotation;
        public float RotationDuration;
        public float SquashX;
        public float StretchY;
        public float Wobble;
        public float WobbleFrequency;
        public float WobbleDamping;
        public float FrozenUntil;
        public float Fade;
        public bool DetachedForDeath;
        public float DestroyAt;
    }

    private sealed class WeaponPose
    {
        public WeaponFxPlayer Source;
        public Transform Visual;
        public Vector3 BasePosition;
        public Vector3 BaseScale;
        public Quaternion BaseRotation;
        public Vector3 RecoilOffset;
        public float Rotation;
        public float KickDuration;
        public float ReturnDuration;
        public float Overshoot;
        public float SettleDuration;
        public float ScaleX;
        public float ScaleY;
        public float StartedAt;
    }

    private static PhysicalCombatFeedbackRuntime instance;
    private static readonly CombatFeelLabSettings fallbackLab = new();

    private readonly Dictionary<int, EnemyPose> enemyPoses = new();
    private readonly Dictionary<int, WeaponPose> weaponPoses = new();
    private readonly List<int> expiredKeys = new();

    private ProductionFeelTuningController tuning;
    private bool hitStopActive;
    private float hitStopBaseline;
    private float hitStopAppliedScale;
    private float hitStopEndsAt;
    private float slowdownEndsAt;
    private float slowdownRecovery;
    private float slowdownScale = 1f;
    private Vector3 cameraAppliedOffset;
    private Vector3 cameraKick;
    private float cameraStartedAt;
    private float cameraKickDuration;
    private float cameraReturnDuration;
    private float cameraKickOvershoot;
    private Camera zoomCamera;
    private float zoomBaseline;
    private float zoomPunch;
    private float zoomStartedAt;
    private float zoomAttack;
    private float zoomReturn;
    private float lastShotAt = -10f;
    private float rapidKillIntensity;
    private float lastKillAt = -10f;

    public static string EnemyVisualRootPolicy =>
        "top-level child containing EnemyWhiteFlash.TargetRenderer";
    public static string WeaponVisualRootPolicy =>
        "first SpriteRenderer child of WeaponFxPlayer";

    public void Configure(ProductionFeelTuningController owner)
    {
        tuning = owner;
        instance = this;
    }

    private void OnEnable()
    {
        WeaponHitResolver.HitResolved += HandleHitResolved;
    }

    private void OnDisable()
    {
        WeaponHitResolver.HitResolved -= HandleHitResolved;
        CancelHitStop(true);
        RestoreAllPoses();
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        UpdateHitStop();
        UpdateEnemyPoses();
        UpdateWeaponPoses();
        rapidKillIntensity = Mathf.MoveTowards(
            rapidKillIntensity, 0f,
            V(CombatFeelParameter.RapidKillDecay) * Time.unscaledDeltaTime);
    }

    private void LateUpdate()
    {
        UpdateCameraResponse();
    }

    public static void NotifyWeaponFired(
        WeaponFxPlayer source,
        Vector2 worldDirection)
    {
        instance?.HandleWeaponFired(source, worldDirection);
    }

    public static void TryDetachDeathVisual(EnemyHealth enemy)
    {
        instance?.DetachDeathVisual(enemy);
    }

    public static float GetLabValue(CombatFeelParameter parameter) =>
        instance != null ? instance.V(parameter) : fallbackLab.Get(parameter);
    public static bool LabAvailable => instance != null;

    public static void RegisterProjectile(GameObject projectile, Vector2 direction)
    {
        if (instance == null || projectile == null) return;
        CombatFeelProjectileVisual visual =
            projectile.GetComponent<CombatFeelProjectileVisual>();
        if (visual == null)
            visual = projectile.AddComponent<CombatFeelProjectileVisual>();
        visual.Configure(instance.tuning.Lab, direction);
    }

    public static void ConfigureSpawnedEffect(
        GameObject effect, bool muzzle, Vector2 direction)
    {
        if (instance == null || effect == null) return;
        CombatFeelParticleOverride modifier =
            effect.GetComponent<CombatFeelParticleOverride>();
        if (modifier == null)
            modifier = effect.AddComponent<CombatFeelParticleOverride>();
        modifier.Configure(instance.tuning.Lab, muzzle, direction);
    }

    public static void CancelHitStopForExternalTimeControl()
    {
        instance?.CancelHitStop(true);
    }

    public void ResetPhysicalFeedback()
    {
        CancelHitStop(true);
        RestoreAllPoses();
    }

    private void HandleHitResolved(WeaponHitContext context)
    {
        if (tuning == null || context.Target == null)
            return;

        bool killed = context.Target.IsDead;
        float strength = GetImpactStrength(context, killed);
        if (killed)
        {
            if (Time.unscaledTime - lastKillAt <= V(CombatFeelParameter.RapidKillWindow))
                rapidKillIntensity = Mathf.Min(
                    V(CombatFeelParameter.RapidKillMax),
                    rapidKillIntensity + V(CombatFeelParameter.RapidKillGain));
            lastKillAt = Time.unscaledTime;
            strength *= 1f + rapidKillIntensity;
        }

        float legacyFreeze = killed
            ? tuning.KillHitStopDuration
            : context.IsCritical ? tuning.CritHitStopDuration
                : tuning.NormalHitStopDuration;
        RequestEventTime(
            legacyFreeze + V(killed ? CombatFeelParameter.KillFreeze : CombatFeelParameter.HitFreeze) * strength,
            V(killed ? CombatFeelParameter.KillSlowdown : CombatFeelParameter.HitSlowdown) * strength,
            V(killed ? CombatFeelParameter.KillSlowdownScale : CombatFeelParameter.HitSlowdownScale),
            V(killed ? CombatFeelParameter.KillRecovery : CombatFeelParameter.HitRecovery),
            context.Target);

        PlayEnemyHit(context.Target, context.Direction, killed, strength);
        PlayCameraEvent(context.Direction, context.HitPoint, killed, strength);
        if (killed)
            PlayCrowdResponse(context.Target, context.HitPoint, context.Direction, strength);
    }

    private void HandleWeaponFired(WeaponFxPlayer source, Vector2 direction)
    {
        float emphasis = Time.unscaledTime - lastShotAt > 0.3f
            ? 1f + V(CombatFeelParameter.FirstShotEmphasis) : 1f;
        lastShotAt = Time.unscaledTime;
        PlayWeaponRecoil(source, direction, emphasis);
        PlayPlayerRecoil(source, direction, emphasis);
        RequestEventTime(
            V(CombatFeelParameter.ShotFreeze),
            V(CombatFeelParameter.ShotSlowdown),
            V(CombatFeelParameter.ShotSlowdownScale),
            V(CombatFeelParameter.ShotRecovery), null);
        float shake = V(CombatFeelParameter.ShotShakeAmplitude) * emphasis;
        if (shake > 0f)
            CameraShake.Instance?.Shake(V(CombatFeelParameter.ShotShakeDuration), shake);
        StartCameraKick(direction, V(CombatFeelParameter.ShotZoomPunch) * emphasis,
            V(CombatFeelParameter.DirectionalKickDistance) * emphasis);
    }

    private float V(CombatFeelParameter parameter) =>
        tuning != null ? tuning.Lab.Get(parameter) : 0f;

    private float GetImpactStrength(WeaponHitContext context, bool killed)
    {
        float influence = V(CombatFeelParameter.DamageInfluence);
        float ratio = context.Target.MaxHealth > 0f
            ? context.Damage / context.Target.MaxHealth : 1f;
        float normalized = Mathf.Clamp(ratio * 4f,
            V(CombatFeelParameter.MinFeedback), V(CombatFeelParameter.MaxFeedback));
        float strength = Mathf.Lerp(1f, normalized, influence);
        if (context.IsCritical) strength *= V(CombatFeelParameter.CritMultiplier);
        strength *= context.Target.IsBoss
            ? V(CombatFeelParameter.BossWeight)
            : context.Target.GetComponent<GoldenEnemyModifier>() != null
                ? V(CombatFeelParameter.EliteWeight)
                : V(CombatFeelParameter.BasicWeight);
        if (killed)
        {
            float preHitHealth = Mathf.Max(.001f, context.Target.CurrentHealth + context.Damage);
            float overkill = context.Damage / preHitHealth;
            if (overkill >= V(CombatFeelParameter.OverkillThreshold))
                strength *= V(CombatFeelParameter.OverkillFeedback);
        }
        return Mathf.Clamp(strength * V(CombatFeelParameter.MasterIntensity), .05f, 6f);
    }

    private void RequestHitStop(float duration)
    {
        if (tuning == null || !tuning.HitStopEnabled || duration <= 0f ||
            Time.timeScale <= 0f)
            return;

        float now = Time.unscaledTime;
        if (!hitStopActive)
        {
            hitStopBaseline = Time.timeScale;
            hitStopAppliedScale = Mathf.Max(0.001f, hitStopBaseline * 0.05f);
            Time.timeScale = hitStopAppliedScale;
            hitStopActive = true;
        }

        hitStopEndsAt = Mathf.Max(hitStopEndsAt, now + duration);
    }

    private void RequestEventTime(
        float freeze, float slowdown, float scale, float recovery,
        EnemyHealth target)
    {
        float typeEmphasis = target == null ? 1f : target.IsBoss
            ? V(CombatFeelParameter.BossTimeEmphasis)
            : target.GetComponent<GoldenEnemyModifier>() != null
                ? V(CombatFeelParameter.EliteTimeEmphasis) : 1f;
        freeze *= typeEmphasis;
        slowdown *= typeEmphasis;

        float local = V(CombatFeelParameter.LocalEnemyFreeze) *
            (1f - V(CombatFeelParameter.FreezeBlend));
        if (target != null && local > 0f)
        {
            EnemyPose pose = GetOrCreateEnemyPose(target);
            if (pose != null)
                pose.FrozenUntil = Mathf.Max(
                    pose.FrozenUntil, Time.unscaledTime + local);
        }

        bool globalEnabled = tuning.HitStopEnabled ||
            V(CombatFeelParameter.GlobalFreeze) >= .5f ||
            V(CombatFeelParameter.FreezeBlend) > 0f;
        float globalFreeze = globalEnabled
            ? freeze * Mathf.Max(V(CombatFeelParameter.FreezeBlend),
                tuning.HitStopEnabled ? 1f : 0f)
            : 0f;
        if (globalFreeze <= 0f && slowdown <= 0f)
            return;
        if (Time.timeScale <= 0f)
            return;

        float now = Time.unscaledTime;
        if (!hitStopActive)
        {
            hitStopBaseline = Time.timeScale;
            hitStopAppliedScale = hitStopBaseline;
            hitStopActive = true;
        }
        hitStopEndsAt = Mathf.Max(hitStopEndsAt, now + globalFreeze);
        slowdownEndsAt = Mathf.Max(slowdownEndsAt, hitStopEndsAt + slowdown);
        slowdownScale = Mathf.Min(slowdownScale, Mathf.Clamp(scale, .02f, 1f));
        slowdownRecovery = Mathf.Max(.01f, recovery);
        ApplyOwnedTimeScale(now < hitStopEndsAt
            ? Mathf.Max(.001f, hitStopBaseline * .05f)
            : hitStopBaseline * slowdownScale);
    }

    private void UpdateHitStop()
    {
        if (!hitStopActive)
            return;

        if (!Mathf.Approximately(Time.timeScale, hitStopAppliedScale))
        {
            // Another pause/flow system took ownership. Never overwrite it.
            CancelHitStop(false);
            return;
        }

        float now = Time.unscaledTime;
        if (now < hitStopEndsAt)
        {
            ApplyOwnedTimeScale(Mathf.Max(.001f, hitStopBaseline * .05f));
            return;
        }
        if (now < slowdownEndsAt)
        {
            ApplyOwnedTimeScale(hitStopBaseline * slowdownScale);
            return;
        }

        float restored = Mathf.MoveTowards(
            Time.timeScale, hitStopBaseline,
            Time.unscaledDeltaTime * hitStopBaseline / slowdownRecovery);
        ApplyOwnedTimeScale(restored);
        if (Mathf.Approximately(restored, hitStopBaseline))
            CancelHitStop(true);
    }

    private void ApplyOwnedTimeScale(float value)
    {
        Time.timeScale = value;
        hitStopAppliedScale = value;
    }

    private void CancelHitStop(bool restoreBaseline)
    {
        if (!hitStopActive)
            return;

        if (restoreBaseline &&
            Mathf.Approximately(Time.timeScale, hitStopAppliedScale))
            Time.timeScale = hitStopBaseline;

        hitStopActive = false;
        hitStopEndsAt = 0f;
        slowdownEndsAt = 0f;
        slowdownScale = 1f;
    }

    private void PlayEnemyHit(
        EnemyHealth enemy,
        Vector2 worldDirection,
        bool killed,
        float strength)
    {
        bool punch = tuning.EnemyHitPunchEnabled ||
            (killed && tuning.DeathPunchEnabled) ||
            !Mathf.Approximately(V(CombatFeelParameter.HitSquashX), 0f) ||
            !Mathf.Approximately(V(CombatFeelParameter.HitStretchY), 0f) ||
            (killed && V(CombatFeelParameter.DeathScalePunch) > 0f);
        bool kick = tuning.EnemyVisualKickEnabled ||
            V(killed ? CombatFeelParameter.DeathPush :
                CombatFeelParameter.VisualHitPush) > 0f;
        if (!punch && !kick)
        {
            if (V(CombatFeelParameter.HitRotation) == 0f &&
                V(CombatFeelParameter.WobbleStrength) == 0f)
                return;
        }

        EnemyPose pose = GetOrCreateEnemyPose(enemy);
        if (pose == null || pose.Visual == null)
            return;

        pose.HitStartedAt = Time.unscaledTime;
        pose.PunchStrength = (killed
            ? Mathf.Max(tuning.DeathPunchEnabled ? tuning.DeathPunchStrength : 0f,
                V(CombatFeelParameter.DeathScalePunch))
            : tuning.EnemyHitPunchEnabled ? tuning.EnemyPunchStrength : 0f) * strength;
        pose.PunchDuration = killed
            ? Mathf.Max(tuning.DeathPunchEnabled ? tuning.DeathPunchDuration : 0f,
                V(CombatFeelParameter.DeathHold) + V(CombatFeelParameter.GhostLifetime))
            : Mathf.Max(tuning.EnemyPunchDuration,
                V(CombatFeelParameter.HitSquashDuration) +
                V(CombatFeelParameter.HitRestoreDuration));
        pose.KickDuration = killed ? .02f : V(CombatFeelParameter.HitPushDuration);
        pose.ReturnDuration = killed
            ? Mathf.Max(.01f, V(CombatFeelParameter.GhostLifetime))
            : V(CombatFeelParameter.HitReturnDuration);
        pose.Overshoot = killed ? 0f : V(CombatFeelParameter.HitOvershoot);
        pose.SquashX = (killed ? -V(CombatFeelParameter.DeathSquash) :
            V(CombatFeelParameter.HitSquashX)) * strength;
        pose.StretchY = (killed ? V(CombatFeelParameter.DeathStretch) :
            V(CombatFeelParameter.HitStretchY)) * strength;
        pose.Rotation = (killed ? V(CombatFeelParameter.DeathRotation) :
            V(CombatFeelParameter.HitRotation) +
            UnityEngine.Random.Range(-V(CombatFeelParameter.HitRotationRandomness),
                V(CombatFeelParameter.HitRotationRandomness))) * strength;
        pose.RotationDuration = killed ? pose.PunchDuration :
            V(CombatFeelParameter.HitRotationReturn);
        pose.Wobble = V(CombatFeelParameter.WobbleStrength) * strength;
        pose.WobbleFrequency = V(CombatFeelParameter.WobbleFrequency);
        pose.WobbleDamping = V(CombatFeelParameter.WobbleDamping);
        pose.Fade = killed ? V(CombatFeelParameter.GhostFade) : 0f;

        Transform parent = pose.Visual.parent;
        Vector3 localDirection = parent != null
            ? parent.InverseTransformVector(worldDirection)
            : (Vector3)worldDirection;
        float distance = killed ? V(CombatFeelParameter.DeathPush) :
            Mathf.Max(tuning.EnemyVisualKickEnabled ? tuning.EnemyKickDistance : 0f,
                V(CombatFeelParameter.VisualHitPush));
        distance *= V(CombatFeelParameter.DirectionInfluence) * strength;
        Vector2 noisy = localDirection;
        noisy = Quaternion.Euler(0, 0, UnityEngine.Random.Range(
            -45f, 45f) * V(CombatFeelParameter.DirectionRandomness)) * noisy;
        pose.KickOffset = noisy.sqrMagnitude > 0.0001f
            ? (Vector3)noisy.normalized * distance
            : Vector3.zero;
    }

    private EnemyPose GetOrCreateEnemyPose(EnemyHealth enemy)
    {
        int id = enemy.GetInstanceID();
        if (enemyPoses.TryGetValue(id, out EnemyPose existing))
            return existing;

        Transform visual = ResolveEnemyVisual(enemy);
        if (visual == null)
            return null;

        EnemyPose pose = new()
        {
            TargetId = id,
            Visual = visual,
            BasePosition = visual.localPosition,
            BaseScale = visual.localScale,
            BaseRotation = visual.localRotation,
            Renderer = visual.GetComponentInChildren<SpriteRenderer>(true)
        };
        pose.Animator = visual.GetComponentInChildren<Animator>(true);
        pose.BaseAnimatorSpeed = pose.Animator != null ? pose.Animator.speed : 1f;
        pose.BaseColor = pose.Renderer != null ? pose.Renderer.color : Color.white;
        enemyPoses.Add(id, pose);
        return pose;
    }

    private static Transform ResolveEnemyVisual(EnemyHealth enemy)
    {
        EnemyWhiteFlash flash = enemy.GetComponent<EnemyWhiteFlash>();
        SpriteRenderer renderer = flash != null ? flash.TargetRenderer : null;
        if (renderer == null)
            renderer = enemy.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer == null || renderer.transform == enemy.transform)
            return null;

        Transform visual = renderer.transform;
        while (visual.parent != null && visual.parent != enemy.transform)
            visual = visual.parent;
        return visual.parent == enemy.transform ? visual : null;
    }

    private void DetachDeathVisual(EnemyHealth enemy)
    {
        bool advancedDeath = V(CombatFeelParameter.GhostEnabled) >= .5f ||
            V(CombatFeelParameter.DeathHold) > 0f ||
            V(CombatFeelParameter.DeathFade) > 0f;
        if (tuning == null || enemy == null ||
            (!tuning.DeathPunchEnabled && !advancedDeath))
            return;

        EnemyPose pose = GetOrCreateEnemyPose(enemy);
        if (pose == null || pose.Visual == null || pose.DetachedForDeath)
            return;

        Transform visual = pose.Visual;
        Vector3 worldPosition = visual.position;
        Quaternion worldRotation = visual.rotation;
        Vector3 worldScale = visual.lossyScale;
        visual.SetParent(transform, true);
        visual.position = worldPosition;
        visual.rotation = worldRotation;
        SetWorldScale(visual, worldScale);

        pose.BasePosition = visual.localPosition;
        pose.BaseScale = visual.localScale;
        pose.BaseRotation = visual.localRotation;
        if (pose.Renderer != null)
            pose.BaseColor = pose.Renderer.color;
        pose.DetachedForDeath = true;
        pose.HitStartedAt = Time.unscaledTime;
        pose.PunchStrength = Mathf.Max(
            tuning.DeathPunchEnabled ? tuning.DeathPunchStrength : 0f,
            V(CombatFeelParameter.DeathScalePunch));
        pose.PunchDuration = Mathf.Max(
            tuning.DeathPunchEnabled ? tuning.DeathPunchDuration : 0f,
            V(CombatFeelParameter.DeathHold) +
            (V(CombatFeelParameter.GhostEnabled) >= .5f
                ? V(CombatFeelParameter.GhostLifetime) : 0f));
        pose.KickDuration = 0f;
        pose.KickOffset = Vector3.zero;
        pose.DestroyAt = Time.unscaledTime + Mathf.Max(.01f, pose.PunchDuration);
        pose.BaseScale *= V(CombatFeelParameter.GhostEnabled) >= .5f
            ? V(CombatFeelParameter.GhostScale) : 1f;
        pose.Fade = Mathf.Max(V(CombatFeelParameter.DeathFade),
            V(CombatFeelParameter.GhostEnabled) >= .5f
                ? V(CombatFeelParameter.GhostFade) : 0f);
    }

    private static void SetWorldScale(Transform target, Vector3 worldScale)
    {
        Vector3 parentScale = target.parent != null
            ? target.parent.lossyScale
            : Vector3.one;
        target.localScale = new Vector3(
            SafeDivide(worldScale.x, parentScale.x),
            SafeDivide(worldScale.y, parentScale.y),
            SafeDivide(worldScale.z, parentScale.z));
    }

    private static float SafeDivide(float value, float divisor) =>
        Mathf.Abs(divisor) > 0.0001f ? value / divisor : value;

    private void UpdateEnemyPoses()
    {
        expiredKeys.Clear();
        float now = Time.unscaledTime;
        foreach (KeyValuePair<int, EnemyPose> pair in enemyPoses)
        {
            EnemyPose pose = pair.Value;
            if (pose.Visual == null)
            {
                expiredKeys.Add(pair.Key);
                continue;
            }

            if (pose.DetachedForDeath && now >= pose.DestroyAt)
            {
                Destroy(pose.Visual.gameObject);
                expiredKeys.Add(pair.Key);
                continue;
            }

            float punchT = NormalizedAge(now, pose.HitStartedAt, pose.PunchDuration);
            float age = now - pose.HitStartedAt;
            float kickT = NormalizedAge(now, pose.HitStartedAt, pose.KickDuration);
            float returnT = NormalizedAge(now,
                pose.HitStartedAt + pose.KickDuration, pose.ReturnDuration);
            float kickCurve = kickT < 1f ? EaseOut(kickT) :
                1f - EaseWithOvershoot(returnT, pose.Overshoot);
            float punchEnvelope = 1f - Smooth(punchT);
            float punch = 1f + pose.PunchStrength * punchEnvelope;
            pose.Visual.localScale = Vector3.Scale(pose.BaseScale,
                new Vector3(
                    punch * (1f + pose.SquashX * punchEnvelope),
                    punch * (1f + pose.StretchY * punchEnvelope), 1f));
            pose.Visual.localPosition = pose.BasePosition + pose.KickOffset * kickCurve;
            float rotationT = NormalizedAge(now, pose.HitStartedAt,
                pose.RotationDuration);
            float wobble = pose.Wobble * Mathf.Sin(age * pose.WobbleFrequency) *
                Mathf.Exp(-age * pose.WobbleDamping);
            pose.Visual.localRotation = pose.BaseRotation * Quaternion.Euler(
                0f, 0f, pose.Rotation * (1f - Smooth(rotationT)) + wobble);

            if (pose.Animator != null)
                pose.Animator.speed = now < pose.FrozenUntil ? 0f : pose.BaseAnimatorSpeed;
            if (pose.Renderer != null && pose.DetachedForDeath && pose.Fade > 0f)
            {
                Color color = pose.BaseColor;
                color.a *= 1f - punchT * pose.Fade;
                pose.Renderer.color = color;
            }

            if (!pose.DetachedForDeath && punchT >= 1f && returnT >= 1f &&
                rotationT >= 1f && now >= pose.FrozenUntil)
            {
                pose.Visual.localScale = pose.BaseScale;
                pose.Visual.localPosition = pose.BasePosition;
                pose.Visual.localRotation = pose.BaseRotation;
                if (pose.Renderer != null) pose.Renderer.color = pose.BaseColor;
                if (pose.Animator != null) pose.Animator.speed = pose.BaseAnimatorSpeed;
                expiredKeys.Add(pair.Key);
            }
        }

        foreach (int key in expiredKeys)
            enemyPoses.Remove(key);
    }

    private void PlayWeaponRecoil(
        WeaponFxPlayer source,
        Vector2 worldDirection,
        float emphasis)
    {
        float advancedDistance = V(CombatFeelParameter.WeaponKickDistance);
        if (tuning == null || source == null ||
            (!tuning.WeaponVisualRecoilEnabled && advancedDistance <= 0f))
            return;

        int id = source.GetInstanceID();
        if (!weaponPoses.TryGetValue(id, out WeaponPose pose))
        {
            SpriteRenderer renderer = source.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null || renderer.transform == source.transform)
                return;

            pose = new WeaponPose
            {
                Source = source,
                Visual = renderer.transform,
                BasePosition = renderer.transform.localPosition,
                BaseScale = renderer.transform.localScale,
                BaseRotation = renderer.transform.localRotation
            };
            weaponPoses.Add(id, pose);
        }

        Transform parent = pose.Visual.parent;
        Vector3 localDirection = parent != null
            ? parent.InverseTransformVector(worldDirection)
            : (Vector3)worldDirection;
        float distance = Mathf.Max(
            tuning.WeaponVisualRecoilEnabled ? tuning.WeaponRecoilDistance : 0f,
            advancedDistance) * emphasis;
        pose.RecoilOffset = localDirection.sqrMagnitude > 0.0001f
            ? -localDirection.normalized * distance
            : Vector3.zero;
        pose.KickDuration = V(CombatFeelParameter.WeaponKickDuration);
        pose.ReturnDuration = Mathf.Max(
            tuning.WeaponVisualRecoilEnabled ? tuning.WeaponRecoilReturnDuration : 0f,
            V(CombatFeelParameter.WeaponReturnDuration));
        pose.Overshoot = V(CombatFeelParameter.WeaponOvershoot);
        pose.SettleDuration = V(CombatFeelParameter.WeaponSettleDuration);
        pose.Rotation = (V(CombatFeelParameter.WeaponKickRotation) +
            UnityEngine.Random.Range(-1f, 1f) *
            V(CombatFeelParameter.WeaponKickRandomness) * 10f) * emphasis;
        pose.ScaleX = V(CombatFeelParameter.WeaponScalePunchX) * emphasis;
        pose.ScaleY = V(CombatFeelParameter.WeaponScalePunchY) * emphasis;
        pose.StartedAt = Time.unscaledTime;
    }

    private void UpdateWeaponPoses()
    {
        expiredKeys.Clear();
        float now = Time.unscaledTime;
        foreach (KeyValuePair<int, WeaponPose> pair in weaponPoses)
        {
            WeaponPose pose = pair.Value;
            if (pose.Source == null || pose.Visual == null)
            {
                expiredKeys.Add(pair.Key);
                continue;
            }

            float kickT = NormalizedAge(now, pose.StartedAt, pose.KickDuration);
            float returnT = NormalizedAge(now,
                pose.StartedAt + pose.KickDuration, pose.ReturnDuration);
            float settleT = NormalizedAge(now,
                pose.StartedAt + pose.KickDuration + pose.ReturnDuration,
                pose.SettleDuration);
            float offsetFactor = kickT < 1f ? EaseOut(kickT) : returnT < 1f
                ? 1f - Smooth(returnT) -
                  Mathf.Sin(returnT * Mathf.PI) * pose.Overshoot
                : 0f;
            pose.Visual.localPosition = pose.BasePosition +
                pose.RecoilOffset * offsetFactor;
            float envelope = 1f - Mathf.Clamp01(
                (now - pose.StartedAt) /
                Mathf.Max(.001f, pose.KickDuration + pose.ReturnDuration));
            pose.Visual.localScale = Vector3.Scale(pose.BaseScale,
                new Vector3(1f + pose.ScaleX * envelope,
                    1f + pose.ScaleY * envelope, 1f));
            pose.Visual.localRotation = pose.BaseRotation *
                Quaternion.Euler(0f, 0f, pose.Rotation * envelope);
            if (settleT >= 1f)
            {
                pose.Visual.localPosition = pose.BasePosition;
                pose.Visual.localScale = pose.BaseScale;
                pose.Visual.localRotation = pose.BaseRotation;
                expiredKeys.Add(pair.Key);
            }
        }

        foreach (int key in expiredKeys)
            weaponPoses.Remove(key);
    }

    private void PlayPlayerRecoil(
        WeaponFxPlayer source, Vector2 worldDirection, float emphasis)
    {
        CharacterMovement2D movement = source != null
            ? source.GetComponentInParent<CharacterMovement2D>() : null;
        if (movement == null) return;

        if (V(CombatFeelParameter.PhysicalRecoil) >= .5f &&
            V(CombatFeelParameter.PlayerRecoilVelocity) > 0f)
        {
            Rigidbody2D body = movement.GetComponent<Rigidbody2D>();
            if (body != null)
                body.linearVelocity -= worldDirection.normalized *
                    V(CombatFeelParameter.PlayerRecoilVelocity) * emphasis;
        }

        float movementDamp = V(CombatFeelParameter.MovementDamp);
        if (movementDamp > 0f)
        {
            Rigidbody2D body = movement.GetComponent<Rigidbody2D>();
            if (body != null)
                body.linearVelocity *= 1f - Mathf.Clamp01(movementDamp);
        }

        float visualDistance = V(CombatFeelParameter.PlayerVisualRecoil);
        if (visualDistance <= 0f && V(CombatFeelParameter.PlayerSquash) <= 0f &&
            V(CombatFeelParameter.PlayerStretch) <= 0f) return;

        SpriteRenderer renderer = movement.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer == null) return;
        int key = ~source.GetInstanceID();
        if (!weaponPoses.TryGetValue(key, out WeaponPose pose))
        {
            pose = new WeaponPose
            {
                Source = source,
                Visual = renderer.transform,
                BasePosition = renderer.transform.localPosition,
                BaseScale = renderer.transform.localScale,
                BaseRotation = renderer.transform.localRotation
            };
            weaponPoses[key] = pose;
        }
        Transform parent = pose.Visual.parent;
        Vector3 localDirection = parent != null
            ? parent.InverseTransformVector(worldDirection) : (Vector3)worldDirection;
        pose.RecoilOffset = -localDirection.normalized * visualDistance * emphasis;
        pose.KickDuration = .01f;
        pose.ReturnDuration = V(CombatFeelParameter.PlayerVisualRecoilDuration);
        pose.SettleDuration = .01f;
        pose.Overshoot = .08f * V(CombatFeelParameter.PlayerReturnSpring);
        pose.Rotation = V(CombatFeelParameter.PlayerRotationKick) * emphasis;
        pose.ScaleX = -V(CombatFeelParameter.PlayerSquash) * emphasis;
        pose.ScaleY = V(CombatFeelParameter.PlayerStretch) * emphasis;
        pose.StartedAt = Time.unscaledTime;
    }

    private void PlayCameraEvent(
        Vector2 direction, Vector2 hitPoint, bool killed, float strength)
    {
        float amplitude = V(killed ? CombatFeelParameter.KillShakeAmplitude :
            CombatFeelParameter.HitShakeAmplitude) * strength;
        float duration = V(killed ? CombatFeelParameter.KillShakeDuration :
            CombatFeelParameter.HitShakeDuration);
        if (amplitude > 0f && duration > 0f)
            CameraShake.Instance?.Shake(duration, amplitude);

        Vector2 kickDirection = -direction.normalized;
        Camera camera = Camera.main;
        if (camera != null)
        {
            float towardHit = V(CombatFeelParameter.TowardHit);
            Vector2 toHit = hitPoint - (Vector2)camera.transform.position;
            if (toHit.sqrMagnitude > .001f)
                kickDirection += toHit.normalized * towardHit;
        }
        StartCameraKick(kickDirection,
            V(killed ? CombatFeelParameter.KillZoomPunch :
                CombatFeelParameter.HitZoomPunch) * strength,
            V(CombatFeelParameter.DirectionalKickDistance) * strength);
    }

    private void StartCameraKick(Vector2 direction, float zoom, float distance)
    {
        cameraKick = direction.sqrMagnitude > .001f
            ? (Vector3)direction.normalized * distance : Vector3.zero;
        cameraStartedAt = Time.unscaledTime;
        cameraKickDuration = V(CombatFeelParameter.DirectionalKickDuration);
        cameraReturnDuration = V(CombatFeelParameter.DirectionalReturn);
        cameraKickOvershoot = V(CombatFeelParameter.DirectionalOvershoot);

        zoomCamera = Camera.main;
        if (zoomCamera != null && zoomCamera.orthographic && zoom != 0f)
        {
            zoomBaseline = zoomCamera.orthographicSize;
            zoomPunch = zoomBaseline * zoom;
            zoomStartedAt = Time.unscaledTime;
            zoomAttack = V(CombatFeelParameter.ZoomAttack);
            zoomReturn = V(CombatFeelParameter.ZoomReturn);
        }
    }

    private void UpdateCameraResponse()
    {
        Transform cameraTransform = CameraShake.Instance != null
            ? CameraShake.Instance.transform : null;
        if (cameraTransform != null)
        {
            cameraTransform.localPosition -= cameraAppliedOffset;
            float age = Time.unscaledTime - cameraStartedAt;
            float factor;
            if (age < cameraKickDuration)
                factor = EaseOut(age / Mathf.Max(.001f, cameraKickDuration));
            else
            {
                float t = Mathf.Clamp01((age - cameraKickDuration) /
                    Mathf.Max(.001f, cameraReturnDuration));
                factor = 1f - EaseWithOvershoot(t, cameraKickOvershoot);
            }
            cameraAppliedOffset = cameraKick * factor;
            cameraTransform.localPosition += cameraAppliedOffset;
            if (age >= cameraKickDuration + cameraReturnDuration)
            {
                cameraAppliedOffset = Vector3.zero;
                cameraKick = Vector3.zero;
            }
        }

        if (zoomCamera == null || zoomPunch == 0f) return;
        float zoomAge = Time.unscaledTime - zoomStartedAt;
        float zoomFactor = zoomAge < zoomAttack
            ? EaseOut(zoomAge / Mathf.Max(.001f, zoomAttack))
            : 1f - Smooth(Mathf.Clamp01((zoomAge - zoomAttack) /
                Mathf.Max(.001f, zoomReturn)));
        zoomCamera.orthographicSize = zoomBaseline + zoomPunch * zoomFactor;
        if (zoomAge >= zoomAttack + zoomReturn)
        {
            zoomCamera.orthographicSize = zoomBaseline;
            zoomCamera = null;
            zoomPunch = 0f;
        }
    }

    private void PlayCrowdResponse(
        EnemyHealth killed, Vector2 position, Vector2 direction, float strength)
    {
        float radius = V(CombatFeelParameter.CrowdRadius);
        int maximum = Mathf.RoundToInt(V(CombatFeelParameter.CrowdMaxTargets));
        float amount = V(CombatFeelParameter.CrowdStrength);
        if (radius <= 0f || maximum <= 0 || amount <= 0f) return;
        int affected = 0;
        foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
        {
            if (enemy == null || enemy == killed || enemy.IsDead) continue;
            float distance = Vector2.Distance(position, enemy.transform.position);
            if (distance > radius) continue;
            float falloff = Mathf.Pow(1f - distance / radius,
                V(CombatFeelParameter.CrowdFalloff));
            Vector2 away = (Vector2)enemy.transform.position - position;
            PlayEnemyHit(enemy, away.sqrMagnitude > .001f ? away : direction,
                false, strength * amount * falloff);
            if (++affected >= maximum) break;
        }
    }

    private static float NormalizedAge(float now, float start, float duration) =>
        duration <= 0f ? 1f : Mathf.Clamp01((now - start) / duration);

    private static float Smooth(float value) => value * value * (3f - 2f * value);
    private static float EaseOut(float value) => 1f - Mathf.Pow(1f - value, 3f);
    private static float EaseWithOvershoot(float value, float overshoot) =>
        Smooth(value) + Mathf.Sin(value * Mathf.PI) * overshoot;

    private void RestoreAllPoses()
    {
        foreach (EnemyPose pose in enemyPoses.Values)
        {
            if (pose.Visual == null)
                continue;
            if (pose.DetachedForDeath)
                Destroy(pose.Visual.gameObject);
            else
            {
                pose.Visual.localPosition = pose.BasePosition;
                pose.Visual.localScale = pose.BaseScale;
                pose.Visual.localRotation = pose.BaseRotation;
                if (pose.Renderer != null) pose.Renderer.color = pose.BaseColor;
                if (pose.Animator != null) pose.Animator.speed = pose.BaseAnimatorSpeed;
            }
        }
        enemyPoses.Clear();

        foreach (WeaponPose pose in weaponPoses.Values)
            if (pose.Visual != null)
            {
                pose.Visual.localPosition = pose.BasePosition;
                pose.Visual.localScale = pose.BaseScale;
                pose.Visual.localRotation = pose.BaseRotation;
            }
        weaponPoses.Clear();

        if (CameraShake.Instance != null)
            CameraShake.Instance.transform.localPosition -= cameraAppliedOffset;
        cameraAppliedOffset = Vector3.zero;
        if (zoomCamera != null && zoomCamera.orthographic)
            zoomCamera.orthographicSize = zoomBaseline;
        zoomCamera = null;
    }
}
#endif
