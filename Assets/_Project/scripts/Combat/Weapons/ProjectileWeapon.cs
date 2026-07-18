using UnityEngine;

public class ProjectileWeapon : BaseWeapon
{
    [Header("Shoot Settings")]
    public GameObject ShootFX;
    [SerializeField] private float baseKnockbackForce = 4f;
    [SerializeField] private float spreadAngle = 0f;
    [Header("Sprite")]
    [SerializeField] private SpriteRenderer weaponSprite;

    [Header("Recoil")]
    [SerializeField] private float recoilReturnSpeed = 14f;

    [SerializeField] private MonoBehaviour fireBehaviourSource;
    [SerializeField] private ProjectileShotPattern shotPattern;

    [Header("Audio (Stage 1)")]
    [SerializeField] private AudioCueId attackCue = AudioCueId.None;

    private IWeaponFireBehaviour fireBehaviour;

    private float currentRecoil;

    protected override void Start()
    {
        base.Start();
        if (shotPattern == null)
            shotPattern = GetComponent<ProjectileShotPattern>();

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

    public override bool Attack()
    {
        if (!TryGetAimDirectionFromFirePoint(out Vector2 aimDirection))
            return false;

        Vector2 baseDirection = ApplyAccuracyPenalty(aimDirection);

        if (!FireShotGroup(baseDirection))
            return false;

        if (AudioService.Instance != null)
            AudioService.Instance.PlayAt(attackCue, firePoint.position);
        else if (weaponData != null)
            PlaySound(weaponData.attackSound);

        FxPlayer?.PlayFire(firePoint.position, baseDirection);
        return true;
    }

    private bool FireShotGroup(Vector2 baseDirection)
    {
        if (shotPattern == null)
        {
            Debug.LogWarning($"[ProjectileWeapon] ShotPattern is missing on {name}.");
            return false;
        }

        WeaponFireContext context = BuildFireContext(
            firePoint.position,
            baseDirection
        ).WithKnockback(GetKnockbackForce(baseKnockbackForce));

        return shotPattern.FirePattern(
            context,
            fireBehaviour,
            spreadAngle
        );
    }

    public void FireExternalProjectile(Vector2 direction)
    {
        if (firePoint == null)
            firePoint = transform;

        if (direction.sqrMagnitude < 0.001f)
            return;

        WeaponFireContext context = BuildFireContext(
            firePoint.position,
            direction.normalized
        ).WithKnockback(GetKnockbackForce(baseKnockbackForce));

        if (fireBehaviour != null)
            fireBehaviour.Fire(context);
    }
}
