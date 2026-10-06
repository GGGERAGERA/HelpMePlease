#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
// Intentionally separate from the production moving EvacuationCorridorEvent.
public sealed class CorridorV2Event : WorldEvent
{
    public CorridorV2Route Route { get; private set; }
    public CorridorV2Settings Settings { get; private set; }
    public float Elapsed { get; private set; }
    public string Result { get; private set; } = "Ready";
    public string Pattern => strikes == null ? "Quiet first segment" : strikes.Pattern.ToString();
    public float CollapseDistance { get; private set; }
    public bool IsFinalPush => Route != null && Route.ExitOpen;
    public float FinalPushRemaining { get; private set; }
    public bool ExitReady => IsFinalPush && FinalPushRemaining <= 0;
    public bool UnderPressure { get; private set; }
    public int BoundaryHits { get; private set; }
    public int RocketsLaunched => strikes?.Launched ?? 0;
    public IReadOnlyList<Collider2D> Walls => presentation.Walls;
    public System.Action<string> Finished;
    public event System.Action<int> CheckpointPassed;
    private Transform player;
    private PlayerHealth health;
    private Rigidbody2D playerBody;
    private CorridorV2Presentation presentation;
    private CorridorV2Strikes strikes;
    private Vector2 previous;
    private float damageCooldown, finishing = -1;
    private bool cleaned;
    public void Configure(Transform target, CorridorV2Settings settings, int turns,
        GameObject rocket, GameObject marker, ParticleSystem explosion)
    {
        player = target; health = target.GetComponent<PlayerHealth>(); playerBody = target.GetComponent<Rigidbody2D>();
        Settings = settings.Snapshot(); Route = new CorridorV2Route(Settings, turns);
        transform.position = Route.Vertices[0]; previous = PlayerPosition;
        int playerLayers = 0;
        foreach (var collider in player.GetComponentsInChildren<Collider2D>(true))
            if (!collider.isTrigger) playerLayers |= 1 << collider.gameObject.layer;
        presentation = new CorridorV2Presentation(transform, Route, playerLayers, Settings.font);
        strikes = new CorridorV2Strikes(this, Route, Settings, rocket, marker, explosion);
        FinalPushRemaining = Mathf.Max(0, Settings.finalPushDuration);
    }
    private Vector2 PlayerPosition => playerBody != null ? playerBody.position : (Vector2)player.position;
    protected override void OnEventStarted() { Result = "Running"; previous = PlayerPosition; }
    private void Update()
    {
        if (!IsStarted || IsCompleted || Time.timeScale <= 0f) return;
        if (player == null || health == null || health.IsDead) { End(false, "Player down"); return; }
        float delta = Time.deltaTime; Elapsed += delta;
        Vector2 position = PlayerPosition;
        if (finishing >= 0)
        {
            finishing -= delta; presentation.Tick(delta, CollapseDistance, true, true);
            if (finishing <= 0) End(true, $"EXIT reached in {Elapsed:F1}s");
            return;
        }
        if (Route.TryAdvance(previous, position))
        {
            presentation.CheckpointPulse();
            CameraShake.Instance?.Shake(.1f, .045f);
            AudioService.Instance?.PlayAt(Settings.checkpointSfx, position);
            CheckpointPassed?.Invoke(Route.Completed);
            if (IsFinalPush) presentation.Refresh(ExitReady);
        }
        previous = position;
        if (IsFinalPush)
        {
            FinalPushRemaining = Mathf.Max(0, FinalPushRemaining - delta);
            presentation.Refresh(ExitReady);
        }
        if (Route.Completed > 0)
        {
            // Once started, the front keeps advancing; camping just beyond a gate is unsafe.
            CollapseDistance = Mathf.Min(Route.Length, CollapseDistance + delta * Mathf.Max(.1f, Settings.collapseSpeed) *
                (IsFinalPush ? Mathf.Max(1, Settings.finalCollapseMultiplier) : 1f));
        }
        float along = Route.Project(position, out _);
        UnderPressure = Route.Completed > 0 && along <= CollapseDistance + .5f;
        damageCooldown -= delta;
        if (UnderPressure && Settings.collapseDamage > 0 && damageCooldown <= 0)
        {
            Route.Point(CollapseDistance, out Vector2 direction);
            if (health.TakeDamage(Settings.collapseDamage, direction))
            {
                BoundaryHits++; damageCooldown = Mathf.Max(.65f, Settings.collapseDamageInterval);
            }
        }
        if (ExitReady && Route.Contains(position) &&
            Vector2.Distance(position, Route.Point(Route.Length, out _)) < 2f)
        {
            finishing = .45f; strikes.Cancel();
            CameraShake.Instance?.Shake(.16f, .09f);
            AudioService.Instance?.PlayAt(Settings.completionSfx, position);
            presentation.Tick(delta, CollapseDistance, true, true);
            return;
        }
        strikes.Tick(delta, position, Route.Completed > 0, IsFinalPush,
            () => isActiveAndEnabled && !IsCompleted && health != null && !health.IsDead);
        presentation.Tick(delta, CollapseDistance, ExitReady, false);
    }
    private void End(bool success, string message)
    {
        Result = $"{message} | CP {Route.Completed}/{Route.CheckpointCount} | pressure hits {BoundaryHits} | rockets {RocketsLaunched}";
        Finished?.Invoke(Result);
        if (success) CompleteEvent();
        else { FailEvent(); Destroy(gameObject); }
    }
    protected override void CleanupEvent()
    {
        if (cleaned) return; cleaned = true;
        strikes?.Dispose(); presentation?.Dispose();
    }
    private void OnDisable() => strikes?.Cancel();
}
#endif
