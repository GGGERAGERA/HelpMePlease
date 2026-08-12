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
    [SerializeField] private Transform recoilVisual;
    [SerializeField] private float recoilDistance = 0.12f;
    [SerializeField] private float recoilRotationDegrees = 3f;
    [SerializeField] private float recoilReturnSpeed = 14f;

    [SerializeField] private MonoBehaviour fireBehaviourSource;
    [SerializeField] private ProjectileShotPattern shotPattern;

    [Header("Audio (Stage 1)")]
    [SerializeField] private AudioCueId attackCue = AudioCueId.None;

    private IWeaponFireBehaviour fireBehaviour;
    private bool usesRocketProjectile;

    protected override WeaponShotKind ShotKind => usesRocketProjectile
        ? WeaponShotKind.Rocket
        : WeaponShotKind.Standard;

    private float currentRecoil;
    private Vector3 recoilRestPosition;
    private Quaternion recoilRestRotation;
    private Vector3 recoilRestScale;
    private bool recoilRestStateCaptured;

    protected override void Start()
    {
        base.Start();
        if (shotPattern == null)
            shotPattern = GetComponent<ProjectileShotPattern>();

        if (weaponSprite == null)
            weaponSprite = GetComponentInChildren<SpriteRenderer>();

        CaptureRecoilRestState();

        if (owner == null && transform.parent != null)
            owner = transform.parent;
    }
    protected override void Awake()
    {
        base.Awake();

        fireBehaviour = fireBehaviourSource as IWeaponFireBehaviour;
        usesRocketProjectile =
            fireBehaviourSource is ProjectileFireBehaviour projectileFire &&
            projectileFire.UsesRocketProjectile;

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

        ApplyRecoil();
    }

    public override bool Attack()
    {
        if (!TryGetAimDirectionFromFirePoint(out Vector2 aimDirection))
            return false;

        return EmitAttack(firePoint.position, aimDirection);
    }

    protected override bool EmitAttack(WeaponFireContext context)
    {
        context = context.WithKnockback(
            GetKnockbackForce(baseKnockbackForce)
        );

        if (!FireShotGroup(context))
            return false;

        PlayRecoil();

        if (AudioService.Instance != null)
            AudioService.Instance.PlayAt(attackCue, context.Origin);
        else if (weaponData != null)
            PlaySound(weaponData.attackSound);

        FxPlayer?.PlayFire(context.Origin, context.Direction);
        return true;
    }

    private bool FireShotGroup(WeaponFireContext context)
    {
        if (shotPattern == null)
        {
            Debug.LogWarning($"[ProjectileWeapon] ShotPattern is missing on {name}.");
            return false;
        }

        return shotPattern.FirePattern(
            context,
            fireBehaviour,
            spreadAngle
        );
    }

    public override bool TryEmitExternalAttack(Vector2 direction)
    {
        if (firePoint == null)
            firePoint = transform;

        if (direction.sqrMagnitude < 0.001f)
            return false;

        WeaponFireContext context = BuildFireContext(
            firePoint.position,
            direction.normalized
        ).WithKnockback(GetKnockbackForce(baseKnockbackForce));

        if (fireBehaviour == null || !fireBehaviour.Fire(context))
            return false;

        PlayRecoil();
        return true;
    }

    private void CaptureRecoilRestState()
    {
        if (recoilVisual == null)
            return;

        recoilRestPosition = recoilVisual.localPosition;
        recoilRestRotation = recoilVisual.localRotation;
        recoilRestScale = recoilVisual.localScale;
        recoilRestStateCaptured = true;
    }

    private void PlayRecoil()
    {
        if (!recoilRestStateCaptured || recoilDistance <= 0f)
            return;

        currentRecoil = recoilDistance;
        ApplyRecoil();
    }

    private void ApplyRecoil()
    {
        if (!recoilRestStateCaptured)
            return;

        float recoilAmount = recoilDistance > 0f
            ? currentRecoil / recoilDistance
            : 0f;

        recoilVisual.localPosition = recoilRestPosition + Vector3.left * currentRecoil;
        recoilVisual.localRotation = recoilRestRotation * Quaternion.Euler(
            0f,
            0f,
            recoilRotationDegrees * recoilAmount
        );
        recoilVisual.localScale = recoilRestScale;
    }

    private void OnDisable()
    {
        currentRecoil = 0f;

        if (!recoilRestStateCaptured || recoilVisual == null)
            return;

        recoilVisual.localPosition = recoilRestPosition;
        recoilVisual.localRotation = recoilRestRotation;
        recoilVisual.localScale = recoilRestScale;
    }
}
