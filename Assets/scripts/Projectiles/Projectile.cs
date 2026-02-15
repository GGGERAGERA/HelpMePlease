using UnityEngine;

public class Projectile : MonoBehaviour
{
    public int damage = 10;
    public float speed = 10f;
    public float lifetime = 2f;

    public ProjectilePool pool;
    public Rigidbody2D rb;
    private float timer;
    private Vector2 direction;

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer > lifetime)
        {
            ReturnToPool();
            timer = 0;
        }
    }
    public void Initialize(ProjectilePool poolRef)
    {
        pool = poolRef;
        rb = GetComponent<Rigidbody2D>();
        gameObject.SetActive(false);
    }

    public void Launch(Vector2 position, Vector2 direction, int finalDamage)
    {
        transform.position = position;
        damage = finalDamage; // ← УРОН ПЕРЕДАЁТСЯ ВМЕСТЕ С ВЫСТРЕЛОМ
        timer = 0;
        gameObject.SetActive(true);
        rb.linearVelocity = direction * speed;
        this.direction = direction;
    }

    void ReturnToPool()
    {
        gameObject.SetActive(false);
        pool?.ReturnProjectile(this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        IDamageable target = other.GetComponent<IDamageable>();
        if (target != null)
        {
            target.TakeDamage(damage, rb.linearVelocity.normalized, gameObject);
            ReturnToPool();
        }
    }
}