using UnityEngine;

public class LaserWeapon : BaseWeapon
{
    [Header("Beam")]
    [SerializeField] private MonoBehaviour fireBehaviourSource;
    [SerializeField] private LaserAudioController audioController;

    private IWeaponFireBehaviour fireBehaviour;

    protected override WeaponShotKind ShotKind => WeaponShotKind.Laser;
    public override WeaponUpgradeCapability UpgradeCapabilities =>
        WeaponUpgradeCapability.MultiProjectile;

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

        return EmitAttack(firePoint.position, aimDirection);
    }

    protected override bool EmitAttack(WeaponFireContext context)
    {
        if (fireBehaviour == null)
            return false;

        int beamCount = Mathf.Max(1, context.ProjectileCount);
        bool fired = false;

        for (int i = 0; i < beamCount; i++)
        {
            float angleOffset = WeaponShotSpread.GetAngleOffset(
                i,
                beamCount);
            Vector2 direction = WeaponShotSpread.RotateDirection(
                context.Direction,
                angleOffset);
            fired |= fireBehaviour.Fire(
                context.WithOriginAndDirection(context.Origin, direction));
        }

        if (!fired)
            return false;

        audioController?.PlayShot(context.Origin);
        FxPlayer?.PlayFire(context.Origin, context.Direction);
        return true;
    }

}
