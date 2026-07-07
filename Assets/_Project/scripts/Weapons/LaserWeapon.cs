using UnityEngine;

public class LaserWeapon : BaseWeapon
{
    [Header("Beam")]
    [SerializeField] private MonoBehaviour fireBehaviourSource;

    private IWeaponFireBehaviour fireBehaviour;

    protected override void Awake()
    {
        base.Awake();

        fireBehaviour = fireBehaviourSource as IWeaponFireBehaviour;

        if (fireBehaviour == null)
            Debug.LogWarning("[LaserWeapon] Fire behaviour source is missing or invalid.");
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

        if (weaponData != null)
            PlaySound(weaponData.attackSound);

        FxPlayer?.PlayFire(context.Origin, context.Direction);
    }
}