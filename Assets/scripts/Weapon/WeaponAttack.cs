using UnityEngine;

public class WeaponAttack : MonoBehaviour
{
    public GameObject projectileToShoot; // ← префаб снаряда (с компонентом Projectile!)
    public Transform firePoint;
    public float fireRate = 0.25f;

    private ProjectilePool _myPool;
    private float _timer;

    private void Start()
    {
        // Ищем пул у игрока (в родительской иерархии)
        _myPool = GetComponentInParent<ProjectilePool>();
        if (_myPool == null)
        {
            GameObject player = transform.root.gameObject;
            _myPool = player.GetComponent<ProjectilePool>() ?? player.AddComponent<ProjectilePool>();
        }

        if (projectileToShoot != null)
            _myPool.AddProjectileType(projectileToShoot);
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= fireRate)
        {
            Fire();
            _timer = 0f;
        }
    }

    private void Fire()
    {
        if (projectileToShoot == null || firePoint == null || _myPool == null) return;

        Projectile proj = _myPool.GetProjectile(projectileToShoot);
        if (proj != null)
        {
            // Тут можешь брать урон из PlayerStats, Progress и т.д.
            int finalDamage = CalculateFinalDamage();

            Vector2 direction = FindClosestEnemyDirection() ?? firePoint.right;

            proj.Initialize(
                prefab: projectileToShoot,
                spawnPosition: firePoint.position,
                overrideDamage: finalDamage,
                direction: direction
            );
        }
    }

    private int CalculateFinalDamage()
    {
        // Пример: базовый урон снаряда + бонус от игрока
        int baseDmg = projectileToShoot.GetComponent<Projectile>().baseDamage;

        // Если у тебя есть PlayerContext или статы — бери оттуда:
        // PlayerAttack player = GetComponentInParent<PlayerAttack>();
        // int bonus = player?.GetContext()?.playerStats?.damageBonus ?? 0;

        return baseDmg; // пока без бонусов
    }

    private Vector2? FindClosestEnemyDirection()
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
        return (closest.position - firePoint.position).normalized;
    }
}