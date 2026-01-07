using UnityEngine;

// Снаряд. Все данные — прямо здесь, на компоненте.
// Не нужно ScriptableObject — всё в инспекторе!
public class Projectile : MonoBehaviour
{
    [Header("Основные параметры")]
    public int baseDamage = 10;
    public float speed = 10f;
    public float lifetime = 5f; // авто-деактивация, если не попал

    [Header("Тип снаряда")]
    public ProjectileType projectileType = ProjectileType.Normal;

    public enum ProjectileType
    {
        Normal,
        Melee,
        Homing
    }

    // Для пула
    [HideInInspector] public GameObject sourcePrefab;

    // Внутренние
    private Transform homingTarget;
    private Rigidbody2D rb;
    private Collider2D col;
    private int damageOverride = -1; // если нужно изменить урон при выстреле

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    // Инициализация при каждом использовании (из пула или Instantiate)
    public void Initialize(
        GameObject prefab,
        Vector2 spawnPosition,
        int? overrideDamage = null,
        float? overrideSpeed = null,
        Vector2? direction = null,
        Transform homingTarget = null
    )
    {
        sourcePrefab = prefab;
        damageOverride = overrideDamage ?? -1;
        this.homingTarget = homingTarget;

        transform.position = spawnPosition;
        gameObject.SetActive(true);

        // Сброс скорости
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.linearVelocity = Vector2.zero;

        if (projectileType == ProjectileType.Melee)
        {
            col.enabled = true;
            Invoke(nameof(Deactivate), 0.1f);
        }
        else
        {
            col.enabled = true;
            float finalSpeed = overrideSpeed ?? speed;
            if (direction.HasValue)
                rb.linearVelocity = direction.Value.normalized * finalSpeed;
            else
                rb.linearVelocity = transform.right * finalSpeed;

            // Автодеактивация по таймеру (защита от зависших снарядов)
            CancelInvoke(nameof(Deactivate));
            Invoke(nameof(Deactivate), lifetime);
        }
    }

    public GameObject GetSourcePrefab() => sourcePrefab;
    public int GetDamage() => damageOverride >= 0 ? damageOverride : baseDamage;

    private void Deactivate()
    {
        CancelInvoke();
        gameObject.SetActive(false);
        ProjectilePool pool = GetComponentInParent<ProjectilePool>();
        if (pool != null)
            pool.ReturnProjectile(this);
        else
            Debug.LogWarning("Снаряд не может найти пул для возврата!");
    }

    private void Update()
    {
        if (projectileType == ProjectileType.Homing && homingTarget != null)
        {
            Vector2 dir = (homingTarget.position - transform.position).normalized;
            rb.linearVelocity = dir * speed;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            Vector2 hitDir = rb.linearVelocity.normalized;
            damageable.TakeDamage(GetDamage(), hitDir, gameObject);
            Deactivate();
        }
    }
}