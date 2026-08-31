using System.Collections.Generic;
using UnityEngine;

public class ExplosiveProjectile : MonoBehaviour, IWeaponProjectile,
    IAnomalySpeedProjectile, IAnomalyExternalVelocity
{
    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 2.2f;
    [SerializeField] private LayerMask enemyMask;

    [Header("Explosion FX")]
    [SerializeField] private GameObject explosionFxPrefab;
    [SerializeField] private float explosionFxLifetime = 1f;

    private float damage;
    private float speed;
    private float range;
    private Vector2 direction;
    private Vector3 startPosition;
    private bool exploded;
    private bool isCritical;
    private float knockbackForce;
    private PlayerCombatModifiers modifiers;
    private ProjectileCombatContext combatContext;
    private PooledGameObject pooledObject;
    private Collider2D[] explosionHits = new Collider2D[64];
    private readonly HashSet<EnemyHealth> damagedEnemies = new();
    private ContactFilter2D explosionFilter;
    private readonly AnomalySpeedMultiplierStack anomalySpeed = new();
    private readonly AnomalyExternalVelocityStack
        anomalyExternalVelocity = new();

    private void Awake()
    {
        explosionFilter = ContactFilter2D.noFilter;
        explosionFilter.SetLayerMask(enemyMask);
        explosionFilter.useTriggers = true;
    }

    public void Initialize(
        float damage,
        float speed,
        float range,
        Vector2 direction,
        int pierce = 0,
        bool isCritical = false,
        int ricochet = 0,
        float knockbackForce = 0f
    )
    {
        this.damage = damage;
        this.speed = speed;
        this.range = range;
        this.direction = direction.normalized;
        this.isCritical = isCritical;
        this.knockbackForce = knockbackForce;
        exploded = false;
        damagedEnemies.Clear();
        anomalySpeed.Clear();
        anomalyExternalVelocity.Clear();
        pooledObject ??= GetComponent<PooledGameObject>();
        combatContext ??= GetComponent<ProjectileCombatContext>();

        startPosition = transform.position;

        float angle = Mathf.Atan2(this.direction.y, this.direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        if (exploded)
            return;

        Vector2 windVelocity = WorldRuleController.Instance != null
            ? WorldRuleController.Instance.ProjectileWindVelocity
            : Vector2.zero;
        transform.position += (Vector3)(
            (direction * speed * anomalySpeed.Value + windVelocity +
             anomalyExternalVelocity.Value) *
            Time.deltaTime
        );

        if (Vector3.Distance(startPosition, transform.position) >= range)
            Explode();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (exploded)
            return;

        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();

        if (enemy != null)
            Explode();
    }

    private void Explode()
    {
        if (exploded)
            return;

        exploded = true;
        AudioService.Instance?.PlayAt(
            AudioCueId.Explosion,
            transform.position
        );
        SpawnExplosionFx();

        if (combatContext == null)
            combatContext = GetComponent<ProjectileCombatContext>();

        damagedEnemies.Clear();
        int hitCount;
        do
        {
            hitCount = Physics2D.OverlapCircle(
                transform.position,
                explosionRadius,
                explosionFilter,
                explosionHits);

            if (hitCount < explosionHits.Length)
                break;

            System.Array.Resize(
                ref explosionHits,
                explosionHits.Length * 2);
        }
        while (true);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = explosionHits[i];
            explosionHits[i] = null;
            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();

            if (enemy == null)
                continue;

            if (damagedEnemies.Contains(enemy))
                continue;

            damagedEnemies.Add(enemy);

           

            WeaponHitContext hitContext = new WeaponHitContext(
                combatContext != null ? combatContext.Weapon : null,
                combatContext != null ? combatContext.WeaponData : null,
                enemy,
                transform.position,
                direction,
                damage,
                isCritical
            );
            WeaponHitResolver.Resolve(hitContext);

            EnemyMovement enemyMovement = enemy.GetComponent<EnemyMovement>();

            if (enemyMovement != null)
                enemyMovement.ApplyKnockback(direction, knockbackForce);
        }

        Despawn();
    }
    private void SpawnExplosionFx()
    {
        if (explosionFxPrefab == null)
            return;

        GameObject fx = Instantiate(
            explosionFxPrefab,
            transform.position,
            Quaternion.identity
        );

        Destroy(fx, explosionFxLifetime);
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

    private void Despawn()
    {
        if (pooledObject != null && pooledObject.Release())
            return;

        Destroy(gameObject);
    }
}
