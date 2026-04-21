using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 10f;
    public float damage = 20f;
    public float destroyDuration = 5f;
    private Vector2 direction;


    public void Initialize(float bulletDamage, float bulletSpeed, Vector2 dir)
    {
        speed = bulletSpeed;
        damage = bulletDamage;
        direction = dir.normalized;
        Destroy(gameObject, destroyDuration);
    }

    void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            other.GetComponent<EnemyHealth>()?.TakeDamage(20f);
            Destroy(gameObject);
        }
    }
}