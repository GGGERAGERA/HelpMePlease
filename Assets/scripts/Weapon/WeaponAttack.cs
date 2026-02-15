using UnityEngine;

public class WeaponAttack : MonoBehaviour
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private int poolSize = 10;
    [SerializeField] private float fireRate = 0.25f;

    private ProjectilePool pool;
    private float timer;
    private Vector2 direction = Vector2.left;
    private PlayerMovement playerMovement;

    void Awake()
    {
        playerMovement = GetComponentInParent<PlayerMovement>();
        var poolParent = new GameObject($"Pool_{name}");
        poolParent.transform.SetParent(ProjectilePoolManager.Instance.transform);
        pool = new ProjectilePool(projectilePrefab.gameObject, poolSize, poolParent.transform);
    }

    void Update()
    {
        Vector2 inputDirection = playerMovement.GetMovementDirection();

        if (inputDirection == Vector2.zero)
            inputDirection = direction;

        direction = inputDirection;

        timer += Time.deltaTime;
        if (timer >= fireRate)
        {
            Shoot();
            timer = 0f;
        }

    }

    void Shoot()
    {
        int finalDamage = 25; // ← ПОТОМ ЗАМЕНИШЬ НА НАСТОЯЩИЙ РАСЧЁТ
        Projectile proj = pool.GetProjectile();

        proj.Launch(firePoint.position, direction, finalDamage);
    }
}