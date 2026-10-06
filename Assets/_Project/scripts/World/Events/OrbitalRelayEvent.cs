using System;
using System.Collections.Generic;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

public sealed class OrbitalRelayEvent : WorldEvent
{
    [SerializeField] private OrbitalRelayConfig config;
    [SerializeField] private CircleCollider2D arenaBounds;
    [SerializeField] private CircleCollider2D startArea;
    [Header("Node placement on start")]
    [SerializeField, Min(.1f)] private float nodeMinRadius = 3.5f;
    [SerializeField, Min(.1f)] private float nodeMaxRadius = 6.5f;
    [SerializeField, Min(.1f)] private float nodeMinSeparation = 2.5f;
    [SerializeField] private OrbitalRelayNode[] nodes;
    [SerializeField] private OrbitalRelayPresentation presentation;
    [SerializeField] private WorldEventPressureModifier pressure;
    [SerializeField] private Transform markerAnchor;
    [SerializeField] private Transform rewardAnchor;
    private OrbitalRelayState state;
    private OrbitalRelaySettings settings;
    private Transform player;
    private OrbitalStationRuntime station;
    public OrbitalRelaySnapshot Snapshot => state != null ? state.Snapshot : default;
    public OrbitalRelayNode ActiveNode => Snapshot.ActiveNodeIndex >= 0 && nodes != null && Snapshot.ActiveNodeIndex < nodes.Length
        ? nodes[Snapshot.ActiveNodeIndex] : null;
    public float ArenaRadius => arenaBounds != null ? arenaBounds.radius * Mathf.Abs(arenaBounds.transform.lossyScale.x) : 0;
    // Read-only presentation data: base placement limits, not a per-node validity mask.
    public CircleCollider2D ArenaBounds => arenaBounds;
    public Vector2 BaseNodeSpawnRange => new(nodeMinRadius, nodeMaxRadius);
    public bool IsPlayerInside { get; private set; }
    public bool IsPlayerInStartZone { get; private set; }
    public int RequiredActivations => config != null ? config.requiredActivations : 0;
    public override Vector3 RewardPosition => rewardAnchor != null ? rewardAnchor.position : transform.position;
    public override bool UsesStandardSpawnPressure => false;
    public override WorldEventRewardResult? CompletionReward => state?.Result is OrbitalRelayResult result && result.Success ? result.Reward : null;
    public event Action PlayerEntered;
    public event Action<OrbitalRelayPhase> PhaseChanged;
    public event Action<OrbitalRelayResult> Finished;

