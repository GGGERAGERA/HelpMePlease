using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(BallRollVisual))]
public sealed class FootballBallRuntime : MonoBehaviour, IAnomalyExternalVelocity
{
    private readonly AnomalyExternalVelocityStack anomalyVelocity = new();

    private Rigidbody2D body;
    private FootballMinigame owner;
    private Transform spawnPoint;
    private Bounds playBounds;
    private float stuckSpeed;
    private float stuckDuration;
    private float outOfBoundsPadding;
    private float respawnDelay;
    private float stillTime;
    private float respawnAt = -1f;

    public Component ExternalVelocityComponent => this;
    public bool IsRespawning => respawnAt >= 0f;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    public void Configure(
        FootballMinigame minigame,
        Transform assignedSpawnPoint,
        Bounds assignedPlayBounds,
        float assignedStuckSpeed,
        float assignedStuckDuration,
        float assignedOutOfBoundsPadding,
        float assignedRespawnDelay)
    {
        owner = minigame;
        spawnPoint = assignedSpawnPoint;
        playBounds = assignedPlayBounds;
        stuckSpeed = Mathf.Max(0f, assignedStuckSpeed);
        stuckDuration = Mathf.Max(0.1f, assignedStuckDuration);
        outOfBoundsPadding = Mathf.Max(0f, assignedOutOfBoundsPadding);
        respawnDelay = Mathf.Max(0f, assignedRespawnDelay);
        stillTime = 0f;
        respawnAt = -1f;
        anomalyVelocity.Clear();
    }

    private void FixedUpdate()
    {
        if (owner == null || !owner.IsRunning || IsRespawning)
            return;

        // GravityZone supplies a velocity contribution through the same project
        // contract used by projectiles and characters. For a Rigidbody2D ball it
        // is applied as acceleration so momentum and collisions remain physical.
        body.AddForce(anomalyVelocity.Value, ForceMode2D.Force);

        if (!ContainsWithPadding(playBounds, body.position, outOfBoundsPadding))
        {
            RequestRespawn();
            return;
        }

        if (body.linearVelocity.sqrMagnitude <= stuckSpeed * stuckSpeed)
            stillTime += Time.fixedDeltaTime;
        else
            stillTime = 0f;

        if (stillTime >= stuckDuration)
            RequestRespawn();
    }

    private void Update()
    {
        if (!IsRespawning || Time.unscaledTime < respawnAt)
            return;

        respawnAt = -1f;
        RespawnNow();
    }

    public void RequestRespawn()
    {
        if (IsRespawning)
            return;

        anomalyVelocity.Clear();
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.simulated = false;
        respawnAt = Time.unscaledTime + respawnDelay;
    }

    public void RespawnNow()
    {
        if (spawnPoint == null)
            return;

        respawnAt = -1f;
        stillTime = 0f;
        anomalyVelocity.Clear();
        body.simulated = true;
        GetComponent<BallRollVisual>().ResetBall(spawnPoint);
    }

    public void SetAnomalyExternalVelocity(Object source, Vector2 velocity)
    {
        anomalyVelocity.Set(source, velocity);
    }

    public void RemoveAnomalyExternalVelocity(Object source)
    {
        anomalyVelocity.Remove(source);
    }

    private void OnDisable()
    {
        anomalyVelocity.Clear();
        respawnAt = -1f;
        stillTime = 0f;
    }

    private static bool ContainsWithPadding(Bounds bounds, Vector2 point, float padding)
    {
        return point.x >= bounds.min.x - padding &&
               point.x <= bounds.max.x + padding &&
               point.y >= bounds.min.y - padding &&
               point.y <= bounds.max.y + padding;
    }
}
