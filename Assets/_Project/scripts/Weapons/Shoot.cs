using UnityEngine;

public class Shoot : BaseWeapon
{
    [Header("Shoot Settings")]
    public GameObject bulletPrefab;

    [Header("Aim")]
    [SerializeField] private bool rotateToMouse = true;
    [SerializeField] private bool moveAroundOwner = true;
    [SerializeField] private Transform owner;
    [SerializeField] private float orbitRadius = 0.5f;

    [Header("Sprite")]
    [SerializeField] private SpriteRenderer weaponSprite;

    protected override void Start()
    {
        base.Start();

        if (weaponSprite == null)
            weaponSprite = GetComponentInChildren<SpriteRenderer>();

        if (owner == null && transform.parent != null)
            owner = transform.parent;
    }

    protected override void Update()
    {
        base.Update();

        if (Time.timeScale == 0f)
            return;

        if (moveAroundOwner)
            MoveAroundOwner();

        if (rotateToMouse)
            RotateToMouse();

        if (Input.GetMouseButton(0) && CanAttack())
        {
            Attack();
        }
    }

    public override void Attack()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("Shoot: bulletPrefab is not assigned.");
            return;
        }

        if (firePoint == null)
            firePoint = transform;

        Vector2 direction = GetShootDirection();

        GameObject bullet = Instantiate(
            bulletPrefab,
            firePoint.position,
            Quaternion.identity
        );

        Bullet bulletScript = bullet.GetComponent<Bullet>();

        if (bulletScript != null)
        {
            bulletScript.Initialize(GetDamage(), GetRange(), direction);
        }

        if (weaponData != null)
        {
            PlaySound(weaponData.attackSound);
        }

        MarkAttackTime();
    }

    private void MoveAroundOwner()
    {
        if (owner == null)
            return;

        Vector2 direction = GetMouseDirectionFromOwner();

        transform.position = (Vector2)owner.position + direction * orbitRadius;
    }

    private void RotateToMouse()
    {
        Vector2 direction = GetShootDirection();

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // ВАЖНО:
        // твой спрайт пистолета изначально смотрит ВЛЕВО,
        // а Unity angle 0 смотрит ВПРАВО.
        // Поэтому добавляем 180 градусов.
        transform.rotation = Quaternion.Euler(0f, 0f, angle + 180f);
    }

    private Vector2 GetMouseDirectionFromOwner()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0f;

        return ((Vector2)mousePosition - (Vector2)owner.position).normalized;
    }

    private Vector2 GetShootDirection()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0f;

        return ((Vector2)mousePosition - (Vector2)firePoint.position).normalized;
    }
}