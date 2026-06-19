using UnityEngine;

public class LaserWeapon : BaseWeapon
{
    [Header("Orbit")]
    [SerializeField] private Transform owner;
    [SerializeField] private float orbitRadius = 0.55f;
    [SerializeField] private float orbitSmoothSpeed = 25f;

    [Header("Laser")]
    [SerializeField] private LayerMask hitMask;
    [SerializeField] private float beamWidth = 0.25f;

    [Header("Visual Beam")]
    [SerializeField] private Material beamMaterial;
    [SerializeField] private float beamVisibleTime = 0.08f;


    [SerializeField] private ParticleSystem muzzleFxPrefab;
    [SerializeField] private ParticleSystem hitFxPrefab;
    [SerializeField] private float fxLifetime = 0.25f;

    private Camera mainCamera;

    protected override void Start()
    {
        base.Start();

        mainCamera = Camera.main;

        if (owner == null && transform.parent != null)
            owner = transform.parent;

        if (firePoint == null)
            firePoint = transform;
    }

    protected override void Update()
    {
        base.Update();

        if (Time.timeScale == 0f)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (owner == null)
            return;

        Vector2 aimDirection = GetAimDirectionFromOwner();

        UpdateOrbit(aimDirection);
        RotateWeapon(aimDirection);

        if (Input.GetMouseButton(0))
        {
            if (CanAttack())
            {
                Attack();
            }
        }
    }

    public override void Attack()
    {
        Vector2 origin = firePoint.position;
        Vector2 direction = GetAimDirectionFromFirePoint();

        float range = GetRange();

        RaycastHit2D hit = Physics2D.CircleCast(
            origin,
            beamWidth * 0.5f,
            direction,
            range,
            hitMask
        );

        Vector2 endPoint = origin + direction * range;

        if (hit.collider != null)
        {
            endPoint = hit.point;

            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();

            if (enemy != null)
            {
                bool isCritical = RollCritical();
                float finalDamage = GetDamage();

                if (isCritical)
                    finalDamage *= GetCritMultiplier();

                enemy.TakeDamage(finalDamage, hit.point, isCritical);
                EnemyMovement movement = enemy.GetComponent<EnemyMovement>();

                if (movement != null)
                {
                    Vector2 knockbackDirection = enemy.transform.position - transform.position;
                    movement.ApplyKnockback(
                        knockbackDirection,
                        GetKnockbackForce(3f)
                    );
                }
            }
        }

        ShowBeam(origin, endPoint);
        SpawnBeamFx(muzzleFxPrefab, origin, direction);
        SpawnBeamFx(hitFxPrefab, endPoint, -direction);
        TryFireEveryFifthExtraBeam(origin, direction);

        if (weaponData != null)
            PlaySound(weaponData.attackSound);

        MarkAttackTime();
    }

    private void UpdateOrbit(Vector2 aimDirection)
    {
        Vector2 targetPosition = (Vector2)owner.position + aimDirection * orbitRadius;

        transform.position = Vector2.Lerp(
            transform.position,
            targetPosition,
            orbitSmoothSpeed * Time.deltaTime
        );
    }

    private void RotateWeapon(Vector2 aimDirection)
    {
        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private Vector2 GetAimDirectionFromOwner()
    {
        Vector2 mousePosition = GetMouseWorldPosition();
        Vector2 direction = mousePosition - (Vector2)owner.position;

        if (direction.sqrMagnitude < 0.001f)
            return Vector2.right;

        return direction.normalized;
    }

    private Vector2 GetAimDirectionFromFirePoint()
    {
        Vector2 mousePosition = GetMouseWorldPosition();
        Vector2 direction = mousePosition - (Vector2)firePoint.position;

        if (direction.sqrMagnitude < 0.001f)
            return Vector2.right;

        return direction.normalized;
    }

    private Vector2 GetMouseWorldPosition()
    {
        Vector3 mousePosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0f;
        return mousePosition;
    }
    private void ShowBeam(Vector2 origin, Vector2 endPoint)
    {
        CreateBeamLine(origin, endPoint, 0.22f, new Color(0f, 0.8f, 1f, 0.35f), "LaserGlow");
        CreateBeamLine(origin, endPoint, 0.07f, Color.white, "LaserCore");
    }

    private void CreateBeamLine(Vector2 origin, Vector2 endPoint, float width, Color color, string name)
    {
        GameObject beamObject = new(name);

        LineRenderer line = beamObject.AddComponent<LineRenderer>();

        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 6;
        line.numCornerVertices = 6;
        line.sortingOrder = 100;

        Material material = new Material(Shader.Find("Sprites/Default"));
        material.color = color;
        line.material = material;

        line.startColor = color;
        line.endColor = color;

        line.SetPosition(0, new Vector3(origin.x, origin.y, 0f));
        line.SetPosition(1, new Vector3(endPoint.x, endPoint.y, 0f));

        Destroy(beamObject, beamVisibleTime);
    }

    private void SpawnBeamFx(ParticleSystem prefab, Vector2 position, Vector2 direction)
    {
        if (prefab == null)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        ParticleSystem fx = Instantiate(
            prefab,
            position,
            Quaternion.Euler(0f, 0f, angle)
        );

        fx.Play();
        Destroy(fx.gameObject, fxLifetime);
    }

    private void TryFireEveryFifthExtraBeam(Vector2 origin, Vector2 baseDirection)
    {
        PlayerCombatModifiers modifiers = GetComponentInParent<PlayerCombatModifiers>();

        if (modifiers == null)
            return;

        if (!modifiers.ShouldFireExtraShot())
            return;

        Vector2 extraDirection = RotateVector(baseDirection, Random.Range(-12f, 12f));

        FireBeam(origin, extraDirection);
    }

    private void FireBeam(Vector2 origin, Vector2 direction)
    {
        float range = GetRange();

        RaycastHit2D hit = Physics2D.CircleCast(
            origin,
            beamWidth * 0.5f,
            direction,
            range,
            hitMask
        );

        Vector2 endPoint = origin + direction * range;

        if (hit.collider != null)
        {
            endPoint = hit.point;

            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();

            if (enemy != null)
            {
                bool isCritical = RollCritical();
                float finalDamage = GetDamage();

                if (isCritical)
                    finalDamage *= GetCritMultiplier();

                enemy.TakeDamage(finalDamage, hit.point, isCritical);

                EnemyMovement movement = enemy.GetComponent<EnemyMovement>();

                if (movement != null)
                {
                    Vector2 knockbackDirection = enemy.transform.position - transform.position;
                    movement.ApplyKnockback(
                        knockbackDirection,
                        GetKnockbackForce(3f)
                    );
                }
            }
        }

        ShowBeam(origin, endPoint);
        SpawnBeamFx(muzzleFxPrefab, origin, direction);
        SpawnBeamFx(hitFxPrefab, endPoint, -direction);
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
}