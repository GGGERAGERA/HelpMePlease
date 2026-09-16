using UnityEngine;

/// <summary>Player-only, bidirectional sector prototype. No physics triggers on other actors.</summary>
[DefaultExecutionOrder(-100)]
public sealed class ProductionPortalPair : MonoBehaviour
{
    public static readonly Color ColorA = new(0.1f, 0.9f, 1f);
    public static readonly Color ColorB = new(0.8f, 0.3f, 1f);
    private const float EntryRadius = 0.8f;
    private const float RearmRadius = 1.2f;
    private Transform player;
    private Rigidbody2D playerBody;
    private ProductionPortalVisual visualA;
    private ProductionPortalVisual visualB;
    private bool armed;
    private float nextTeleportTime;
    private float flashUntil;
    public Vector2 PositionA { get; private set; }
    public Vector2 PositionB { get; private set; }

    public void Initialize(Vector2 a, Vector2 b, ProductionPortalVisual visualPrefab)
    {
        PositionA = a;
        PositionB = b;
        if (visualPrefab != null)
        {
            visualA = Instantiate(visualPrefab, new Vector3(a.x, a.y), Quaternion.identity, transform);
            visualA.name = "Portal A";
            visualA.Initialize(new Color(0.85f, 0.12f, 1f), 0f);
            visualB = Instantiate(visualPrefab, new Vector3(b.x, b.y), Quaternion.identity, transform);
            visualB.name = "Portal B";
            visualB.Initialize(new Color(0.1f, 0.72f, 1f), 1.2f);
        }
        // A player already standing on a newly spawned portal must first leave it.
        armed = false;
    }

    private void FixedUpdate()
    {
        if (player == null)
        {
            Transform actor = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
            if (actor == null) return;
            player = actor;
            playerBody = actor.GetComponent<Rigidbody2D>();
        }
        if (!player.gameObject.activeInHierarchy) return;
        Vector2 position = playerBody != null ? playerBody.position : (Vector2)player.position;
        float distanceA = Vector2.Distance(position, PositionA);
        float distanceB = Vector2.Distance(position, PositionB);
        if (distanceA > RearmRadius && distanceB > RearmRadius) armed = true;
        if (!armed || Time.time < nextTeleportTime) return;
        if (distanceA > EntryRadius && distanceB > EntryRadius) return;

        Vector2 destination = distanceA <= EntryRadius ? PositionB : PositionA;
        // Preserve rotation, velocity, input state and the player's Z plane.
        if (playerBody != null) playerBody.position = destination;
        player.position = new Vector3(destination.x, destination.y, player.position.z);
        armed = false;
        nextTeleportTime = Time.time + 1f;
        flashUntil = Time.time + 0.18f;
    }

    private void Update()
    {
        visualA?.Render(Time.time, nextTeleportTime - Time.time, flashUntil - Time.time);
        visualB?.Render(Time.time, nextTeleportTime - Time.time, flashUntil - Time.time);
    }
}
