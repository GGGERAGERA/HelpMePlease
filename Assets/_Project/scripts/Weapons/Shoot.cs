using UnityEngine;

public class Shoot : MonoBehaviour
{
    [Header("Weapon Settings")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float bulletSpeed = 10f;
    public float shootInterval = 0.5f;
    public float searchRadius = 20f;

    private float nextShootTime;

    void Start()
    {
        nextShootTime = Time.time;
    }

    void Update()
    {
        if (Time.time >= nextShootTime)
        {
            ShootBullet();
            nextShootTime = Time.time + shootInterval;
        }
    }

    void ShootBullet()
    {
        if (firePoint == null || bulletPrefab == null) return;

        Transform nearestEnemy = FindNearestEnemy();
        if (nearestEnemy == null) return;

        Vector2 direction = (nearestEnemy.position - firePoint.position).normalized;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            int damage = PlayerStats.Instance != null ? PlayerStats.Instance.GetDamage() : 10;
            bulletScript.Initialize(damage, bulletSpeed, direction);
        }
    }

    Transform FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform nearest = null;
        float minDist = Mathf.Infinity;

        foreach (GameObject enemy in enemies)
        {
            float dist = Vector2.Distance(firePoint.position, enemy.transform.position);
            if (dist < minDist && dist <= searchRadius)
            {
                minDist = dist;
                nearest = enemy.transform;
            }
        }
        return nearest;
    }
}