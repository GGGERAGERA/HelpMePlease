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

    public override bool Attack()
    {
        if (fireBehaviour == null ||
            !TryGetAimDirectionFromFirePoint(out Vector2 aimDirection))
        {
            return false;
        }

        Vector2 direction = ApplyAccuracyPenalty(aimDirection);

        WeaponFireContext context = BuildFireContext(
            firePoint.position,
            direction
        );

        if (!fireBehaviour.Fire(context))
            return false;

        audioController?.PlayShot(context.Origin);
        FxPlayer?.PlayFire(context.Origin, context.Direction);
        return true;
    }
}
