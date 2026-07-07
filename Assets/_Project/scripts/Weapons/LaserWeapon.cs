using UnityEngine;
using UnityEngine.Animations.Rigging;

public class LaserWeapon : BaseWeapon
{

    [Header("Laser")]
    [SerializeField] private LayerMask hitMask;
    [SerializeField] private float beamWidth = 0.25f;
    [Header("Beam Renderer")]
    [SerializeField] private LaserBeamRenderer beamRenderer;


    private Camera mainCamera;

    protected override void Start()
    {
        base.Start();

        mainCamera = Camera.main;

        if (owner == null && transform.parent != null)
            owner = transform.parent;

        if (firePoint == null)
            firePoint = transform;
        if (beamRenderer == null)
            beamRenderer = GetComponent<LaserBeamRenderer>();
    }

    protected override void Update()
    {
        base.Update();
    }

    public override void Attack()
    {
        Vector2 direction = ApplyAccuracyPenalty(GetAimDirectionFromFirePoint());

        WeaponFireContext context = BuildFireContext(
            firePoint.position,
            direction
        );

        FireBeam(context);

        if (weaponData != null)
            PlaySound(weaponData.attackSound);

        FxPlayer?.PlayFire(context.Origin, context.Direction);
    }
    private void FireBeam(WeaponFireContext context)
    {
        float range = GetRange();

        RaycastHit2D hit = Physics2D.CircleCast(
           context.Origin,
            beamWidth * 0.5f,
            context.Direction,
            range,
            hitMask
        );

        Vector2 endPoint = context.Origin + context.Direction * context.Range;

        if (hit.collider != null)
        {
            endPoint = hit.point;
            HandleBeamHit(hit, context.Direction);
        }

        if (beamRenderer != null)
            beamRenderer.Render(context.Origin, endPoint);
    }

    private void HandleBeamHit(RaycastHit2D hit, Vector2 direction)
    {
        EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();

        if (enemy == null)
            return;

        bool isCritical = RollCritical();
        float finalDamage = GetDamage();

        if (isCritical)
            finalDamage *= GetCritMultiplier();

        enemy.TakeDamage(finalDamage, hit.point, isCritical);

        PlayerCombatModifiers modifiers = GetComponentInParent<PlayerCombatModifiers>();

        if (modifiers != null)
        {
            CombatExplosionService.TryExplodeOnHit(
                hit.point,
                finalDamage,
                modifiers,
                modifiers.enemyMask
            );
        }

        EnemyMovement movement = enemy.GetComponent<EnemyMovement>();

        if (movement != null)
        {
            Vector2 knockbackDirection = direction.normalized;
            movement.ApplyKnockback(
                knockbackDirection,
                GetKnockbackForce(3f)
            );
        }

        FxPlayer?.PlayHit(hit.point, -direction, isCritical);
    }
}