// WeaponAttack.cs
using UnityEngine;

public class WeaponAttack : MonoBehaviour
{
    [Header("Оружие")]
    [SerializeField] private GameObject weaponProjectile1;
    [SerializeField] private WeaponSO WeaponSO1;
    [SerializeField] private Transform firePoint;

    [Header("Сам игрок для первого (дефолтного) оружия")]
    public GameObject PlayerParent;

    private float timer = 0f;

    private void Awake()
    {

        /*//ВАЖНО! Если игрока в родителях не нашли, но хотим чтоб оружие стреляло- пишем это!
        if (PlayerParent == null)
        PlayerParent = this.gameObject;
        //playerTransform = transform.parent;
        if (WeaponSO1 == null || projectilePrefab == null)
        {
            Debug.LogError($"Weapon {name} не настроен!");
            enabled = false;
            return;
        }*/

        if (WeaponSO1 == null || WeaponSO1.WeaponProjectilePrefab == null)
        {
            Debug.LogError($"Weapon {name} не настроен!");
            enabled = false;
            return;
        }

        // Добавляем себя в список игрока (если игрок есть)
        if (PlayerParent != null)
        {
            PlayerAttack playerAttack = PlayerParent.GetComponent<PlayerAttack>();
            if (playerAttack != null)
                playerAttack.AddWeapon(gameObject); // ← добавляем себя в список
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
        int baseDamage = WeaponSO1.WeaponDamage;
        int bonus = 0;

        if (PlayerParent != null)
        {
            PlayerStatsSO stats = PlayerParent.GetComponent<PlayerStatsSO>();
            if (stats != null) bonus += stats.power;

            PlayerBuffsSO buffs = PlayerParent.GetComponent<PlayerBuffsSO>();
            if (buffs != null) bonus += buffs.PlayerBuffsPower;
        }

        return baseDamage + bonus;
    }

    // Рассчитывает итоговую скорость снаряда с учётом баффов
private float GetFinalProjectileSpeed(float baseSpeed)
{
    float multiplier = 1f;

    if (PlayerParent != null && PlayerParent.GetComponent<PlayerStatsSO>() != null)
    {
        PlayerBuffsSO buffs = PlayerParent.GetComponent<PlayerBuffsSO>();
        if (buffs != null)
            multiplier += buffs.PlayerBuffsProjectileSpeed; // например, +0.5f = +50%
    }

    return baseSpeed * multiplier;
}

// Рассчитывает итоговую перезарядку с учётом баффов
private float GetFinalFireRate(float baseFireRate)
{
    float multiplier = 1f;

    if (PlayerParent != null && PlayerParent.GetComponent<PlayerStatsSO>() != null)
    {
        PlayerBuffsSO buffs = PlayerParent.GetComponent<PlayerBuffsSO>();
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