// WeaponAttack.cs
using UnityEngine;

public class WeaponAttack : MonoBehaviour
{
    [Header("Оружие")]
    [SerializeField] private WeaponSO WeaponSO1;
    [SerializeField] private Transform firePoint;

    private PlayerContext _context; // ← Ссылка на контекст игрока
    private float timer = 0f;

    private void Awake()
    {
        if (WeaponSO1 == null || WeaponSO1.WeaponProjectilePrefab == null)
        {
            Debug.LogError($"Weapon {name} не настроен!");
            enabled = false;
            return;
        }

        // Ищем PlayerContext через PlayerAttack
        PlayerAttack playerAttack = GetComponentInParent<PlayerAttack>();
        if (playerAttack != null)
        {
            _context = playerAttack.GetContext(); // ← Нужно добавить метод GetContext в PlayerAttack
        }

        if (_context == null)
        {
            Debug.LogError("Не найден PlayerContext для оружия!");
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        timer += Time.fixedDeltaTime;
        if (timer >= WeaponSO1.FireRate)
        {
            Fire();
            timer = 0f;
        }
    }

    private void Fire()
    {
        int totalDamage = CalculateTotalDamage();

        Projectile proj = ProjectilePool.InstancePoolParent.GetProjectile(WeaponSO1.WeaponProjectilePrefab);
        if (proj == null) return;

        Vector2 spawnPos = firePoint != null ? firePoint.position : transform.position;
        Vector2? direction = null;
        Transform homingTarget = null;

        ProjectileSO so = WeaponSO1.WeaponProjectilePrefab.GetComponent<Projectile>().projectileSO1;
        float baseSpeed = so.ProjectileSpeed;
        float finalSpeed = GetFinalProjectileSpeed(baseSpeed);

        if (so.projectileType != ProjectileSO.EProjectileType.Homing)
            direction = firePoint != null ? firePoint.right : transform.right;
        else
            homingTarget = FindClosestEnemy();

        proj.Initialize(
            prefab: WeaponSO1.WeaponProjectilePrefab,
            data: so,
            spawnPosition: spawnPos,
            overrideDamage: totalDamage,
            overrideSpeed: finalSpeed,
            direction: direction,
            homingTarget: homingTarget
        );
    }

    private int CalculateTotalDamage()
    {
        if (_context == null) return WeaponSO1.WeaponDamage;
        return WeaponSO1.WeaponDamage + _context.GetTotalAttack();
    }

    private float GetFinalProjectileSpeed(float baseSpeed)
    {
        if (_context == null) return baseSpeed;
        return baseSpeed * _context.GetProjectileSpeedMultiplier();
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