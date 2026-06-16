using System.Collections.Generic;
using UnityEngine;

public class RocketProjectile : MonoBehaviour, IWeaponProjectile
{
    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 2.2f;
    [SerializeField] private LayerMask enemyMask;

    private float damage;
    private float speed;
    private float range;
    private Vector2 direction;
    private Vector3 startPosition;
    private bool exploded;
    private bool isCritical;

    public void Initialize(
        float damage,
        float speed,
        float range,
        Vector2 direction,
        int pierce = 0,
        bool isCritical = false,
        int ricochet = 0
    )
    {
        this.damage = damage;
        this.speed = speed;
        this.range = range;
        this.direction = direction.normalized;
        this.isCritical = isCritical;

        startPosition = transform.position;

        float angle = Mathf.Atan2(this.direction.y, this.direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        if (exploded)
            return;

        transform.position += (Vector3)(direction * speed * Time.deltaTime);

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
        }

        Destroy(gameObject);
    }
}