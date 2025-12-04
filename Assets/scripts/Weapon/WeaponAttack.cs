// WeaponAttack.cs
using UnityEngine;

public class WeaponAttack : MonoBehaviour
{
    [Header("Оружие")]
    [SerializeField] private GameObject weaponProjectile1;
    [SerializeField] private WeaponSO weaponData;

    private ProjectileSO ProjectileSO1;
    private ProjectileSO.EProjectileType ProjectileType1;

    public GameObject projectileContainer;
    

    //private WeaponSO.E weaponData;

    [Header("Точка выстрела")]
    [SerializeField] private Transform firePoint;

    private float timer = 0f;
    public Transform playerTransform;

    private void Awake()
    {
        //playerTransform = transform.parent;
        playerTransform = transform.parent;
        weaponProjectile1 = weaponData.WeaponProjectilePrefab;
        
        if (weaponData == null)
        {
            Debug.LogError("WeaponSO не задан на " + name, this);
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        timer += Time.fixedDeltaTime;

        float timeBetweenShots = weaponData.FireRate;
        if (timer >= timeBetweenShots)
        {
            Fire();
            timer = 0f;
        }
    }


private void Fire()
{
    // Получаем данные снаряда
    ProjectileSO projectileData = weaponData.WeaponProjectilePrefab.GetComponent<Projectile>().projectileSO1;

    // Получаем урон
    int totalDamage = CalculateTotalDamage();

    // Получаем снаряд из пула
    Projectile proj = ProjectilePool.InstancePoolParent.GetProjectile(projectileData.projectileType);
    if (proj == null) return;

    // Инициализируем снаряд
    Vector2 spawnPos = firePoint.position;
    Vector2? direction = firePoint.right; // или null, если Homing
    Transform homingTarget = null;

    if (projectileData.projectileType == ProjectileSO.EProjectileType.Homing)
        homingTarget = FindClosestEnemy();

    proj.Initialize(
        data: projectileData,
        spawnPosition: spawnPos,
        direction: direction,
        homingTarget: homingTarget
    );

    // Передаём урон в снаряд (если нужно)
    proj.SetDamage(totalDamage);
}

    private int CalculateTotalDamage()
{
    PlayerStatsSO playerStats = playerTransform?.GetComponent<PlayerStatsSO>();
    int baseDamage = weaponData.WeaponDamage;
    int bonusDamage = 0;

    if (playerStats != null)
        bonusDamage = playerStats.power;

    // Можно добавить бонусы от баффов
    PlayerBuffsSO buffs = playerTransform?.GetComponent<PlayerBuffsSO>();
    if (buffs != null)
        bonusDamage += buffs.PlayerBuffsPower;

    return baseDamage + bonusDamage;
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