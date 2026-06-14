using UnityEngine;

public class LaserWeapon : BaseWeapon
{
    [Header("Orbit")]
    [SerializeField] private Transform player;
    [SerializeField] private float orbitRadius = 1.2f;
    [SerializeField] private float smoothSpeed = 15f;

    [Header("Laser Visual")]
    [SerializeField] private LineRenderer laserLine;
    [SerializeField] private float laserVisibleTime = 0.08f;
    [SerializeField] private float laserWidth = 0.08f;

    [Header("FX")]
    [SerializeField] private ParticleSystem hitParticles;

    [Header("Hit")]
    [SerializeField] private LayerMask hitMask;

    private Camera mainCamera;
    private Vector3 lastMouseWorldPosition;
    private float laserHideTime;

    protected override void Start()
    {
        base.Start();

        mainCamera = Camera.main;

        if (firePoint == null)
            firePoint = transform;

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }

        if (laserLine == null)
            laserLine = GetComponentInChildren<LineRenderer>(true);

        SetupLaserLine();
        HideLaserLine();

        if (hitParticles != null)
            hitParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    protected override void Update()
    {
        base.Update();

        if (Time.timeScale == 0f)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
            else
                return;
        }

        UpdateMousePosition();
        UpdateOrbitPosition();

        if (Input.GetMouseButton(0) && CanAttack())
            Attack();

        if (laserLine != null && laserLine.enabled && Time.time >= laserHideTime)
            HideLaserLine();
    }

    public override void Attack()
    {
        Vector3 origin = firePoint.position;

        Vector3 direction = lastMouseWorldPosition - origin;
        direction.z = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();

        float range = GetRange();
        float damage = GetDamage();

        Vector3 endPoint = origin + direction * range;

        RaycastHit2D hit = FindFirstHit(origin, direction, range);

        if (hit.collider != null)
        {
            endPoint = hit.point;

            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();

            if (enemy != null)
            {
                bool isCritical = RollCritical();

                if (isCritical)
                    damage *= GetCritMultiplier();

                enemy.TakeDamage(damage, hit.point, isCritical);
            }

            PlayHitParticles(endPoint, direction);
        }

        ShowLaserLine(origin, endPoint);

        if (weaponData != null)
            PlaySound(weaponData.attackSound);

        MarkAttackTime();
    }

    private void UpdateMousePosition()
    {
        lastMouseWorldPosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        lastMouseWorldPosition.z = 0f;
    }

    private void UpdateOrbitPosition()
    {
        Vector3 directionFromPlayer = lastMouseWorldPosition - player.position;
        directionFromPlayer.z = 0f;

        if (directionFromPlayer.sqrMagnitude < 0.001f)
            return;

        directionFromPlayer.Normalize();

        Vector3 targetPosition = player.position + directionFromPlayer * orbitRadius;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            smoothSpeed * Time.deltaTime
        );

        Vector3 directionToMouse = lastMouseWorldPosition - transform.position;
        directionToMouse.z = 0f;

        if (directionToMouse.sqrMagnitude < 0.001f)
            return;

        directionToMouse.Normalize();

        float angle = Mathf.Atan2(directionToMouse.y, directionToMouse.x) * Mathf.Rad2Deg;

        // Все оружия теперь смотрят вправо, поэтому без +180.
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        transform.localScale = Vector3.one;
    }

    private RaycastHit2D FindFirstHit(Vector3 origin, Vector3 direction, float range)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, range, hitMask);

        RaycastHit2D closestHit = new RaycastHit2D();
        float closestDistance = float.MaxValue;

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
            }
        }

        return closestHit;
    }

    private void SetupLaserLine()
    {
        if (laserLine == null)
            return;

        laserLine.positionCount = 2;
        laserLine.useWorldSpace = true;
        laserLine.startWidth = laserWidth;
        laserLine.endWidth = laserWidth;
        laserLine.enabled = false;
    }

    private void ShowLaserLine(Vector3 startPoint, Vector3 endPoint)
    {
        if (laserLine == null)
            return;

        laserLine.enabled = true;
        laserLine.SetPosition(0, startPoint);
        laserLine.SetPosition(1, endPoint);

        laserHideTime = Time.time + laserVisibleTime;
    }

    private void HideLaserLine()
    {
        if (laserLine != null)
            laserLine.enabled = false;
    }

    private void PlayHitParticles(Vector3 position, Vector3 direction)
    {
        if (hitParticles == null)
            return;

        hitParticles.transform.position = position;
        hitParticles.transform.right = direction;

        hitParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        hitParticles.Play(true);
    }
}