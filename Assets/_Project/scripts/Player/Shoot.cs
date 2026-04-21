using UnityEngine;

public class Shoot : MonoBehaviour
{
    public GameObject bulletPrefab;   // префаб пули
    public Transform firePoint;       // точка, откуда вылетает пуля (можно создать дочерний объект)
    public float bulletSpeed = 10f;
    public float bulletDamage = 20f;
    public float shootInterval = 2f;   // Интервал между выстрелами
    public float searchRadius = 20f; // радиус поиска врагов

    void Start()
    {
        // Начинаем стрельбу с задержкой 1 секунда, затем каждые shootInterval секунд
        InvokeRepeating("ShootBullet", 1f, shootInterval);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // левая кнопка мыши
        {
            ShootBullet();
        }
        // Или можно по кнопке: if (Input.GetKeyDown(KeyCode.Space))
    }

    void ShootBullet()
    {
        if (firePoint == null || bulletPrefab == null) return;
        Transform nearestEnemy = FindNearestEnemy();
        if (nearestEnemy == null) return;

        Vector2 direction = (nearestEnemy.position - firePoint.position).normalized;
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        bullet.GetComponent<Bullet>().Initialize(bulletDamage, bulletSpeed, direction);
    }

    Transform FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform nearest = null;
        float minDist = Mathf.Infinity;

        foreach (GameObject enemy in enemies)
        {
            float dist = Vector3.Distance(firePoint.position, enemy.transform.position);
            if (dist < minDist && dist <= searchRadius)
            {
                minDist = dist;
                nearest = enemy.transform;
            }
        }
        return nearest;
    }
}

