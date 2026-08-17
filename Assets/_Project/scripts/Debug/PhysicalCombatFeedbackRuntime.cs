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
        public Vector3 KickOffset;
        public float HitStartedAt;
        public float PunchStrength;
        public float PunchDuration;
        public float KickDuration;
        public bool DetachedForDeath;
        public float DestroyAt;
    }

    private sealed class WeaponPose
    {
        public WeaponFxPlayer Source;
        public Transform Visual;
        public Vector3 BasePosition;
        public Vector3 RecoilOffset;
        public float StartedAt;
    }

    private static PhysicalCombatFeedbackRuntime instance;

    private readonly Dictionary<int, EnemyPose> enemyPoses = new();
    private readonly Dictionary<int, WeaponPose> weaponPoses = new();
    private readonly List<int> expiredKeys = new();

    private ProductionFeelTuningController tuning;
    private bool hitStopActive;
    private float hitStopBaseline;
    private float hitStopAppliedScale;
    private float hitStopEndsAt;

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
    }

    public static void NotifyWeaponFired(
        WeaponFxPlayer source,
        Vector2 worldDirection)
    {
        instance?.PlayWeaponRecoil(source, worldDirection);
    }

    public static void TryDetachDeathVisual(EnemyHealth enemy)
    {
        instance?.DetachDeathVisual(enemy);
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
        RequestHitStop(killed
            ? tuning.KillHitStopDuration
            : context.IsCritical
                ? tuning.CritHitStopDuration
                : tuning.NormalHitStopDuration);

        PlayEnemyHit(context.Target, context.Direction, killed);
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

        if (Time.unscaledTime >= hitStopEndsAt)
            CancelHitStop(true);
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
    }

    private void PlayEnemyHit(
        EnemyHealth enemy,
        Vector2 worldDirection,
        bool killed)
    {
        bool punch = tuning.EnemyHitPunchEnabled ||
            (killed && tuning.DeathPunchEnabled);
        bool kick = tuning.EnemyVisualKickEnabled;
        if (!punch && !kick)
            return;

        EnemyPose pose = GetOrCreateEnemyPose(enemy);
        if (pose == null || pose.Visual == null)
            return;

        pose.HitStartedAt = Time.unscaledTime;
        pose.PunchStrength = killed && tuning.DeathPunchEnabled
            ? tuning.DeathPunchStrength
            : tuning.EnemyPunchStrength;
        pose.PunchDuration = killed && tuning.DeathPunchEnabled
            ? tuning.DeathPunchDuration
            : tuning.EnemyPunchDuration;
        pose.KickDuration = tuning.EnemyKickReturnDuration;

        Transform parent = pose.Visual.parent;
        Vector3 localDirection = parent != null
            ? parent.InverseTransformVector(worldDirection)
            : (Vector3)worldDirection;
        pose.KickOffset = localDirection.sqrMagnitude > 0.0001f
            ? localDirection.normalized * tuning.EnemyKickDistance
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
            BaseScale = visual.localScale
        };
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
        if (tuning == null || !tuning.DeathPunchEnabled || enemy == null ||
            tuning.DeathPunchDuration <= 0f)
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
        pose.DetachedForDeath = true;
        pose.HitStartedAt = Time.unscaledTime;
        pose.PunchStrength = tuning.DeathPunchStrength;
        pose.PunchDuration = tuning.DeathPunchDuration;
        pose.KickDuration = 0f;
        pose.KickOffset = Vector3.zero;
        pose.DestroyAt = Time.unscaledTime + tuning.DeathPunchDuration;
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
            float kickT = NormalizedAge(now, pose.HitStartedAt, pose.KickDuration);
            float punch = 1f + pose.PunchStrength * (1f - punchT);
            pose.Visual.localScale = pose.BaseScale * punch;
            pose.Visual.localPosition = pose.BasePosition +
                pose.KickOffset * (1f - kickT);

            if (!pose.DetachedForDeath && punchT >= 1f && kickT >= 1f)
            {
                pose.Visual.localScale = pose.BaseScale;
                pose.Visual.localPosition = pose.BasePosition;
                expiredKeys.Add(pair.Key);
            }
        }

        foreach (int key in expiredKeys)
            enemyPoses.Remove(key);
    }

    private void PlayWeaponRecoil(
        WeaponFxPlayer source,
        Vector2 worldDirection)
    {
        if (tuning == null || !tuning.WeaponVisualRecoilEnabled ||
            tuning.WeaponRecoilDistance <= 0f || source == null)
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
                BasePosition = renderer.transform.localPosition
            };
            weaponPoses.Add(id, pose);
        }

        Transform parent = pose.Visual.parent;
        Vector3 localDirection = parent != null
            ? parent.InverseTransformVector(worldDirection)
            : (Vector3)worldDirection;
        pose.RecoilOffset = localDirection.sqrMagnitude > 0.0001f
            ? -localDirection.normalized * tuning.WeaponRecoilDistance
            : Vector3.zero;
        pose.StartedAt = Time.unscaledTime;
    }

    private void UpdateWeaponPoses()
    {
        expiredKeys.Clear();
        float now = Time.unscaledTime;
        float duration = tuning != null ? tuning.WeaponRecoilReturnDuration : 0f;
        foreach (KeyValuePair<int, WeaponPose> pair in weaponPoses)
        {
            WeaponPose pose = pair.Value;
            if (pose.Source == null || pose.Visual == null)
            {
                expiredKeys.Add(pair.Key);
                continue;
            }

            float t = NormalizedAge(now, pose.StartedAt, duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            pose.Visual.localPosition = pose.BasePosition +
                Vector3.LerpUnclamped(pose.RecoilOffset, Vector3.zero, eased);
            if (t >= 1f)
            {
                pose.Visual.localPosition = pose.BasePosition;
                expiredKeys.Add(pair.Key);
            }
        }

        foreach (int key in expiredKeys)
            weaponPoses.Remove(key);
    }

    private static float NormalizedAge(float now, float start, float duration) =>
        duration <= 0f ? 1f : Mathf.Clamp01((now - start) / duration);

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
            }
        }
        enemyPoses.Clear();

        foreach (WeaponPose pose in weaponPoses.Values)
            if (pose.Visual != null)
                pose.Visual.localPosition = pose.BasePosition;
        weaponPoses.Clear();
    }
}
#endif
