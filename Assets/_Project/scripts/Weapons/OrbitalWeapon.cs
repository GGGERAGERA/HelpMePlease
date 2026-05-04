using UnityEngine;

public class OrbitalWeapon : BaseWeapon
{
    [Header("Orbit Settings")]
    public Transform player;
    public float orbitRadius = 1.2f;
    public float smoothSpeed = 15f;

    [Header("Laser Line Visual")]
    public LineRenderer laserLine;
    public float laserVisibleTime = 0.08f;
    public float laserWidth = 0.08f;

    [Header("Optional Particle Visuals")]
    public ParticleSystem muzzleFlashParticles;
    public ParticleSystem hitParticles;

    [Header("Combat")]
    public LayerMask enemyLayer;

    private Camera mainCamera;
    private Vector3 lastMouseWorldPosition;
    private float laserHideTime;

    protected override void Start()
    {
        base.Start();

        mainCamera = Camera.main;

        if (firePoint == null)
        {
            firePoint = transform;
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        if (laserLine == null)
        {
            laserLine = GetComponentInChildren<LineRenderer>(true);
        }

        SetupLaserLine();
        HideLaserLine();

        StopParticles(muzzleFlashParticles);
        StopParticles(hitParticles);
    }

    protected override void Update()
    {
        base.Update();

        if (Time.timeScale == 0f)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;

            if (mainCamera == null)
            {
                return;
            }
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                player = playerObject.transform;
            }
            else
            {
                return;
            }
        }

        UpdateMousePosition();
        UpdateOrbitPosition();

        if (Input.GetMouseButton(0) && CanAttack())
        {
            Attack();
        }

        if (laserLine != null && laserLine.enabled && Time.time >= laserHideTime)
        {
            HideLaserLine();
        }
    }

    public override void Attack()
    {
        if (firePoint == null)
        {
            firePoint = transform;
        }

        Vector3 origin = firePoint.position;

        Vector3 direction = lastMouseWorldPosition - origin;
        direction.z = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        direction.Normalize();

        float range = GetRange();
        int damage = GetDamage();

        Vector3 endPoint = origin + direction * range;

        RaycastHit2D hit = FindFirstEnemyHit(origin, direction, range);

        if (hit.collider != null)
        {
            endPoint = hit.point;

            EnemyHealth enemyHealth = hit.collider.GetComponent<EnemyHealth>();

            if (enemyHealth == null)
            {
                enemyHealth = hit.collider.GetComponentInParent<EnemyHealth>();
            }

            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
            }

            PlayHitParticles(endPoint, direction);
        }

        ShowLaserLine(origin, endPoint);
        PlayMuzzleFlash(origin, direction);

        MarkAttackTime();

        Debug.DrawLine(origin, endPoint, Color.red, 0.15f);
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
        {
            return;
        }

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
        {
            return;
        }

        directionToMouse.Normalize();

        float angle = Mathf.Atan2(directionToMouse.y, directionToMouse.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        transform.localScale = Vector3.one;
    }

    private RaycastHit2D FindFirstEnemyHit(Vector3 origin, Vector3 direction, float range)
    {
        RaycastHit2D[] hits;

        if (enemyLayer.value == 0)
        {
            hits = Physics2D.RaycastAll(origin, direction, range);
        }
        else
        {
            hits = Physics2D.RaycastAll(origin, direction, range, enemyLayer);
        }

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            EnemyHealth enemyHealth = hit.collider.GetComponent<EnemyHealth>();

            if (enemyHealth == null)
            {
                enemyHealth = hit.collider.GetComponentInParent<EnemyHealth>();
            }

            if (enemyHealth != null)
            {
                return hit;
            }
        }

        return new RaycastHit2D();
    }

    private void SetupLaserLine()
    {
        if (laserLine == null)
        {
            Debug.LogWarning("OrbitalWeapon: Laser Line is not assigned. Laser damage will work, but line will not be visible.");
            return;
        }

        laserLine.positionCount = 2;
        laserLine.useWorldSpace = true;

        laserLine.startWidth = laserWidth;
        laserLine.endWidth = laserWidth;

        laserLine.enabled = false;
    }

    private void ShowLaserLine(Vector3 startPoint, Vector3 endPoint)
    {
        if (laserLine == null)
        {
            return;
        }

        laserLine.enabled = true;

        laserLine.SetPosition(0, startPoint);
        laserLine.SetPosition(1, endPoint);

        laserHideTime = Time.time + laserVisibleTime;
    }

    private void HideLaserLine()
    {
        if (laserLine != null)
        {
            laserLine.enabled = false;
        }
    }

    private void PlayMuzzleFlash(Vector3 position, Vector3 direction)
    {
        if (muzzleFlashParticles == null)
        {
            return;
        }

        muzzleFlashParticles.transform.position = position;
        muzzleFlashParticles.transform.right = direction;

        muzzleFlashParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        muzzleFlashParticles.Play(true);
    }

    private void PlayHitParticles(Vector3 position, Vector3 direction)
    {
        if (hitParticles == null)
        {
            return;
        }

        hitParticles.transform.position = position;
        hitParticles.transform.right = direction;

        hitParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        hitParticles.Play(true);
    }

    private void StopParticles(ParticleSystem particles)
    {
        if (particles == null)
        {
            return;
        }

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}