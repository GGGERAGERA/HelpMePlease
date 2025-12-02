using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Projectile : MonoBehaviour
{
    [Header("Projectile")]
    [Tooltip("Ссылка на ScriptableObject с параметрами projectile")]
    public ProjectileSO projectileSO1;
    private ProjectileSO.EProjectileType projectileType1;
    public int Damage1 { get; private set; }
    //public ProjectileType Type; // enum: Direct, Homing, Melee
    [SerializeField] private float Speed1 = 10f;
    [SerializeField] private float Lifetime1 = 5f;

    //private ProjectileSO.EProjectileType projectileType; // → выпадающий список из 4 вариантов!

    private Transform target; // для Homing
    private Rigidbody2D rb;

    private void Awake()
    {   

        projectileType1 = projectileSO1.projectileType;
        
        rb = GetComponent<Rigidbody2D>();
        Destroy(gameObject, Lifetime1); // самоликвидация
    }

    public void Initialize(int damage, ProjectileSO.EProjectileType projectileType2, Vector2 direction, Transform homingTarget = null)
    {
        //public EProjectileType projectileType = EProjectileType.Homing;
        Damage1 = damage;
        projectileType1 = projectileType2;

        if (projectileType1 == ProjectileSO.EProjectileType.Homing && homingTarget != null)
        {
            this.target = homingTarget;
        }
        else
        {
            rb.linearVelocity = direction * Speed1;
        }
    }

    private void Update()
    {
        if (projectileType1 == ProjectileSO.EProjectileType.Homing && target != null)
        {
            Vector2 dir = (target.position - transform.position).normalized;
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, dir * Speed1, Time.deltaTime * 5f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            // Наносим урон ТОЛЬКО один раз
            damageable.TakeDamage(Damage1, rb.linearVelocity, gameObject);
            ReturnToPool(); // или Destroy(gameObject);
        }
    }

    private void ReturnToPool()
    {
        // ObjectPool.Return(this);
        Destroy(gameObject);
    }
}