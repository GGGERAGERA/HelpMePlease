// WeaponAttack.cs
using UnityEngine;

public class WeaponAttack : MonoBehaviour
{
    [Header("Оружие")]
    [SerializeField] private WeaponSO weaponData;

    private ProjectileSO ProjectileSO1;
    private ProjectileSO.EProjectileType ProjectileType1;

    //private WeaponSO.E weaponData;

    [Header("Точка выстрела")]
    [SerializeField] private Transform firePoint;

    private float timer = 0f;
    private Transform playerTransform;

    private void Awake()
    {
        playerTransform = transform.parent;

        if (weaponData == null)
        {
            Debug.LogError("WeaponSO не задан на " + name, this);
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;

        float timeBetweenShots = 1f / weaponData.FireRate;
        if (timer >= timeBetweenShots)
        {
            Fire();
            timer = 0f;
        }
    }

    private void Fire()
    {
        // Получаем итоговый урон
        int totalDamage = GetPlayerTotalAttack();

        // Определяем точку и направление
        Vector2 spawnPos = (firePoint != null) ? firePoint.position : transform.position;
        Vector2 direction = Vector2.zero;

        if (ProjectileType1 != ProjectileSO.EProjectileType.Melee)
        {
            direction = (playerTransform != null) ? playerTransform.right : transform.right;
        }

        // Получаем снаряд из пула
        Projectile projectile = ProjectilePool.Instance.GetProjectile(ProjectileType1);

        if (projectile == null)
        {
            Debug.LogError("Не удалось получить снаряд!");
            return;
        }

        // Инициализируем
        projectile.Initialize(
            damage: totalDamage,
            spawnPosition: spawnPos,
            type: ProjectileType1,
            direction: (ProjectileType1 != ProjectileSO.EProjectileType.Melee) ? direction : null,
            homingTarget: (ProjectileType1 == ProjectileSO.EProjectileType.Homing) ? FindClosestEnemy() : null
        );
    }

    private int GetPlayerTotalAttack()
    {
        PlayerStatsSO playerStats = playerTransform?.GetComponent<PlayerStatsSO>();
        if (playerStats != null)
        {
            return playerStats.power + weaponData.WeaponDamage;
        }
        return weaponData.WeaponDamage;
    }

    private Transform FindClosestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemies.Length == 0) return null;

        Transform closest = null;
        float minDist = Mathf.Infinity;

        foreach (GameObject enemy in enemies)
        {
            float dist = Vector2.Distance(transform.position, enemy.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = enemy.transform;
            }
        }
        return closest;
    }
}