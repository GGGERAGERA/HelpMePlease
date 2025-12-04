using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

public class Projectile : MonoBehaviour
{
    [Header("Параметры")]
    [HideInInspector] public GameObject sourcePrefab; // ← храним префаб, из которого создан
    public ProjectileSO projectileSO1;
    private Transform target; // Для Homing
    private Rigidbody2D rb;
    private Collider2D col;
    private int damageOverride = -1; // если > -1 — используем его вместо projectileSO1.ProjectileDamage

        // Инициализирует снаряд. Вызывать ОБЯЗАТЕЛЬНО после получения из пула!
private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    /// Инициализация снаряда. Обязательно вызывать при получении из пула или Instantiate.
    public void Initialize(
        GameObject prefab,
        ProjectileSO data,
        Vector2 spawnPosition,
        int? overrideDamage = null,
        float? overrideSpeed = null,      // ← новое!
        Vector2? direction = null,
        Transform homingTarget = null
    )
    {
        sourcePrefab = prefab; // ← ЗАПОМИНАЕМ префаб!
        projectileSO1 = data;
        damageOverride = overrideDamage ?? -1;

        transform.position = spawnPosition;
        gameObject.SetActive(true);

        if (data.projectileType == ProjectileSO.EProjectileType.Melee)
        {
            col.enabled = true;
            Invoke(nameof(Deactivate), 0.1f);
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

    /// Вспомогательный метод для пула: по какому префабу был создан?
    public GameObject GetSourcePrefab()
    {
        return sourcePrefab;
    }

    /// Получить урон снаряда (с учётом override)
    public int GetDamage()
    {
        if (damageOverride >= 0)
            return damageOverride;
        return projectileSO1?.ProjectileDamage ?? 0;
    }

    private void Deactivate()
    {
        gameObject.SetActive(false);
        ProjectilePool.InstancePoolParent.ReturnProjectile(this);
    }

    private void Update()
    {
        if (projectileSO1?.projectileType == ProjectileSO.EProjectileType.Homing && target != null)
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
            Vector2 hitDir = rb.linearVelocity.normalized;
            damageable.TakeDamage(GetDamage(), hitDir, gameObject);
            Deactivate();
        }
    }
}