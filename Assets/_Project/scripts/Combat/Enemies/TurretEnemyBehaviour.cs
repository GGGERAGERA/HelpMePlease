using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class TurretEnemyBehaviour : MonoBehaviour
{
    [Header("Existing prefab references")]
    [SerializeField] private Transform aimPivot;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Material aimLineMaterial;

    [Header("Attack")]
    [SerializeField, Min(0f)] private float aimDuration = 1f;
    [SerializeField, Min(0.1f)] private float cooldown = 2.5f;

    private Rigidbody2D body;
    private Transform player;
    private LineRenderer aimLine;
    private float timer;
    private bool aiming;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        BuildAimLine();
    }

    private void Start()
    {
        FindPlayer();
        BeginAim();
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
            return;

        if (player == null)
            FindPlayer();

        if (player == null)
        {
            SetAimLineVisible(false);
            return;
        }

        Vector2 direction = (
            (Vector2)player.position - (Vector2)GetFirePosition()
        ).normalized;
        RotateHead(direction);

        if (aiming)
            UpdateAimLine();

        timer -= Time.deltaTime;

        if (timer > 0f)
            return;

        if (aiming)
        {
            Shoot(direction);
            aiming = false;
            timer = cooldown;
            SetAimLineVisible(false);
        }
        else
        {
            BeginAim();
        }
    }

    private void FixedUpdate()
    {
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private void BeginAim()
    {
        aiming = true;
        timer = aimDuration;
        SetAimLineVisible(player != null);
    }

    private void Shoot(Vector2 direction)
    {
        if (projectilePrefab == null || direction.sqrMagnitude <= Mathf.Epsilon)
            return;

        GameObject projectile = Instantiate(
            projectilePrefab,
            GetFirePosition(),
            Quaternion.identity
        );
        projectile.GetComponent<EnemyProjectile>()?.Initialize(direction);
    }

    private void RotateHead(Vector2 direction)
    {
        if (aimPivot == null || direction.sqrMagnitude <= Mathf.Epsilon)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        aimPivot.rotation = Quaternion.Euler(0f, 0f, angle + 180f);
    }

    private void UpdateAimLine()
    {
        if (aimLine == null)
            return;

        aimLine.SetPosition(0, GetFirePosition());
        aimLine.SetPosition(1, player.position);
    }

    private void BuildAimLine()
    {
        aimLine = gameObject.AddComponent<LineRenderer>();
        aimLine.useWorldSpace = true;
        aimLine.positionCount = 2;
        aimLine.startWidth = 0.035f;
        aimLine.endWidth = 0.018f;
        aimLine.startColor = new Color(1f, 0.22f, 0.16f, 0.75f);
        aimLine.endColor = new Color(1f, 0.65f, 0.2f, 0.2f);
        aimLine.sortingOrder = 20;
        aimLine.sharedMaterial = aimLineMaterial;
        aimLine.enabled = false;
    }

    private Vector3 GetFirePosition()
    {
        return firePoint != null ? firePoint.position : transform.position;
    }

    private void SetAimLineVisible(bool visible)
    {
        if (aimLine != null)
            aimLine.enabled = visible;
    }

    private void FindPlayer()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    private void OnDisable()
    {
        SetAimLineVisible(false);
    }
}
