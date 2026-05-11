using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 10f;
    public float damage = 20f;
    public float destroyDuration = 5f;

    private Vector2 direction;

    public void Initialize(float bulletDamage, float bulletSpeed, Vector2 dir)
    {
        damage = bulletDamage;
        speed = bulletSpeed;
        direction = dir.normalized;

        Destroy(gameObject, destroyDuration);
    }

    private void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Enemy"))
            return;

        EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>();

        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage, transform.position);
        }

        Destroy(gameObject);
    }
}