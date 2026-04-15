using UnityEngine;

public class Shoot : MonoBehaviour
{
    public GameObject bulletPrefab;   // префаб пули
    public Transform firePoint;       // точка, откуда вылетает пуля (можно создать дочерний объект)
    public float bulletSpeed = 10f;

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
        // Определяем направление по масштабу персонажа (корневой объект)
        float direction = -Mathf.Sign(transform.root.localScale.x);

        // Если direction = 0 (стоим на месте), то по умолчанию вправо
        if (direction == 0) direction = 1f;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.SetDirection(direction);
            bulletScript.speed = bulletSpeed;
        }
    }
}

