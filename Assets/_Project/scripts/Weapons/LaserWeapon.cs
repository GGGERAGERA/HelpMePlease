using UnityEngine;
using UnityEngine.Animations.Rigging;

public class LaserWeapon : BaseWeapon
{
    [Header("Orbit")]
    [SerializeField] private Transform owner;
    [SerializeField] private float orbitRadius = 0.55f;
    [SerializeField] private float orbitSmoothSpeed = 25f;

    [Header("Laser")]
    [SerializeField] private LayerMask hitMask;
    [SerializeField] private float beamWidth = 0.25f;
    [Header("Beam Base")]
    [SerializeField] private Material beamMaterial;
    [SerializeField] private float beamDuration = 0.08f;
    [Header("Beam Visual Layers")]
    [SerializeField] private Material coreBeamMaterial;
    [SerializeField] private Material glowBeamMaterial;

    [SerializeField] private Color coreBeamColor = Color.white;
    [SerializeField] private Color glowBeamColor = new Color(0.1f, 0.8f, 1f, 0.35f);

    [SerializeField] private float coreBeamWidth = 0.08f;
    [SerializeField] private float glowBeamWidth = 0.28f;


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

        if (!CanAttack())
            return;

        Vector2 origin = firePoint.position;
        Vector2 direction = GetAimDirectionFromFirePoint();
        direction = ApplyAccuracyPenalty(direction);
        
        FireBeam(origin, direction);

        TryFireRandomExtraBeams(origin, direction);

        if (weaponData != null)
            PlaySound(weaponData.attackSound);
        FxPlayer?.PlayFire(origin, direction);

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
    private void ShowBeam(Vector2 start, Vector2 end)
    {
        CreateBeamLine(start, end, glowBeamMaterial, glowBeamColor, glowBeamWidth, beamDuration);
        CreateBeamLine(start, end, coreBeamMaterial, coreBeamColor, coreBeamWidth, beamDuration);
    }

    private void CreateBeamLine(
     Vector2 start,
     Vector2 end,
     Material material,
     Color color,
     float width,
     float duration
 )
    {
        GameObject beamObject = new GameObject("LaserBeam");
        LineRenderer lineRenderer = beamObject.AddComponent<LineRenderer>();

        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);

        lineRenderer.useWorldSpace = true;
        lineRenderer.sortingLayerName = "Effects";
        lineRenderer.sortingOrder = 20;

        lineRenderer.material = material != null
            ? material
            : beamMaterial;

        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width * 0.75f;

        lineRenderer.startColor = color;
        lineRenderer.endColor = new Color(color.r, color.g, color.b, 0f);

        Destroy(beamObject, duration);
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
            HandleBeamHit(hit, direction);
        }

        ShowBeam(origin, endPoint);
    }

    private void HandleBeamHit(RaycastHit2D hit, Vector2 direction)
    {
        EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();

        if (enemy == null)
            return;

        bool isCritical = RollCritical();
        float finalDamage = GetDamage();

        if (isCritical)
            finalDamage *= GetCritMultiplier();

        enemy.TakeDamage(finalDamage, hit.point, isCritical);

        PlayerCombatModifiers modifiers = GetComponentInParent<PlayerCombatModifiers>();

        if (modifiers != null)
        {
            CombatExplosionService.TryExplodeOnHit(
                hit.point,
                finalDamage,
                modifiers,
                modifiers.enemyMask
            );
        }

        EnemyMovement movement = enemy.GetComponent<EnemyMovement>();

        if (movement != null)
        {
            Vector2 knockbackDirection = direction.normalized;
            movement.ApplyKnockback(
                knockbackDirection,
                GetKnockbackForce(3f)
            );
        }

        FxPlayer?.PlayHit(hit.point, -direction, isCritical);
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
    private void TryFireRandomExtraBeams(Vector2 origin, Vector2 baseDirection)
    {
        PlayerCombatModifiers modifiers = GetComponentInParent<PlayerCombatModifiers>();

        if (modifiers == null)
            return;

        if (modifiers.randomExtraShotsChance <= 0f)
            return;

        if (Random.value > modifiers.randomExtraShotsChance)
            return;

        for (int i = 0; i < 2; i++)
        {
            Vector2 randomDirection = RotateVector(
                baseDirection,
                Random.Range(-70f, 70f)
            );

            FireBeam(origin, randomDirection);
        }
    }
}