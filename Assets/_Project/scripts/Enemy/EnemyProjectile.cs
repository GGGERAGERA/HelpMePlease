using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 7f;
    [SerializeField] private int damage = 10;
    [SerializeField] private float lifetime = 5f;

    private Vector2 direction;

    public void Initialize(Vector2 shootDirection)
    {
        direction = shootDirection.normalized;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.position += (Vector3)(direction * speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        PlayerHealth health = other.GetComponent<PlayerHealth>();

        if (health != null)
        {
            Vector2 hitDirection = other.transform.position - transform.position;
            health.TakeDamage(damage, hitDirection);
        }

        Destroy(gameObject);
    }
}