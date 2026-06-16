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

    [Header("Debug")]
    [SerializeField] private bool drawDebugLine = true;

    [Header("Visual Beam")]
    [SerializeField] private Material beamMaterial;
    [SerializeField] private float beamVisibleTime = 0.08f;
    [SerializeField] private float beamWidthVisual = 0.08f;

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
            Debug.Log("LASER LMB");

            if (CanAttack())
            {
                Debug.Log("LASER CAN ATTACK");
                Attack();
            }
        }
    }

    public override void Attack()
    {
        Debug.Log("LASER ATTACK");
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
            }
        }

        if (drawDebugLine)
            Debug.DrawLine(origin, endPoint, Color.cyan, 0.12f);

        ShowBeam(origin, endPoint);

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
        GameObject beamObject = new GameObject("LaserBeam_Runtime");

        LineRenderer line = beamObject.AddComponent<LineRenderer>();

        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = beamWidthVisual;
        line.endWidth = beamWidthVisual;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.sortingOrder = 100;

        if (beamMaterial != null)
            line.material = beamMaterial;
        else
            line.material = new Material(Shader.Find("Sprites/Default"));

        line.startColor = Color.cyan;
        line.endColor = Color.cyan;

        line.SetPosition(0, new Vector3(origin.x, origin.y, 0f));
        line.SetPosition(1, new Vector3(endPoint.x, endPoint.y, 0f));

        Destroy(beamObject, beamVisibleTime);
    }
}