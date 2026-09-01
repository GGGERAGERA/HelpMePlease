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
    private float remainingLifetime;
    private float rangeSquared;
    private ProjectileCombatContext combatContext;
    private PooledGameObject pooledObject;
    private Collider2D[] ricochetHits = new Collider2D[32];
    private ContactFilter2D ricochetFilter;
    private readonly AnomalySpeedMultiplierStack anomalySpeed = new();
    private readonly AnomalyExternalVelocityStack
        anomalyExternalVelocity = new();

    private readonly HashSet<EnemyHealth> hitEnemies = new HashSet<EnemyHealth>();
    private readonly HashSet<WorldBreakable> hitBreakables =
        new HashSet<WorldBreakable>();

    private void Awake()
    {
        ricochetFilter = ContactFilter2D.noFilter;
        ricochetFilter.useTriggers = true;
    }

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
        remainingLifetime = maxLifetime;
        rangeSquared = Mathf.Max(0f, range * range);
        hitEnemies.Clear();
        hitBreakables.Clear();
        anomalySpeed.Clear();
        anomalyExternalVelocity.Clear();
        pooledObject ??= GetComponent<PooledGameObject>();
        combatContext ??= GetComponent<ProjectileCombatContext>();
    }

    private void Update()
    {
        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f)
        {
            Despawn();
            return;
        }

        Vector2 windVelocity = WorldRuleController.Instance != null
            ? WorldRuleController.Instance.ProjectileWindVelocity
            : Vector2.zero;
        transform.Translate(
            (direction * speed * anomalySpeed.Value + windVelocity +
             anomalyExternalVelocity.Value) *
            Time.deltaTime,
            Space.World
        );

        if ((transform.position - startPosition).sqrMagnitude >= rangeSquared)
            Despawn();
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
        {
            WorldBreakable breakable =
                other.GetComponentInParent<WorldBreakable>();

            if (breakable == null || hitBreakables.Contains(breakable))
                return;

            hitBreakables.Add(breakable);
            breakable.TakeDamage(damage, transform.position);

            if (pierceCount > 0)
            {
                pierceCount--;
                return;
            }

            Despawn();
            return;
        }

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

        Despawn();
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
        int hitCount;
        do
        {
            hitCount = Physics2D.OverlapCircle(
                transform.position,
                ricochetSearchRadius,
                ricochetFilter,
                ricochetHits);

            if (hitCount < ricochetHits.Length)
                break;

            System.Array.Resize(
                ref ricochetHits,
                ricochetHits.Length * 2);
        }
        while (true);

        EnemyHealth nearestEnemy = null;
        float nearestDistanceSquared = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = ricochetHits[i];
            ricochetHits[i] = null;
            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();

            if (enemy == null)
                continue;

            if (enemy == ignoredEnemy)
                continue;

            if (hitEnemies.Contains(enemy))
                continue;

            float distanceSquared =
                ((Vector2)transform.position -
                 (Vector2)enemy.transform.position).sqrMagnitude;

            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestEnemy = enemy;
            }
        }

        return nearestEnemy;
    }

    private void Despawn()
    {
        if (pooledObject != null && pooledObject.Release())
            return;

        Destroy(gameObject);
    }
}
