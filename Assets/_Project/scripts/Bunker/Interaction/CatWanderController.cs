using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class CatWanderController : MonoBehaviour
{
    public enum CatState { Idle, Walking, Interacting }

    [Header("Wander")]
    [SerializeField, Min(.01f)] private float moveSpeed = 1.5f;
    [SerializeField, Min(0f)] private float minIdleTime = 2f;
    [SerializeField, Min(0f)] private float maxIdleTime = 7f;
    [SerializeField, Min(0f)] private float minWanderDistance = .8f;
    [SerializeField, Min(0f)] private float maxWanderDistance = 3f;
    [SerializeField, Min(.01f)] private float destinationTolerance = .08f;
    [SerializeField, Min(1)] private int attemptsToFindDestination = 16;
    [SerializeField, Min(.1f)] private float stuckTimeout = .6f;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [Tooltip("Existing bunker floor tilemaps; assigned on the scene instance.")]
    [SerializeField] private Tilemap[] walkableFloors;

    [Header("Existing graphics")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer graphic;
    [Tooltip("Direction the authored unflipped artwork faces.")]
    [SerializeField] private bool authoredFacesRight = false;

    private const float Skin = .025f;
    private static readonly int IdleAnimation = Animator.StringToHash("Base Layer.CatIdle1");
    private static readonly int WalkAnimation = Animator.StringToHash("Base Layer.CatWalk1");
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[32];
    private readonly Collider2D[] overlapHits = new Collider2D[32];
    private Rigidbody2D body;
    private CircleCollider2D footprint;
    private ContactFilter2D obstacleFilter;
    private Vector2 destination;
    private Vector2 lastPosition;
    private float stateUntil;
    private float stuckTime;
    private bool walkingAnimation;
    private bool initialFlip;

    public CatState State { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        footprint = GetComponent<CircleCollider2D>();
        obstacleFilter = new ContactFilter2D { useTriggers = false };
        obstacleFilter.SetLayerMask(obstacleMask);
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (graphic != null) initialFlip = graphic.flipX;
    }

    private void OnEnable()
    {
        lastPosition = body.position;
        EnterIdle(RandomIdle());
    }

    private void OnDisable()
    {
        if (body != null) body.linearVelocity = Vector2.zero;
        SetWalkingAnimation(false);
    }

    private void FixedUpdate()
    {
        Vector2 actualDelta = body.position - lastPosition;
        lastPosition = body.position;
        bool moved = State == CatState.Walking && actualDelta.sqrMagnitude > .000001f;
        SetWalkingAnimation(moved);
        if (moved && graphic != null && Mathf.Abs(actualDelta.x) > .0001f)
            graphic.flipX = initialFlip ^ ((actualDelta.x > 0f) != authoredFacesRight);

        if (SceneTransitionOverlay.IsTransitioning ||
            (BunkerContext.Instance != null && BunkerContext.Instance.Panels != null && BunkerContext.Instance.Panels.IsAnyPanelOpen))
        {
            EnterIdle(.5f);
            return;
        }
        if (State == CatState.Interacting)
        {
            if (Time.time >= stateUntil) EnterIdle(RandomIdle());
            return;
        }
        if (State == CatState.Idle)
        {
            if (Time.time >= stateUntil)
            {
                if (TryFindDestination(out destination))
                {
                    State = CatState.Walking;
                    stuckTime = 0f;
                }
                else EnterIdle(1f);
            }
            return;
        }

        Vector2 remaining = destination - body.position;
        if (remaining.magnitude <= destinationTolerance)
        {
            EnterIdle(RandomIdle());
            return;
        }
        stuckTime = moved ? 0f : stuckTime + Time.fixedDeltaTime;
        float step = Mathf.Min(moveSpeed * Time.fixedDeltaTime, remaining.magnitude);
        Vector2 next = body.position + remaining.normalized * step;
        // Recheck doors/props each physics step: a previously valid route can close.
        if (stuckTime >= stuckTimeout || !HasFloor(next) || !RouteClear(remaining.normalized, step + Skin))
        {
            EnterIdle(.75f);
            return;
        }
        body.MovePosition(next);
    }

    public void BeginPetting(float pauseDuration)
    {
        destination = body.position;
        body.linearVelocity = Vector2.zero;
        State = CatState.Interacting;
        stateUntil = Time.time + Mathf.Max(.1f, pauseDuration);
        SetWalkingAnimation(false);
    }

    private bool TryFindDestination(out Vector2 point)
    {
        for (int i = 0; i < attemptsToFindDestination; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float distance = Random.Range(minWanderDistance, Mathf.Max(minWanderDistance, maxWanderDistance));
            Vector2 candidate = body.position + direction * distance;
            if (!RouteClear(direction, distance + Skin) || !DestinationClear(candidate)) continue;
            bool floor = true;
            int samples = Mathf.Max(1, Mathf.CeilToInt(distance / .2f));
            for (int j = 1; j <= samples && floor; j++)
                floor = HasFloor(Vector2.Lerp(body.position, candidate, (float)j / samples));
            if (!floor) continue;
            point = candidate;
            return true;
        }
        point = body.position;
        return false;
    }

    private bool RouteClear(Vector2 direction, float distance)
    {
        int count = footprint.Cast(direction, obstacleFilter, castHits, distance);
        if (count == castHits.Length) return false;
        for (int i = 0; i < count; i++)
            if (castHits[i].collider != null && castHits[i].rigidbody != body) return false;
        return true;
    }

    private bool DestinationClear(Vector2 position)
    {
        Vector2 center = position + (Vector2)transform.TransformVector(footprint.offset);
        int count = Physics2D.OverlapCircle(center, Radius + Skin, obstacleFilter, overlapHits);
        if (count == overlapHits.Length) return false;
        for (int i = 0; i < count; i++)
            if (overlapHits[i] != null && overlapHits[i].attachedRigidbody != body) return false;
        return true;
    }

    private float Radius => footprint.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));

    private bool HasFloor(Vector2 position)
    {
        // Fail closed if this prefab hasn't been bound to the bunker floor.
        if (walkableFloors == null || walkableFloors.Length == 0) return false;
        Vector2 center = position + (Vector2)transform.TransformVector(footprint.offset);
        for (int i = 0; i < 9; i++)
        {
            Vector2 sample = center;
            if (i > 0)
            {
                float angle = (i - 1) * Mathf.PI / 4f;
                sample += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (Radius + Skin);
            }
            bool found = false;
            foreach (Tilemap floor in walkableFloors)
                if (floor != null && floor.isActiveAndEnabled && floor.HasTile(floor.WorldToCell(sample))) { found = true; break; }
            if (!found) return false;
        }
        return true;
    }

    private float RandomIdle() => Random.Range(minIdleTime, Mathf.Max(minIdleTime, maxIdleTime));

    private void EnterIdle(float duration)
    {
        State = CatState.Idle;
        destination = body.position;
        body.linearVelocity = Vector2.zero;
        stateUntil = Time.time + duration;
        stuckTime = 0f;
        SetWalkingAnimation(false);
    }

    private void SetWalkingAnimation(bool walking)
    {
        if (animator == null || !animator.isActiveAndEnabled ||
            (walkingAnimation == walking && animator.GetCurrentAnimatorStateInfo(0).fullPathHash != 0)) return;
        walkingAnimation = walking;
        animator.Play(walking ? WalkAnimation : IdleAnimation, 0, 0f);
    }
}
