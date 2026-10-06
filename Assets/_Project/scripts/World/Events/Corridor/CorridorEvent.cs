using System.Collections.Generic;
using UnityEngine;

public sealed class CorridorEvent : WorldEvent, ICorridorNavigation, IWorldEventObjectiveProvider
{
    [SerializeField] private CorridorConfig config;
    public CorridorConfig Config => config;
    public CorridorRoute Route { get; private set; }
    public CorridorSettings Settings { get; private set; }
    public CorridorRuntimeState State { get; private set; }
    public float Elapsed { get; private set; }
    public string Result { get; private set; } = "Ready";
    public string Pattern => strikes == null ? "Quiet first segment" : strikes.Pattern.ToString();
    public float CollapseDistance => collapse?.Distance ?? 0;
    public bool IsFinalPush => State != null && State.Completed == Route.CheckpointCount;
    public float FinalPushRemaining => State?.FinalPushRemaining ?? 0;
    public bool ExitReady => State != null && State.ExitOpen;
    public bool IsFinishing => State?.Phase == CorridorPhase.Finishing;
    public bool UnderPressure => collapse != null && collapse.UnderPressure;
    public int BoundaryHits { get; private set; }
    public int RocketsLaunched => strikes?.Launched ?? 0;
    public IReadOnlyList<Collider2D> Walls => presentation?.Walls;
    public System.Action<string> Finished;
    public event System.Action<int> CheckpointPassed;
    public override Vector3 RewardPosition => Route != null ? (Vector3)Route.Sample(Route.Length) : transform.position;
    public override SiteEnvironmentCompletionPolicy EnvironmentCompletionPolicy => SiteEnvironmentCompletionPolicy.PreserveUntilOwnerReset;
    public override void CollectReservedFootprints(List<Rect> footprints)
    { if (!IsCompleted && Route != null) footprints.AddRange(CorridorPlacement.Footprints(Route)); }
    private Transform player;
    private PlayerHealth health;
    private Rigidbody2D playerBody;
    private CorridorPresentation presentation;
    private CorridorStrikes strikes;
    private CorridorCollapse collapse;
    private Vector2 previous;
    private bool cleaned, explicitSelection;
    private int turns;

