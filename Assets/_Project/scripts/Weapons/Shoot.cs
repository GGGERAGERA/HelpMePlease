using UnityEngine;

public class Shoot : MonoBehaviour
{
    [Header("Weapon Data")]
    public WeaponData weaponData; // ссылка на ScriptableObject с параметрами оружия

    [Header("References")]
    public Transform firePoint;    // точка вылета пули (лучше назначить в префабе)
    public GameObject bulletPrefab; // префаб пули (можно указать в WeaponData, но для простоты оставим)

    private float lastShootTime;

    void Start()
    {

        if (weaponData == null)
            Debug.LogError("Shoot: WeaponData not assigned!", this);
    }

    void Update()
    {
        // Не стреляем на паузе
        if (Time.timeScale == 0f) return;
        if (weaponData == null) return;


        if(Input.GetMouseButtonDown(0))
        {
            if (Time.time >= lastShootTime + weaponData.fireRate)
            {
                ShootBullet();
                lastShootTime = Time.time;
            }
        }
    }

    void ShootBullet()
    {
        if (firePoint == null)
        {
           
            return;
        }
        if (bulletPrefab == null)
        {
           
            return;
        }

        Vector2 direction = GetShootDirection();

        // Создаём пулю
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            // Передаём урон из WeaponData
            bulletScript.Initialize(weaponData.damage, weaponData.range, direction);
        }
        else
        {
            Debug.LogError("Bullet prefab must have Bullet component!");
        }
    }


    Vector2 GetShootDirection()
    {
        // Получаем позицию мыши в мировых координатах
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f; // обнуляем z, так как мы работаем в 2D
        return (mousePos - firePoint.position).normalized;
    }
}