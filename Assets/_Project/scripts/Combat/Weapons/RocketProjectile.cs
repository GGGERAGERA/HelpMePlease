using System.Collections.Generic;
using UnityEngine;

public class RocketProjectile : MonoBehaviour, IWeaponProjectile, IAnomalySpeedProjectile
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
    private readonly AnomalySpeedMultiplierStack anomalySpeed = new();


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

        startPosition = transform.position;

        float angle = Mathf.Atan2(this.direction.y, this.direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        if (exploded)
            return;

        transform.position += (Vector3)(
            direction * speed * anomalySpeed.Value * Time.deltaTime
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
            AudioCueId.RocketExplosion,
            transform.position
        );
        SpawnExplosionFx();

        HashSet<EnemyHealth> damagedEnemies = new HashSet<EnemyHealth>();

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            explosionRadius,
            enemyMask
        );

        foreach (Collider2D hit in hits)
        {
            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();

            if (enemy == null)
                continue;

            if (damagedEnemies.Contains(enemy))
                continue;

            damagedEnemies.Add(enemy);

           

            enemy.TakeDamage(damage, transform.position, isCritical);

            EnemyMovement enemyMovement = enemy.GetComponent<EnemyMovement>();

            if (enemyMovement != null)
                enemyMovement.ApplyKnockback(direction, knockbackForce);
        }

        Destroy(gameObject);
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

    private void OnDisable()
    {
        AnomalyProjectileLifecycle.NotifyDisabled(this);
        anomalySpeed.Clear();
    }
}
