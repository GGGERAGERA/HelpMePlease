using UnityEngine;

public class LaserWeapon : BaseWeapon
{
    [Header("Beam")]
    [SerializeField] private MonoBehaviour fireBehaviourSource;
    [SerializeField] private LaserAudioController audioController;

    private IWeaponFireBehaviour fireBehaviour;

    protected override void Awake()
    {
        base.Awake();

        fireBehaviour = fireBehaviourSource as IWeaponFireBehaviour;
        if (audioController == null)
            audioController = GetComponent<LaserAudioController>();

        if (fireBehaviour == null)
            Debug.LogWarning("[LaserWeapon] Fire behaviour source is missing or invalid.");
    }

    protected override void Update()
    {
        audioController?.SetFiring(
            Time.timeScale > 0f && IsTryingToAttack()
        );

        base.Update();
    }

    public override void Attack()
    {
        Vector2 direction = ApplyAccuracyPenalty(GetAimDirectionFromFirePoint());

        WeaponFireContext context = BuildFireContext(
            firePoint.position,
            direction
        );

        if (fireBehaviour != null)
            fireBehaviour.Fire(context);

        bool hitEnemy =
            fireBehaviour is BeamFireBehaviour beam &&
            beam.HitEnemyLastFire;

        audioController?.SetImpacting(hitEnemy);

        FxPlayer?.PlayFire(context.Origin, context.Direction);
    }
}
