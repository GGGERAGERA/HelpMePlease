using UnityEngine;

public class Shoot : BaseWeapon
{
    [Header("Shoot Settings")]
    public GameObject bulletPrefab;
    public GameObject ShootFX;
    [SerializeField] private float baseKnockbackForce = 4f;
    [SerializeField] private float spreadAngle = 12f;

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
        base.Start();

        if (bulletPrefab != null)
            bulletPrefab.SetActive(true);

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
            Attack();

        currentRecoil = Mathf.MoveTowards(
            currentRecoil,
            0f,
            recoilReturnSpeed * Time.deltaTime
        );
    }

    public override void Attack()
    {
        if (!CanAttack())
            return;

        if (bulletPrefab == null)
            return;

        if (firePoint == null)
            firePoint = transform;

        Vector2 baseDirection = GetShootDirection();
        baseDirection = ApplyAccuracyPenalty(baseDirection);

        FireShotGroup(baseDirection, GetProjectileCount());

        if (weaponData != null)
            PlaySound(weaponData.attackSound);

        currentRecoil = recoilDistance;

        SpawnShootFx();

        MarkAttackTime();
    }

    private void FireShotGroup(Vector2 baseDirection, int count)
    {
        int safeCount = Mathf.Max(1, count);

        for (int i = 0; i < safeCount; i++)
        {
            float angleOffset = GetSpreadOffset(i, safeCount);
            Vector2 direction = RotateVector(baseDirection, angleOffset);

            SpawnSingleProjectile(direction);
        }
    }

    private float GetSpreadOffset(int index, int count)
    {
        if (count <= 1)
            return 0f;

        return Mathf.Lerp(
            -spreadAngle,
            spreadAngle,
            (float)index / (count - 1)
        );
    }

    private void SpawnSingleProjectile(Vector2 direction)
    {
        if (!IsValidVector(direction) || direction.sqrMagnitude < 0.001f)
            return;

        GameObject projectileObject = Instantiate(
            bulletPrefab,
            firePoint.position,
            Quaternion.identity
        );

        IWeaponProjectile projectile =
            projectileObject.GetComponent<IWeaponProjectile>();

        if (projectile == null)
        {
            Debug.LogWarning("Shoot: spawned projectile has no IWeaponProjectile component.");
            Destroy(projectileObject);
            return;
        }

        bool isCritical = RollCritical();

        float finalDamage = GetDamage();

        if (isCritical)
            finalDamage *= GetCritMultiplier();

        PlayerCombatModifiers modifiers = GetComponentInParent<PlayerCombatModifiers>();

        projectile.Initialize(
            finalDamage,
            GetProjectileSpeed(),
            GetRange(),
            direction,
            GetProjectilePierce(),
            isCritical,
            GetProjectileRicochet(),
            GetKnockbackForce(baseKnockbackForce)
        );

        ProjectileCombatContext context =
            projectileObject.GetComponent<ProjectileCombatContext>();

        if (context == null)
            context = projectileObject.AddComponent<ProjectileCombatContext>();

        context.Initialize(modifiers);
    }

    private void SpawnShootFx()
    {
        if (ShootFX == null)
            return;

        ParticleSystem particleSystem = ShootFX.GetComponent<ParticleSystem>();

        float destroyTime = particleSystem != null
            ? particleSystem.main.startLifetime.constantMax
            : 1f;

        GameObject shootFx = Instantiate(
            ShootFX,
            firePoint.position,
            transform.rotation,
            transform
        );

        shootFx.SetActive(true);
        Destroy(shootFx, destroyTime);
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

        transform.position =
            (Vector2)owner.position +
            direction * orbitRadius +
            recoilOffset;
    }

    private void RotateToMouse()
    {
        Vector2 direction = GetShootDirection();
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private Vector2 GetMouseDirectionFromOwner()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0f;

        Vector2 direction =
            (Vector2)mousePosition - (Vector2)owner.position;

        if (direction.sqrMagnitude < 0.001f)
            return Vector2.right;

        return direction.normalized;
    }

    private Vector2 GetShootDirection()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0f;

        Vector2 direction =
            (Vector2)mousePosition - (Vector2)firePoint.position;

        if (!IsValidVector(direction) || direction.sqrMagnitude < 0.001f)
            return transform.right;

        return direction.normalized;
    }
    private bool IsValidVector(Vector2 value)
    {
        return
            !float.IsNaN(value.x) &&
            !float.IsNaN(value.y) &&
            !float.IsInfinity(value.x) &&
            !float.IsInfinity(value.y);
    }

    private Vector2 ApplyAccuracyPenalty(Vector2 direction)
    {
        PlayerCombatModifiers modifiers = GetComponentInParent<PlayerCombatModifiers>();

        if (modifiers == null || modifiers.accuracyPenaltyDegrees <= 0f)
            return direction;

        float randomAngle = Random.Range(
            -modifiers.accuracyPenaltyDegrees,
            modifiers.accuracyPenaltyDegrees
        );

        return RotateVector(direction, randomAngle);
    }
    public void FireExternalProjectile(Vector2 direction)
    {
        if (bulletPrefab == null)
            return;

        if (firePoint == null)
            firePoint = transform;

        SpawnSingleProjectile(direction);
    }
}