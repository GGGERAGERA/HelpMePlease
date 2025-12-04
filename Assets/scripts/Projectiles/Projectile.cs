using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

public class Projectile : MonoBehaviour
{
    [Header("Параметры")]
    public ProjectileSO projectileSO1;
    private Transform target; // Для Homing
    private Rigidbody2D rb;
    private Collider2D col;

        // Инициализирует снаряд. Вызывать ОБЯЗАТЕЛЬНО после получения из пула!
    public void Initialize(ProjectileSO data, Vector2 spawnPosition, Vector2? direction = null, Transform homingTarget = null)
    {
        projectileSO1 = data; // Сохраняем ссылку на данные
        int totalDamage = 0;

        transform.position = spawnPosition;
        gameObject.SetActive(true);
        // Настройка поведения
        if (data.projectileType == ProjectileSO.EProjectileType.Melee)
        {
            col.enabled = true;
            Invoke(nameof(Deactivate), 0.1f); // Самоликвидация через 0.1 сек
        }
        else
        {
            col.enabled = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;

            if (direction.HasValue)
                rb.linearVelocity = direction.Value.normalized * data.ProjectileSpeed;
            else
                rb.linearVelocity = transform.right * data.ProjectileSpeed;

            if (data.projectileType == ProjectileSO.EProjectileType.Homing && homingTarget != null)
                target = homingTarget;
        }
    } 

        public void SetDamage(int damage)
    {
        damage = damage;
    }
    
    private void Deactivate()
    {
        gameObject.SetActive(false);
        // Если нужно — вернуть в пул
        ProjectilePool.InstancePoolParent.ReturnProjectile(this);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        //Destroy(gameObject, Lifetime); // Самоликвидация
    }

    private void Update()
    {
        if (projectileSO1.projectileType == ProjectileSO.EProjectileType.Homing && target != null)
        {
            Vector2 direction = (target.position - transform.position).normalized;
            rb.linearVelocity = direction * projectileSO1.ProjectileSpeed;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            // Наносим урон
            Vector2 hitDirection = rb.linearVelocity.normalized;
            damageable.TakeDamage(projectileSO1.ProjectileDamage, hitDirection, gameObject);

            // Возвращаем в пул
            ProjectilePool.InstancePoolParent.ReturnProjectile(this);
        }
    }

}