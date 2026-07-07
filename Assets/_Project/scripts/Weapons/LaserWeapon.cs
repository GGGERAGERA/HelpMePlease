using UnityEngine;
using UnityEngine.Animations.Rigging;

public class LaserWeapon : BaseWeapon
{

    [Header("Laser")]
    [SerializeField] private LayerMask hitMask;
    [SerializeField] private float beamWidth = 0.25f;
    [Header("Beam Renderer")]
    [SerializeField] private LaserBeamRenderer beamRenderer;
    [SerializeField] private MonoBehaviour fireBehaviourSource;

    private IWeaponFireBehaviour fireBehaviour;


    private Camera mainCamera;

    protected override void Start()
    {
        base.Start();

        mainCamera = Camera.main;

        if (owner == null && transform.parent != null)
            owner = transform.parent;

        if (firePoint == null)
            firePoint = transform;
        if (beamRenderer == null)
            beamRenderer = GetComponent<LaserBeamRenderer>();
    }

    protected override void Update()
    {
        base.Update();
    }
    protected override void Awake()
    {
        base.Awake();

        fireBehaviour = fireBehaviourSource as IWeaponFireBehaviour;

        if (fireBehaviour == null)
            Debug.LogWarning("[LaserWeapon] Fire behaviour source is missing or invalid.");
    }
    public override void Attack()
    {
        Vector2 direction = ApplyAccuracyPenalty(GetAimDirectionFromFirePoint());

        WeaponFireContext context = BuildFireContext(
            firePoint.position,
            direction
        );

        if (fireBehaviour != null)
            fireBehaviour.Fire(context);

        if (weaponData != null)
            PlaySound(weaponData.attackSound);

        FxPlayer?.PlayFire(context.Origin, context.Direction);
    }
}