using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
/// <summary>Runtime tuning input implemented by development controllers.</summary>
public abstract class PhysicalCombatFeedbackTuningSource : MonoBehaviour
{
    public abstract ICombatFeelSettings FeedbackSettings { get; }
    public abstract bool HitStopEnabled { get; }
    public abstract float NormalHitStopDuration { get; }
    public abstract float CritHitStopDuration { get; }
    public abstract float KillHitStopDuration { get; }
    public abstract bool EnemyHitPunchEnabled { get; }
    public abstract float EnemyPunchStrength { get; }
    public abstract float EnemyPunchDuration { get; }
    public abstract bool EnemyVisualKickEnabled { get; }
    public abstract float EnemyKickDistance { get; }
    public abstract float EnemyKickReturnDuration { get; }
    public abstract bool WeaponVisualRecoilEnabled { get; }
    public abstract float WeaponRecoilDistance { get; }
    public abstract float WeaponRecoilReturnDuration { get; }
    public abstract bool DeathPunchEnabled { get; }
    public abstract float DeathPunchStrength { get; }
    public abstract float DeathPunchDuration { get; }
}
#endif
