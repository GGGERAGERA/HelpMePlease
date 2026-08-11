using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour, IWeaponProjectile,
    IAnomalySpeedProjectile, IAnomalyExternalVelocity
{
    public float speed = 10f;
    public float damage = 20f;
    public float range = 10f;

    [SerializeField] private float ricochetSearchRadius = 4f;
    [SerializeField] private float maxLifetime = 5f;

    private Vector2 direction;
    private Vector3 startPosition;
    private int pierceCount;
    private int ricochetCount;
    private bool isCritical;
    private float runtimeKnockbackForce;
    private ProjectileCombatContext combatContext;
    private readonly AnomalySpeedMultiplierStack anomalySpeed = new();
    private readonly AnomalyExternalVelocityStack
        anomalyExternalVelocity = new();

    private readonly HashSet<EnemyHealth> hitEnemies = new HashSet<EnemyHealth>();

    public void Initialize(
        float bulletDamage,
        float bulletSpeed,
        float bulletRange,
        Vector2 dir,
        int pierce = 0,
        bool critical = false,
        int ricochet = 0,
        float knockbackForce = 0f
    )
    {
        damage = bulletDamage;
        speed = bulletSpeed;
        range = bulletRange;
        direction = dir.normalized;
        pierceCount = pierce;
        isCritical = critical;
        ricochetCount = ricochet;
        startPosition = transform.position;
        runtimeKnockbackForce = knockbackForce;
    }
    private void Start()
    {
        Destroy(gameObject, maxLifetime);
    }
    private void Update()
    {
        Vector2 windVelocity = WorldRuleController.Instance != null
            ? WorldRuleController.Instance.ProjectileWindVelocity
            : Vector2.zero;
        transform.Translate(
            (direction * speed * anomalySpeed.Value + windVelocity +
             anomalyExternalVelocity.Value) *
            Time.deltaTime,
            Space.World
        );

        if (Vector3.Distance(startPosition, transform.position) >= range)
            Destroy(gameObject);
    }

    public Component ProjectileComponent => this;
    public Component ExternalVelocityComponent => this;
    public float AnomalySpeedMultiplier => anomalySpeed.Value;

    public void SetAnomalySpeedMultiplier(Object source, float multiplier)
    {
        anomalySpeed.Set(source, multiplier);
    }

    public void RemoveAnomalySpeedMultiplier(Object source)
    {
        anomalySpeed.Remove(source);
    }

    public void ClearAnomalySpeedMultipliers()
    {
        anomalySpeed.Clear();
    }

    public void SetAnomalyExternalVelocity(
        Object source,
        Vector2 velocity)
    {
        anomalyExternalVelocity.Set(source, velocity);
    }

    public void RemoveAnomalyExternalVelocity(Object source)
    {
        anomalyExternalVelocity.Remove(source);
    }

    private void OnDisable()
    {
        AnomalyProjectileLifecycle.NotifyDisabled(this);
        anomalySpeed.Clear();
        anomalyExternalVelocity.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        EnemyHealth enemyHealth = other.GetComponentInParent<EnemyHealth>();

        if (enemyHealth == null)
            return;

        if (hitEnemies.Contains(enemyHealth))
            return;

        if (combatContext == null)
            combatContext = GetComponent<ProjectileCombatContext>();

        hitEnemies.Add(enemyHealth);
        WeaponHitResolver.Resolve(new WeaponHitContext(
            combatContext != null ? combatContext.Weapon : null,
            combatContext != null ? combatContext.WeaponData : null,
            enemyHealth,
            transform.position,
            direction,
            damage,
            isCritical
        ));

        PlayerCombatModifiers modifiers = combatContext != null
            ? combatContext.Modifiers
            : null;

        if (modifiers != null)
        {
            CombatExplosionService.TryExplodeOnHit(
                transform.position,
                damage,
                modifiers,
                modifiers.enemyMask
            );
        }



        EnemyMovement enemyMovement = enemyHealth.GetComponent<EnemyMovement>();

        if (enemyMovement != null)
            enemyMovement.ApplyKnockback(direction, runtimeKnockbackForce);

        if (ricochetCount > 0 && TryRicochet(enemyHealth))
        {
            ricochetCount--;
            return;
        }

        if (pierceCount > 0)
        {
            pierceCount--;
            return;
        }

        Destroy(gameObject);
    }

    private bool TryRicochet(EnemyHealth currentEnemy)
    {
        EnemyHealth target = FindNearestEnemy(currentEnemy);

        if (target == null)
            return false;

        direction = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
        startPosition = transform.position;

        return true;
    }

    private EnemyHealth FindNearestEnemy(EnemyHealth ignoredEnemy)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, ricochetSearchRadius);

        EnemyHealth nearestEnemy = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();

            if (enemy == null)
                continue;

            if (enemy == ignoredEnemy)
                continue;

            if (hitEnemies.Contains(enemy))
                continue;

            float distance = Vector2.Distance(transform.position, enemy.transform.position);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestEnemy = enemy;
            }
        }

        return nearestEnemy;
    }
}
