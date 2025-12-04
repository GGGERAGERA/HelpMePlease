// WeaponAttack.cs
using UnityEngine;

public class WeaponAttack : MonoBehaviour
{
    [Header("Оружие")]
    [SerializeField] private GameObject weaponProjectile1;
    [SerializeField] private WeaponSO WeaponSO1;
    [SerializeField] private Transform firePoint;
    private GameObject projectilePrefab => WeaponSO1.WeaponProjectilePrefab;
    private Transform playerTransform;

    private float timer = 0f;

    private void Awake()
    {
        playerTransform = transform.parent;
        if (WeaponSO1 == null || projectilePrefab == null)
        {
            Debug.LogError($"Weapon {name} не настроен!");
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        //float finalFireRate = GetFinalFireRate(WeaponSO1.FireRate);
        timer += Time.fixedDeltaTime;

        if (timer >= WeaponSO1.FireRate)
        {
            Fire();
            timer = 0f;
        }
    }

    private void Fire()
    {
        // Рассчитываем урон
        int totalDamage = CalculateTotalDamage();


        // Получаем снаряд из пула (по префабу!)
        Projectile proj = ProjectilePool.InstancePoolParent.GetProjectile(projectilePrefab);
        if (proj == null) return;

        // Настраиваем
        Vector2 spawnPos = firePoint != null ? firePoint.position : transform.position;
        Vector2? direction = null;
        Transform homingTarget = null;

        ProjectileSO so = projectilePrefab.GetComponent<Projectile>().projectileSO1;
        float baseSpeed = so.ProjectileSpeed;
        float finalSpeed = GetFinalProjectileSpeed(baseSpeed);

        if (so.projectileType != ProjectileSO.EProjectileType.Homing)
            direction = firePoint != null ? firePoint.right : transform.right;
        else
            homingTarget = FindClosestEnemy();

        // Инициализируем снаряд
        proj.Initialize(
            prefab: projectilePrefab,
             so,
            spawnPosition: spawnPos,
            overrideDamage: totalDamage,
            overrideSpeed: finalSpeed,        // ← передаём!
            direction: direction,
            homingTarget: homingTarget
        );
    }

    private int CalculateTotalDamage()
    {
        int baseDamage = WeaponSO1.WeaponDamage;
        int bonus = 0;

        // Получаем статы игрока
        if (playerTransform != null)
        {
            PlayerStatsSO stats = playerTransform.GetComponent<PlayerStatsSO>();
            if (stats != null) bonus += stats.power;

            PlayerBuffsSO buffs = playerTransform.GetComponent<PlayerBuffsSO>();
            if (buffs != null) bonus += buffs.PlayerBuffsPower;
        }

        return baseDamage + bonus;
    }

    // Рассчитывает итоговую скорость снаряда с учётом баффов
private float GetFinalProjectileSpeed(float baseSpeed)
{
    float multiplier = 1f;

    if (playerTransform != null)
    {
        PlayerBuffsSO buffs = playerTransform.GetComponent<PlayerBuffsSO>();
        if (buffs != null)
            multiplier += buffs.PlayerBuffsProjectileSpeed; // например, +0.5f = +50%
    }

    return baseSpeed * multiplier;
}

// Рассчитывает итоговую перезарядку с учётом баффов
private float GetFinalFireRate(float baseFireRate)
{
    float multiplier = 1f;

    if (playerTransform != null)
    {
        PlayerBuffsSO buffs = playerTransform.GetComponent<PlayerBuffsSO>();
        if (buffs != null)
            multiplier -= buffs.PlayerBuffsReload; // например, +0.2f = -20% времени
    }

    return baseFireRate * Mathf.Max(0.1f, multiplier); // минимум 0.1 сек
}

    private Transform FindClosestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemies.Length == 0) return null;

        Transform closest = null;
        float minDist = Mathf.Infinity;
        foreach (var enemy in enemies)
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