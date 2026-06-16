using Unity.Mathematics;
using UnityEngine;

public class Shoot : BaseWeapon
{
    [Header("Shoot Settings")]
    public GameObject bulletPrefab;
    public GameObject ShootFX;

    [Header("Aim")]
    [SerializeField] private bool rotateToMouse = true;
    [SerializeField] private bool moveAroundOwner = true;
    [SerializeField] private Transform owner;
    [SerializeField] private float orbitRadius = 0.5f;

    [Header("Sprite")]
    [SerializeField] private SpriteRenderer weaponSprite;

    [Header("Recoil")]
    [SerializeField] private float recoilDistance = 0.12f;
    [SerializeField] private float recoilReturnSpeed = 14f;

    private float currentRecoil;

    protected override void Start()
    {
        if (bulletPrefab == null)
        return;
        bulletPrefab.SetActive(true);
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

        currentRecoil = Mathf.MoveTowards(
        currentRecoil,
        0f,
        recoilReturnSpeed * Time.deltaTime
);
    }

    public override void Attack()
    {
        if (bulletPrefab == null)
            return;

        if (firePoint == null)
            firePoint = transform;

        Vector2 baseDirection = GetShootDirection();

        float spreadAngle = 12f;
        int count = Mathf.Max(1, projectileCount);

        for (int i = 0; i < count; i++)
        {
            float angleOffset = 0f;

            if (count > 1)
            {
                angleOffset = Mathf.Lerp(
                    -spreadAngle,
                    spreadAngle,
                    (float)i / (count - 1)
                );
            }

            Vector2 direction = RotateVector(baseDirection, angleOffset);

            GameObject bullet = Instantiate(
                bulletPrefab,
                firePoint.position,
                Quaternion.identity
            );


            Bullet bulletScript = bullet.GetComponent<Bullet>();

            if (bulletScript != null)
            {
                bool isCritical = RollCritical();

                float finalDamage = GetDamage();

                if (isCritical)
                {
                    finalDamage *= GetCritMultiplier();
                }

                bulletScript.Initialize(
                    finalDamage,
                    GetProjectileSpeed(),
                    GetRange(),
                    direction,
                    projectilePierce,
                    isCritical,
                    projectileRicochet
                );
            }
        }

        if (weaponData != null)
            PlaySound(weaponData.attackSound);

        currentRecoil = recoilDistance;
        MarkAttackTime();

        //Спавн эффекта
        if (ShootFX == null)
            return;
        
        float destroyFXTime = ShootFX.GetComponent<ParticleSystem>().main.startLifetime.constantMax;
        Quaternion fxDirection = this.transform.rotation;

        GameObject ShootFX1 = Instantiate(
                ShootFX,
                firePoint.position,
                fxDirection,
                this.transform
                //direction
            );
        ShootFX1.SetActive(true);
        Destroy(ShootFX1, destroyFXTime);
        //Спавн эффекта    
    }

    private Vector2 RotateVector(Vector2 vector, float angle)
    {
        float radians = angle * Mathf.Deg2Rad;

        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        ).normalized;
    }

    private void MoveAroundOwner()
    {
        if (owner == null)
            return;

        Vector2 direction = GetMouseDirectionFromOwner();

        Vector2 recoilOffset = -direction * currentRecoil;

        transform.position = (Vector2)owner.position
            + direction * orbitRadius
            + recoilOffset;
    }

    private void RotateToMouse()
    {
        Vector2 direction = GetShootDirection();

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // �����:
        // ���� ������ ��������� ���������� ������� �����,
        // � Unity angle 0 ������� ������.
        // ������� ��������� 180 ��������.
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
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