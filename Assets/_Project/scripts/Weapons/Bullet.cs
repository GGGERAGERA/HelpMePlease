using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 10f;
    public float damage = 20f;
    public float destroyDuration = 5f;
    [SerializeField] private float knockbackForce = 4f;

    private Vector2 direction;
    private int pierceCount;

    public void Initialize(float bulletDamage, float bulletSpeed, Vector2 dir, int pierce = 0)
    {
        damage = bulletDamage;
        speed = bulletSpeed;
        pierceCount = pierce;
        direction = dir.normalized;
        Debug.Log("Bullet pierce: " + pierce);
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
            EnemyMovement enemyMovement = other.GetComponentInParent<EnemyMovement>();

            if (enemyMovement != null)
            {
                enemyMovement.ApplyKnockback(direction, knockbackForce);
            }
            if (pierceCount > 0)
            {
                pierceCount--;
            }
            else
            {
                Destroy(gameObject);
            }
        }


    }
}