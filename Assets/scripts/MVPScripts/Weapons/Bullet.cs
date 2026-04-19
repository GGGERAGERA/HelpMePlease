using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float damage;
    public float speed;
    private Transform target;

    public float timeBulletDestroy = 5f;

    public void Initialize(float damage, float speed, Transform target)
    {
        this.damage = damage;
        this.speed = speed;
        this.target = target;
        Destroy(gameObject, timeBulletDestroy); // Destroy the bullet after the specified time if it doesn't hit anything
    }

    void Update()
    {
        if (target == null)
        {
            Destroy(gameObject); // Destroy the bullet if the target is destroyed
            return;
        }
        Vector3 direction = (target.position - transform.position).normalized;
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.CompareTag("Enemy"))
        {
            collision.GetComponent<EnemyHealth>().TakeDamage(damage);
            Destroy(gameObject); // Destroy the bullet after hitting an enemy
        }
    }
}
