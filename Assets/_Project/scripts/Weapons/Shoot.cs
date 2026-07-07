using UnityEngine;

public class Shoot : BaseWeapon
{
    [Header("Shoot Settings")]
    public GameObject bulletPrefab;
    public GameObject ShootFX;
    [SerializeField] private float baseKnockbackForce = 4f;
    [SerializeField] private float spreadAngle = 12f;

    [Header("Sprite")]
    [SerializeField] private SpriteRenderer weaponSprite;

    [Header("Recoil")]
    [SerializeField] private float recoilDistance = 0.12f;
    [SerializeField] private float recoilReturnSpeed = 14f;

    [SerializeField] private MonoBehaviour fireBehaviourSource;

    private IWeaponFireBehaviour fireBehaviour;

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
    protected override void Awake()
    {
        base.Awake();

        fireBehaviour = fireBehaviourSource as IWeaponFireBehaviour;

        if (fireBehaviour == null)
            Debug.LogWarning("[Shoot] Fire behaviour source is missing or invalid.");
    }

    protected override void Update()
    {
        base.Update();

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

        Vector2 baseDirection = ApplyAccuracyPenalty(GetAimDirectionFromFirePoint());

        FireShotGroup(baseDirection);

        if (weaponData != null)
            PlaySound(weaponData.attackSound);

        currentRecoil = recoilDistance;

        PlayFireFx();
    }

    private void FireShotGroup(Vector2 baseDirection)
    {
        int safeCount = Mathf.Max(1, GetProjectileCount());

        for (int i = 0; i < safeCount; i++)
        {
            float angleOffset = GetSpreadOffset(i, safeCount);
            Vector2 direction = RotateVector(baseDirection, angleOffset);

            WeaponFireContext context = BuildFireContext(
                firePoint.position,
                direction
            ).WithKnockback(GetKnockbackForce(baseKnockbackForce));

            FireSingleShot(context);
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

    private void FireSingleShot(WeaponFireContext context)
    {
        if (fireBehaviour == null)
            return;

        fireBehaviour.Fire(context);
    }

    private void PlayFireFx()
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

    public void FireExternalProjectile(WeaponFireContext context)
    {
        if (bulletPrefab == null)
            return;

        if (firePoint == null)
            firePoint = transform;

        FireSingleShot(context);
    }
    public void FireExternalProjectile(Vector2 direction)
    {
        if (firePoint == null)
            return;

        if (direction.sqrMagnitude < 0.001f)
            return;

        WeaponFireContext context = BuildFireContext(
            firePoint.position,
            direction.normalized
        );

        FireSingleShot(context);
    }
}