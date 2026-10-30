using UnityEngine;
using UnityEngine.Rendering;

// Shared shield geometry, directional protection, durability and pooled feedback.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class EnemyDirectionalShield : MonoBehaviour
{
    public enum ShieldKind { Durable, Breakable }
    public ShieldKind Kind;
    [Min(1)] public int MaximumBlockedHits = 4;
    [Range(0f, 360f)] public float ProtectedSector = 120f;
    [Min(0f)] public float TurnSpeed = 65f;
    [Min(.1f), Tooltip("World-space radius from the shield centre to the plate.")] public float ShieldDistance = 1.05f;
    public Vector2 LocalShieldCenter = new(0f, .5f);
    [Min(.01f)] public float ShieldThickness = .13f;
    public Transform ShieldPivot;
    public LineRenderer ShieldArc;
    public EdgeCollider2D ShieldCollider;
    public SortingGroup BodySorting;
    public LineRenderer[] DamageCracks;
    public ParticleSystem BlockSparks, BreakShards;
    public AudioSource FeedbackSource;
    public AudioClip BreakSound;
    private Transform playerBody;
    private EnemyHealth health;
    private float facing, flashUntil;
    private Vector2[] arcPoints;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool blockLockedExperiment;
    private float blockLockRemaining;
    public bool IsBlockLockedExperiment => blockLockedExperiment;
    public float BlockLockRemaining => blockLockRemaining;

    public void ConfigureBlockLockedExperiment(bool enabled)
    {
        if (enabled)
        {
            if (RunStateManager.Instance == null) return;
            RunStateManager.Instance.DebugMarkDevelopmentRun();
        }
        blockLockedExperiment = enabled;
        blockLockRemaining = 0f;
    }
#endif
    public int Blocks { get; private set; }
    public int RemainingBlockedHits { get; private set; }
    public bool IsBroken => Kind == ShieldKind.Breakable && RemainingBlockedHits <= 0;
    public Vector2 Center => transform.TransformPoint(LocalShieldCenter);
    public Vector2 Forward => new(Mathf.Cos(facing * Mathf.Deg2Rad), Mathf.Sin(facing * Mathf.Deg2Rad));
    public Color DurabilityColor => Kind == ShieldKind.Durable ? Blue : ColorAtFraction((float)RemainingBlockedHits / Mathf.Max(1, MaximumBlockedHits));
    public static readonly Color Blue = new(.15f, .35f, 1f);
    public static readonly Color Cyan = new(.15f, .9f, 1f);
    public static readonly Color Orange = new(1f, .65f, .12f);
    public static readonly Color Red = new(1f, .16f, .12f);

    private void Awake() => health = GetComponent<EnemyHealth>();
    private void OnEnable() { ResetForSpawn(); Bind(PlayerRuntimeReference.CachedPlayer != null ? PlayerRuntimeReference.CachedPlayer.transform : null); }

    public void ResetForSpawn()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ConfigureBlockLockedExperiment(false);
#endif
        Blocks = 0;
        RemainingBlockedHits = Mathf.Max(1, MaximumBlockedHits);
        flashUntil = 0f;
        BlockSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        BreakShards.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        FeedbackSource.Stop();
        RefreshPresentation();
    }

    public void Bind(Transform body)
    {
        playerBody = body;
        if (body == null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (blockLockedExperiment && blockLockRemaining > 0f) return;
#endif
        Vector2 direction = (Vector2)body.position - Center;
        facing = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        RefreshPresentation();
    }

    private void Update()
    {
        if (playerBody == null) Bind(PlayerRuntimeReference.CachedPlayer != null ? PlayerRuntimeReference.CachedPlayer.transform : null);
        if (!health.IsDead && playerBody != null && !EnemyDebugAiFreeze.IsFrozen && Application.isFocused)
        {
            Vector2 direction = (Vector2)playerBody.position - Center;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (blockLockedExperiment && blockLockRemaining > 0f)
            {
                if (direction.sqrMagnitude > .001f)
                    blockLockRemaining = Mathf.Max(0f, blockLockRemaining - Time.deltaTime);
                RefreshPresentation();
                return;
            }
#endif
            if (direction.sqrMagnitude > .001f)
                facing = Mathf.MoveTowardsAngle(facing, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, TurnSpeed * Time.deltaTime);
        }
        RefreshPresentation();
    }

    public void RefreshPresentation()
    {
        ShieldPivot.localPosition = LocalShieldCenter;
        // Enemy prefabs have different authored scale. Plate radius/thickness remain in world metres.
        Vector3 parentScale = ShieldPivot.parent.lossyScale;
        ShieldPivot.localScale = new(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
        ShieldPivot.rotation = Quaternion.Euler(0f, 0f, facing);
        if (arcPoints == null || arcPoints.Length != ShieldArc.positionCount) arcPoints = new Vector2[ShieldArc.positionCount];
        for (int i = 0; i < arcPoints.Length; i++)
        {
            float angle = Mathf.Lerp(-ProtectedSector * .5f, ProtectedSector * .5f, (float)i / (arcPoints.Length - 1)) * Mathf.Deg2Rad;
            arcPoints[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ShieldDistance;
            ShieldArc.SetPosition(i, arcPoints[i]);
        }
        ShieldArc.widthMultiplier = ShieldThickness;
        ShieldCollider.points = arcPoints;
        ShieldCollider.edgeRadius = ShieldThickness * .5f;
        ShieldCollider.enabled = ShieldArc.enabled = !IsBroken;
        int order = BodySorting.sortingOrder + (Forward.y > .05f ? -2 : 1);
        ShieldArc.sortingLayerID = BodySorting.sortingLayerID;
        ShieldArc.sortingOrder = order;
        SortFeedback(BlockSparks);
        SortFeedback(BreakShards);
        float flash = Mathf.Clamp01((flashUntil - Time.time) / .09f);
        ShieldArc.startColor = ShieldArc.endColor = Color.Lerp(DurabilityColor, Color.white, flash);
        int cracks = Kind == ShieldKind.Breakable ? Mathf.CeilToInt((1f - (float)RemainingBlockedHits / Mathf.Max(1, MaximumBlockedHits)) * DamageCracks.Length) : 0;
        for (int i = 0; i < DamageCracks.Length; i++)
        {
            var crack = DamageCracks[i];
            crack.enabled = !IsBroken && i < cracks;
            crack.sortingLayerID = BodySorting.sortingLayerID;
            crack.sortingOrder = order + 1;
            float angle = Mathf.Lerp(-ProtectedSector * .3f, ProtectedSector * .3f, (i + .5f) / DamageCracks.Length) * Mathf.Deg2Rad;
            Vector2 radial = new(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 tangent = new(-radial.y, radial.x);
            for (int p = 0; p < crack.positionCount; p++)
                crack.SetPosition(p, radial * (ShieldDistance + Mathf.Lerp(-.5f, .5f, (float)p / (crack.positionCount - 1)) * ShieldThickness) + tangent * (p % 2 == 0 ? -.025f : .025f));
        }
    }

    public static Color ColorAtFraction(float fraction)
    {
        if (fraction >= .75f) return Color.Lerp(Cyan, Blue, (fraction - .75f) * 4f);
        if (fraction >= .5f) return Color.Lerp(Orange, Cyan, (fraction - .5f) * 4f);
        return Color.Lerp(Red, Orange, Mathf.Clamp01((fraction - .25f) * 4f));
    }

    private void SortFeedback(ParticleSystem particles)
    {
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingLayerID = BodySorting.sortingLayerID;
        renderer.sortingOrder = BodySorting.sortingOrder + 4;
    }

    public bool Covers(Vector2 attackSide) => !IsBroken && attackSide.sqrMagnitude > .000001f &&
        Vector2.Dot(Forward, attackSide.normalized) >= Mathf.Cos(ProtectedSector * .5f * Mathf.Deg2Rad);

    public bool CanBlockProjectile(Vector2 incomingDirection) => Covers(-incomingDirection);

    public bool BlockBeam(Vector2 from, Vector2 to)
    {
        if (IsBroken || !Covers(from - Center)) return false;
        foreach (var hit in Physics2D.LinecastAll(from, to))
            if (hit.collider == ShieldCollider) { RegisterBlock(hit.point); return true; }
        return false;
    }

    public bool BlockDiskContact(Vector2 center, float radius)
    {
        if (health.IsDead || !Covers(center - Center)) return false;
        Vector2 point = ShieldCollider.ClosestPoint(center);
        if ((point - center).sqrMagnitude > radius * radius) return false;
        RegisterBlock(point);
        return true;
    }

    // Called only for a real collision with this plate, never for a remote body hit.
    public bool BlockProjectile(Vector2 incomingDirection, Vector2 hitPoint, bool consumeDurability = true)
    {
        if (health.IsDead || !CanBlockProjectile(incomingDirection) ||
            ((Vector2)ShieldCollider.ClosestPoint(hitPoint) - hitPoint).sqrMagnitude > .0001f) return false;
        RegisterBlock(hitPoint, consumeDurability);
        return true;
    }

    private void RegisterBlock(Vector2 point, bool consumeDurability = true)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // A confirmed plate block starts one window; further hits cannot refresh it.
        if (blockLockedExperiment && blockLockRemaining <= 0f)
            blockLockRemaining = .9f;
#endif
        Blocks++;
        flashUntil = Time.time + .09f;
        BlockSparks.transform.position = point;
        if (!BlockSparks.isPlaying) BlockSparks.Play();
        BlockSparks.Emit(9);
        if (Kind == ShieldKind.Breakable && consumeDurability)
        {
            RemainingBlockedHits = Mathf.Max(0, RemainingBlockedHits - 1);
            if (IsBroken)
            {
                BreakShards.Play();
                for (int i = 0; i < 16; i++)
                {
                    Vector2 origin = ShieldPivot.TransformPoint(arcPoints[i * (arcPoints.Length - 1) / 15]);
                    BreakShards.Emit(new ParticleSystem.EmitParams { position = origin, velocity = (origin - Center).normalized * 2.8f,
                        startColor = i % 2 == 0 ? Red : Color.white, startLifetime = .35f, startSize3D = new Vector3(.05f, .14f, .05f) }, 1);
                }
                FeedbackSource.PlayOneShot(BreakSound);
                AudioService.Instance?.DebugRecordExternalPlayback(BreakSound, AudioCategory.SFX, point, this);
            }
        }
        RefreshPresentation();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ConfigureBlockLockedExperiment(false);
#endif
        playerBody = null;
        BlockSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        BreakShards.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        FeedbackSource.Stop();
    }
}
