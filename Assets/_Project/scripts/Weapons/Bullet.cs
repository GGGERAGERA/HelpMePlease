using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour, IWeaponProjectile
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
        transform.Translate(direction * speed * Time.deltaTime, Space.World);

        if (Vector3.Distance(startPosition, transform.position) >= range)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        EnemyHealth enemyHealth = other.GetComponentInParent<EnemyHealth>();

        if (enemyHealth == null)
            return;

        if (hitEnemies.Contains(enemyHealth))
            return;

        hitEnemies.Add(enemyHealth);

        ProjectileCombatContext context = GetComponent<ProjectileCombatContext>();

        enemyHealth.TakeDamage(damage, transform.position, isCritical);



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