    public override bool TryValidateConfiguration(out string error)
    {
        error = "Orbital Relay requires config, arena, presentation, pressure, anchors and at least two distinct valid Nodes.";
        if (config == null || arenaBounds == null || startArea == null || startArea.GetComponent<Interactable>() != this ||
            presentation == null || pressure == null ||
            markerAnchor == null || rewardAnchor == null || !presentation.IsValid || nodes == null || nodes.Length < 2) return false;
        if (!config.TryGetSettings(out _, out error)) return false;
        var unique = new HashSet<OrbitalRelayNode>();
        foreach (var node in nodes)
            if (node == null || !unique.Add(node) || !node.IsValid)
            { error = "Orbital Relay Node collection contains a missing, duplicate or invalid authored Node."; return false; }
        Vector3 scale = arenaBounds.transform.lossyScale;
        if (ArenaRadius <= 0 || Mathf.Abs(Mathf.Abs(scale.x) - Mathf.Abs(scale.y)) > .001f)
        { error = "Orbital Relay arena requires a positive uniformly scaled circle."; return false; }
        foreach (var node in nodes)
        {
            node.TryGetContactCircle(out Vector2 center, out float radius);
            if (Vector2.Distance(arenaBounds.transform.TransformPoint(arenaBounds.offset), center) + radius > ArenaRadius)
            { error = "Orbital Relay Node lies outside its authored arena footprint."; return false; }
        }
        if (!TryGetPlacementRange(out _, out _) || startArea.radius <= 0 ||
            Vector2.Distance(startArea.transform.TransformPoint(startArea.offset), arenaBounds.transform.TransformPoint(arenaBounds.offset)) +
            startArea.radius * Mathf.Abs(startArea.transform.lossyScale.x) > ArenaRadius)
        { error = "Orbital Relay requires a contained start zone and feasible finite Node placement range."; return false; }
        error = null; return true;
    }
    private bool TryGetPlacementRange(out float inner, out float outer)
    {
        inner = outer = 0;
        bool Positive(float v) => v > 0 && !float.IsNaN(v) && !float.IsInfinity(v);
        if (!Positive(nodeMinRadius) || !Positive(nodeMaxRadius) || !Positive(nodeMinSeparation) ||
            nodes == null || nodes.Length < 2) return false;
        float largestRadius = 0;
        foreach (var node in nodes)
        {
            if (node == null || !node.TryGetContactCircle(out _, out float radius)) return false;
            largestRadius = Mathf.Max(largestRadius, radius);
        }
        // Random jitter leaves at least 60% of each angular sector between neighbouring Nodes.
        float separation = Mathf.Max(nodeMinSeparation, largestRadius * 2f + .5f);
        inner = Mathf.Max(nodeMinRadius, separation / (2f * Mathf.Sin(Mathf.PI * .6f / nodes.Length)));
        outer = Mathf.Min(nodeMaxRadius, ArenaRadius - largestRadius - .5f);
        return inner <= outer;
    }
    private void RandomizeNodes()
    {
        TryGetPlacementRange(out float inner, out float outer);
        Vector2 center = arenaBounds.transform.TransformPoint(arenaBounds.offset);
        float step = Mathf.PI * 2f / nodes.Length;
        float rotation = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < nodes.Length; i++)
        {
            float angle = rotation + step * i + UnityEngine.Random.Range(-step * .2f, step * .2f);
            float radius = Mathf.Sqrt(UnityEngine.Random.Range(inner * inner, outer * outer));
            nodes[i].transform.position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
    }
    public override void Initialize(WorldEventSpawner spawner)
    {
        base.Initialize(spawner);
        if (!TryValidateConfiguration(out string error)) { Debug.LogError(error, this); return; }
        config.TryGetSettings(out settings, out _);
        state = new OrbitalRelayState(settings, nodes.Length, previous => previous < 0
            ? UnityEngine.Random.Range(0, nodes.Length)
            : (previous + UnityEngine.Random.Range(1, nodes.Length)) % nodes.Length);
        BindPlayer();
        presentation.Bind(settings, nodes);
        presentation.Render(Snapshot);
        ShowEventMarker(markerAnchor, "event.relay.marker");
    }
    private void BindPlayer()
    {
        player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
        station = player != null ? player.GetComponentInChildren<OrbitalStationRuntime>(true) : null;
    }
    private bool ReadyToStart => state != null && station != null && station.IsInitialized && station.Modules.Count > 0 &&
        (owner != null && owner.IsRewardDeliverySuppressed(this) ||
            CurrencyManager.Instance != null && UpgradeManager.Instance != null && UpgradeManager.Instance.CanAcceptWorldEventReward);
    protected override bool CanStartFrom(Vector2 position)
    {
        if (player == null || station == null) BindPlayer();
        return ReadyToStart && Vector2.Distance(position, startArea.transform.TransformPoint(startArea.offset)) <=
            startArea.radius * Mathf.Abs(startArea.transform.lossyScale.x);
    }
    protected override void OnEventStarted()
    {
        BindPlayer();
        if (!ReadyToStart) { RunMessageService.Instance?.ShowCustom("event.relay.failed", "event.relay.unavailable"); FailEvent(); return; }
        RandomizeNodes(); startArea.enabled = false; HideEventMarker();
        state.Start(); PhaseChanged?.Invoke(Snapshot.Phase); presentation.Render(Snapshot);
    }
    private void LateUpdate()
    {
        if (IsCompleted || state == null) return;
        if (player == null || station == null) BindPlayer();
        bool wasInStartZone = IsPlayerInStartZone;
        IsPlayerInside = player != null && Vector2.Distance(player.position, arenaBounds.transform.TransformPoint(arenaBounds.offset)) <= ArenaRadius;
        IsPlayerInStartZone = player != null && Vector2.Distance(player.position, startArea.transform.TransformPoint(startArea.offset)) <=
            startArea.radius * Mathf.Abs(startArea.transform.lossyScale.x);
        if (!wasInStartZone && IsPlayerInStartZone) PlayerEntered?.Invoke();
        presentation.ShowStartPrompt(!IsStarted && CanInteract);
        if (!IsStarted || Time.timeScale <= 0) return;
        var before = Snapshot;
        bool contact = false;
        if (before.Phase != OrbitalRelayPhase.Transition && IsPlayerInside && station != null && station.IsInitialized && !station.Owner.IsDead && ActiveNode != null &&
            ActiveNode.TryGetContactCircle(out Vector2 center, out float radius))
            foreach (var module in station.Modules)
                if (module.HasBodyContact(center, radius)) { contact = true; break; }
        state.Tick(Time.deltaTime, contact);
        var after = Snapshot;
        if (after.StabilizationActivations + after.BonusActivations > before.StabilizationActivations + before.BonusActivations)
            nodes[before.ActiveNodeIndex].PlayActivation();
        ApplyPhaseChange(before.Phase);
        presentation.Render(after);
        if (state.Result.HasValue) Finish();
    }
    private void ApplyPhaseChange(OrbitalRelayPhase previous)
    {
        if (Snapshot.Phase == previous) return;
        if (Snapshot.Phase == OrbitalRelayPhase.Transition) presentation.PlayTransition();
        pressure.SetBonusActive(owner, this, Snapshot.Phase == OrbitalRelayPhase.Bonus, settings.BonusEnemyPressureMultiplier);
        PhaseChanged?.Invoke(Snapshot.Phase);
    }
    private void Finish()
    {
        if (IsCompleted || !state.Result.HasValue) return;
        OrbitalRelayResult result = state.Result.Value;
        pressure.Release(); presentation.ShowResult(result); Finished?.Invoke(result);
        if (result.Success) CompleteEvent(); else { FailEvent(); Destroy(gameObject); }
    }
    public override void Cancel()
    {
        if (state == null || IsCompleted) return;
        OrbitalRelayPhase before = Snapshot.Phase; state.CancelGameplay(); ApplyPhaseChange(before); Finish();
    }
    protected override void CleanupEvent()
    {
        pressure?.Release();
        state?.DisposeWithoutRewards();
        presentation?.Clear();
    }
    private void OnDisable() { if (!IsCompleted) DisposeForOwnerReset(); pressure?.Release(); }
    public override void CollectTacticalMapMarkers(List<TacticalMapMarkerDescriptor> markers)
    {
        base.CollectTacticalMapMarkers(markers);
        if (markers != null && IsStarted && !IsCompleted && ActiveNode != null)
            markers.Add(new TacticalMapMarkerDescriptor(TacticalMapMarkerKind.Target, ActiveNode.transform.position));
    }
}