    // Scene adapters may set inputs, never progression or actor position.
    public void SetRouteInput(CorridorSettings settings, int quarterTurns)
    { if (IsStarted) return; Settings = settings.Snapshot(); turns = quarterTurns; explicitSelection = true; Route = null; }
    public override bool TryValidateConfiguration(out string error)
    { if (config != null) return config.TryValidate(out error); error = "Missing CorridorConfig."; return false; }
    public override bool TryPreparePlacement(WorldEventPlacementContext context, out string error)
    {
        if (!TryValidateConfiguration(out error)) return false;
        Settings ??= config.settings.Snapshot();
        bool prepared = CorridorPlacement.TryPrepare(Settings, context, explicitSelection, turns,
            out var route, out int selectedTurns, out error);
        if (prepared)
        {
            Route = route; turns = selectedTurns; Settings.routeSeed = route.Seed;
            Debug.Log($"[Corridor] admitted {Settings.routePreset} seed={route.Seed} rotation={turns*90} start={context.Origin}", this);
        }
        return prepared;
    }
    public override void Initialize(WorldEventSpawner spawner)
    {
        base.Initialize(spawner);
        if (Route == null) return;
        player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
        if (player == null || !player.TryGetComponent(out health) || health.IsDead)
        { DisposeForOwnerReset(); return; }
        playerBody = player.GetComponent<Rigidbody2D>();
        health.Died += OnPlayerDied;
        State = new CorridorRuntimeState(Route, Settings);
        collapse = new CorridorCollapse(Route, Settings);
        ShowEventMarker(transform, "event.evacuation.name");
    }
    private Vector2 PlayerPosition => playerBody != null ? playerBody.position : (Vector2)player.position;
    protected override bool CanStartFrom(Vector2 position) => Route != null && health != null && !health.IsDead &&
        Vector2.Distance(position, transform.position) <= 2.5f;
    protected override void OnEventStarted()
    {
        if (!CorridorPlacement.Admitted(Route, owner.CreatePlacementContext(this), out _)) { Cancel(); return; }
        int mask = 0;
        foreach (var collider in player.GetComponentsInChildren<Collider2D>(true))
            if (!collider.isTrigger) mask |= 1 << collider.gameObject.layer;
        int enemy = LayerMask.NameToLayer("Enemy");
        if (mask == 0 || (enemy >= 0 && (mask & (1 << enemy)) != 0)) { Cancel(); return; }
        presentation = new CorridorPresentation(transform, Route, mask, config.kit, State);
        strikes = new CorridorStrikes(this, Route, Settings, config.rocket, config.warning, config.explosion);
        State.Start(); Result = "Running"; previous = PlayerPosition;
    }
    private void Update() => Tick(Time.deltaTime);
    internal void Tick(float delta)
    {
        if (!IsStarted || IsCompleted || State == null || delta <= 0) return;
        if (player == null || health == null || health.IsDead) { End(false, "Player down"); return; }
        Elapsed += delta;
        Vector2 position = PlayerPosition;
        if (State.Phase == CorridorPhase.Finishing)
        {
            State.Tick(delta); presentation.Tick(delta, CollapseDistance, true, true);
            if (State.Phase == CorridorPhase.Completed) End(true, "Exit reached");
            return;
        }
        if (State.TryAdvance(previous, position))
        {
            presentation.CheckpointPulse(); CameraShake.Instance?.Shake(.1f,.045f);
            AudioService.Instance?.PlayAt(Settings.checkpointSfx, position);
            CheckpointPassed?.Invoke(State.Completed);
        }
        State.Tick(delta);
        collapse.Tick(delta, State.Completed > 0, IsFinalPush);
        if (collapse.TryPressure(position, State.Completed > 0, out float damage))
        {
            Route.Point(CollapseDistance, out var direction);
            if (health.TakeDamage(damage, direction)) { BoundaryHits++; collapse.DamageApplied(); }
            if (IsCompleted) return;
        }
        if (State.TryBeginFinish(previous,position))
        {
            strikes.Cancel(); AudioService.Instance?.PlayAt(Settings.completionSfx,position);
            presentation.Tick(delta,CollapseDistance,true,true,UnderPressure); return;
        }
        previous = position;
        strikes.Tick(delta,position,State.Completed>0,IsFinalPush,() => isActiveAndEnabled && !IsCompleted && health != null && !health.IsDead);
        if (!IsCompleted) presentation.Tick(delta,CollapseDistance,ExitReady,false,UnderPressure);
    }
    private void OnPlayerDied() => End(false,"Player down");
    private void End(bool success,string message)
    {
        if (IsCompleted) return;
        if (!success) State?.Terminate(CorridorPhase.Failed);
        Result = message;
        if (success) CompleteEvent(); else { FailEvent(); Destroy(gameObject); }
        Finished?.Invoke(message);
    }
    protected override void CleanupEvent()
    {
        if (cleaned) return; cleaned = true;
        State?.Terminate(CorridorPhase.Cancelled);
        if (health != null) health.Died -= OnPlayerDied;
        strikes?.Dispose(); presentation?.Dispose();
    }
    private void OnDisable()
    {
        if (!IsCompleted && gameObject.scene.isLoaded) Cancel();
        else if (!IsCompleted) DisposeForOwnerReset();
    }
    public override void Cancel()
    { if (IsCompleted) return; State?.Terminate(CorridorPhase.Cancelled); End(false,"Cancelled"); }
    public CorridorNavigationSnapshot GetNavigationSnapshot() => new(Route,State,CollapseDistance,strikes?.Threats ?? System.Array.Empty<CorridorStrikeThreat>());
    public WorldEventObjectiveGuidance GetObjectiveGuidance()
    {
        var snapshot = GetNavigationSnapshot();
        return new("event.evacuation.name", IsFinalPush ? (ExitReady ? "event.corridor.exitOpen" : "event.corridor.final") : "event.corridor.gates",
            snapshot.Objective, State.Completed, Route.CheckpointCount);
    }
    public override void CollectTacticalMapMarkers(List<TacticalMapMarkerDescriptor> markers)
    {
        base.CollectTacticalMapMarkers(markers);
        if (IsStarted && !IsCompleted && Route != null && State != null)
            markers?.Add(new TacticalMapMarkerDescriptor(TacticalMapMarkerKind.Target,GetNavigationSnapshot().Objective));
    }
}
