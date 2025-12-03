using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

public class Projectile : MonoBehaviour
{
    [Header("Параметры")]
    public int Damage { get; private set; }
    public ProjectileSO.EProjectileType Type { get; private set; } // ← ВАЖНО: тип хранится здесь!
    public ProjectileSO projectileSO1;
    public float Speed = 10f;
    public float Lifetime = 5f;

    private Transform target; // Для Homing
    private Rigidbody2D rb;
    private Collider2D col;
    public int CurrentPrefabsForSpawn = 5;
    private void Awake()
    {
        CurrentPrefabsForSpawn = projectileSO1.ProjectileSpawnPoolCount;
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        //Destroy(gameObject, Lifetime); // Самоликвидация
    }

    /// <summary>
    /// Инициализирует снаряд. Вызывать ОБЯЗАТЕЛЬНО после получения из пула.
    /// </summary>
    public void Initialize(int damage, Vector2 spawnPosition, ProjectileSO.EProjectileType type, Vector2? direction = null, Transform homingTarget = null)
    {
        Damage = damage;
        Type = type;
        transform.position = spawnPosition;
        gameObject.SetActive(true);

        // Настройка поведения
        if (type == ProjectileSO.EProjectileType.Melee)
        {
            // Melee: сразу активируем коллайдер, уничтожаем через 0.1 сек
            col.enabled = true;
            Invoke(nameof(Deactivate), 0.1f);
        }
        else
        {
            // Direct или Homing: включаем физику
            col.enabled = true;
            //rb.isKinematic = false;
            rb.bodyType = RigidbodyType2D.Dynamic;
            //rb.velocity = Vector2.zero;
            rb.linearVelocity = Vector2.zero;

            if (direction.HasValue)
            {
                if (type == ProjectileSO.EProjectileType.Direct)
                {
                    rb.linearVelocity = direction.Value * Speed;
                }
                else if (type == ProjectileSO.EProjectileType.Homing)
                {
                    rb.linearVelocity = direction.Value * Speed;
                    this.target = homingTarget;
                }
            }
        }
    }

    private void Update()
    {
        if (Type == ProjectileSO.EProjectileType.Homing && target != null)
        {
            Vector2 dir = (target.position - transform.position).normalized;
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, dir * Speed, Time.deltaTime * 8f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            // Наносим урон
            Vector2 hitDirection = rb.linearVelocity.normalized;
            damageable.TakeDamage(Damage, hitDirection, gameObject);

            // Возвращаем в пул
            ProjectilePool.InstancePoolParent.ReturnProjectile2(this);
        }
    }

    private void Deactivate()
    {
        ProjectilePool.InstancePoolParent.ReturnProjectile2(this);
    }
}