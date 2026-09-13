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
    private Material material;
    private LineRenderer ringA;
    private LineRenderer ringB;
    private bool armed;
    private float nextTeleportTime;
    private float flashUntil;
    public Vector2 PositionA { get; private set; }
    public Vector2 PositionB { get; private set; }

    public void Initialize(Vector2 a, Vector2 b)
    {
        PositionA = a;
        PositionB = b;
        material = AnomalyPowerVisuals.CreateMaterial("Portal pair material");
        ringA = CreateRing("Portal A", a, ColorA);
        ringB = CreateRing("Portal B", b, ColorB);
        // A player already standing on a newly spawned portal must first leave it.
        armed = false;
    }

    private LineRenderer CreateRing(string label, Vector2 position, Color color)
    {
        var line = AnomalyPowerVisuals.CreateLine(transform, label, color, 0.12f, 33, material);
        for (int i = 0; i <= 32; i++)
        {
            float angle = i * Mathf.PI * 2f / 32f;
            line.SetPosition(i, position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * EntryRadius);
        }
        return line;
    }

    private void FixedUpdate()
    {
        if (player == null)
        {
            var actor = GameObject.FindGameObjectWithTag("Player");
            if (actor == null) return;
            player = actor.transform;
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
        if (ringA == null || ringB == null) return;
        float width = Time.time < flashUntil ? 0.32f : 0.12f + 0.02f * Mathf.Sin(Time.time * 4f);
        ringA.startWidth = ringA.endWidth = width;
        ringB.startWidth = ringB.endWidth = width;
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
