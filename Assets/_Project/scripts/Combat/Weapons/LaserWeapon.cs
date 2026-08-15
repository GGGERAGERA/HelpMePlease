using UnityEngine;

public class LaserWeapon : BaseWeapon
{
    [Header("Beam")]
    [SerializeField] private MonoBehaviour fireBehaviourSource;
    [SerializeField] private LaserAudioController audioController;
    [SerializeField, Min(0.05f)] private float laneSpacing = 0.34f;

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

        int laneCount = Mathf.Max(1, context.ProjectileCount);
        Vector2 normal = new(-context.Direction.y, context.Direction.x);
        float spacing = laneSpacing * context.ShotVisualScale;
        bool fired = false;

        for (int i = 0; i < laneCount; i++)
        {
            float laneOffset = (i - (laneCount - 1) * 0.5f) * spacing;
            Vector2 laneOrigin = context.Origin + normal * laneOffset;
            fired |= fireBehaviour.Fire(
                context.WithOriginAndDirection(laneOrigin, context.Direction));
        }

        if (!fired)
            return false;

        audioController?.PlayShot(context.Origin);
        FxPlayer?.PlayFire(context.Origin, context.Direction);
        return true;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        laneSpacing = Mathf.Max(0.05f, laneSpacing);
    }
}
