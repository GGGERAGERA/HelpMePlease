using System.Collections;
using System.Collections.Generic;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using Random = GameplayRandom.EventRandom;

[RequireComponent(typeof(CircleCollider2D))]
public sealed class FalseSignalEvent : WorldEvent
{
    [Header("Authored presentation")]
    [SerializeField] private FalseSignalPoint signalPointPrefab;
    [SerializeField] private GameObject startVisual;
    [Header("Placement")]
    [SerializeField, Min(1f)] private float minimumPointRadius = 4.5f;
    [SerializeField, Min(1f)] private float maximumPointRadius = 6f;
    [SerializeField, Min(1f)] private float minimumPointSeparation = 4.5f;
    [SerializeField, Min(1)] private int placementAttempts = 32;
    [Header("Timing")]
    [SerializeField, Min(.1f)] private float scanDuration = 2.4f;
    [SerializeField, Min(.1f)] private float trapWarningDuration = .65f;
    [SerializeField, Min(.1f)] private float activationDuration = .35f;
    [SerializeField, Min(1f)] private float timeLimit = 90f;
    [Header("Anomaly when no existing zone covers the event")]
    [SerializeField] private LocalAnomalyData anomaly;
    [SerializeField] private Vector2 anomalySize = new(5, 5);
    [Header("Bounded false activation wave")]
    [SerializeField, Min(0)] private int falseSignalEnemyCount = 3;
    [SerializeField, Min(0f)] private float minimumEnemyDistanceFromPlayer = 4f;
    [SerializeField, Min(0f)] private float minimumSpawnRadius = 3f;
    [SerializeField, Min(0f)] private float maximumSpawnRadius = 6f;

    private readonly List<FalseSignalPoint> signalPoints = new();
    private readonly List<LevelAnomalyController.LocalAnomalyZoneGeometry> zones = new();
    private readonly Vector3[] positions = new Vector3[3];
    private OrbitalStationRuntime station;
    private PlayerHealth health;
    private LevelAnomalyController anomalies;
    private LocalAnomalyZone ownedZone;
    private Vector2 diagnosticTarget;
    private int realSignalIndex;
    private float remaining;
    private bool completionPending;
    private Vector3 rewardPosition;
    public override Vector3 RewardPosition => completionPending ? rewardPosition : base.RewardPosition;
    internal bool AcceptsSignals => IsStarted && !IsCompleted && !completionPending && health != null && !health.IsDead;

    public override bool TryValidateConfiguration(out string error)
    {
        error = "False Signal requires an authored transmitter, start visual, anomaly and finite placement/timing settings.";
        if (signalPointPrefab == null || !signalPointPrefab.IsValid || startVisual == null || anomaly == null ||
            minimumPointRadius <= 0 || maximumPointRadius < minimumPointRadius || minimumPointSeparation <= 1.4f ||
            placementAttempts <= 0 || scanDuration <= 0 || trapWarningDuration <= 0 || activationDuration <= 0 || timeLimit <= scanDuration)
            return false;
        error = null; return true;
    }
    public override bool TryPreparePlacement(WorldEventPlacementContext context, out string error)
    {
        for (int attempt = 0; attempt < placementAttempts; attempt++)
        {
            float rotation = Random.Range(0f, Mathf.PI * 2);
            bool valid = true;
            for (int i = 0; i < positions.Length; i++)
            {
                float angle = rotation + i * Mathf.PI * 2 / positions.Length + Random.Range(-.15f, .15f);
                positions[i] = context.Origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(minimumPointRadius, maximumPointRadius);
                var footprint = new Rect((Vector2)positions[i] - Vector2.one, Vector2.one * 2);
                if (!context.PlayableArea.Contains(footprint.min) || !context.PlayableArea.Contains(footprint.max) ||
                    (context.SiteStartBounds.HasValue && (!context.SiteStartBounds.Value.Contains(footprint.min) ||
                    !context.SiteStartBounds.Value.Contains(footprint.max))) || !context.IsStaticFootprintClear(footprint)) valid = false;
                for (int j = 0; j < i; j++)
                    if (Vector2.Distance(positions[i], positions[j]) < minimumPointSeparation) valid = false;
            }
            var hub = new Rect(context.Origin - anomalySize * .5f, anomalySize);
            if (valid && context.PlayableArea.Contains(hub.min) && context.PlayableArea.Contains(hub.max) && context.IsStaticFootprintClear(hub))
            { error = null; return true; }
        }
        error = "No clear, separated three-transmitter layout fits this event location."; return false;
    }
    public override void CollectReservedFootprints(List<Rect> footprints)
    {
        if (IsCompleted) return;
        foreach (var point in positions) footprints.Add(new Rect((Vector2)point - Vector2.one, Vector2.one * 2));
    }
    public override void Initialize(WorldEventSpawner spawner)
    {
        base.Initialize(spawner);
        realSignalIndex = Random.Range(0, 3);
        var player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
        station = player != null ? player.GetComponentInChildren<OrbitalStationRuntime>(true) : null;
        health = player != null ? player.GetComponent<PlayerHealth>() : null;
        if (health != null) health.Died += OnPlayerDied;
        ShowEventMarker(transform, "event.false");
    }
    protected override bool CanStartFrom(Vector2 position) => health != null && !health.IsDead &&
        station != null && station.IsInitialized && station.Modules.Count > 0 && GetComponent<CircleCollider2D>().OverlapPoint(position);
    protected override void OnEventStarted()
    {
        GetComponent<CircleCollider2D>().enabled = false;
        startVisual.SetActive(false); HideEventMarker();
        remaining = timeLimit;
        anomalies = LevelAnomalyController.Instance;
        if (!BindAnomaly()) { FailEvent(); return; }
        for (int i = 0; i < positions.Length; i++)
        {
            var point = Instantiate(signalPointPrefab, positions[i], Quaternion.identity, transform);
            point.Initialize(this, i == realSignalIndex, diagnosticTarget, scanDuration);
            signalPoints.Add(point);
        }
    }
    private bool BindAnomaly()
    {
        if (anomalies == null) return false;
        anomalies.CollectActiveLocalZones(zones);
        // Reuse the zone that actually covers the hub; no duplicate gameplay effects.
        foreach (var zone in zones)
        {
            var bounds = new Rect(zone.Center - zone.Size * .5f, zone.Size);
            if (!bounds.Contains(transform.position)) continue;
            diagnosticTarget = new Vector2(Mathf.Clamp(transform.position.x, bounds.xMin + .5f, bounds.xMax - .5f),
                Mathf.Clamp(transform.position.y, bounds.yMin + .5f, bounds.yMax - .5f));
            return true;
        }
        ownedZone = anomalies.SpawnSiteZone(anomaly, transform.position, anomalySize, transform);
        diagnosticTarget = transform.position;
        return ownedZone != null;
    }
    private void LateUpdate()
    {
        if (!AcceptsSignals || Time.deltaTime <= 0) return;
        remaining -= Time.deltaTime;
        if (remaining <= 0) { FailEvent(); return; }
        if (station == null || !station.IsInitialized || station.Owner.IsDead) return;
        foreach (var point in signalPoints)
        {
            if (point == null || point.State != FalseSignalPointState.Unchecked) continue;
            point.GetContactCircle(out var center, out float radius);
            foreach (var module in station.Modules)
                if (module.HasBodyContact(center, radius)) { point.BeginScan(); break; }
        }
    }
    internal void ActivateSignal(FalseSignalPoint point)
    {
        if (!AcceptsSignals || !signalPoints.Contains(point)) return;
        if (point.IsReal)
        {
            completionPending = true; rewardPosition = point.transform.position;
            StartCoroutine(CompleteAfterActivation());
        }
        else StartCoroutine(TrapAfterWarning(point));
    }
    private IEnumerator CompleteAfterActivation()
    {
        AudioService.Instance?.PlayAt(AudioCueId.CoreCascade, rewardPosition);
        yield return new WaitForSeconds(activationDuration);
        if (!IsCompleted) CompleteEvent();
    }
    private IEnumerator TrapAfterWarning(FalseSignalPoint point)
    {
        AudioService.Instance?.PlayAt(AudioCueId.CorePulse, point.transform.position);
        yield return new WaitForSeconds(trapWarningDuration);
        if (!AcceptsSignals || point == null) yield break;
        point.DisableSignal();
        owner.EnemySpawner?.SpawnAdditionalWave(point.transform.position, falseSignalEnemyCount,
            minimumSpawnRadius, maximumSpawnRadius, minimumEnemyDistanceFromPlayer);
        RunMessageService.Instance?.ShowWorldEventFeedback("event.falseTitle", "event.ambush", new Color(1, .2f, .1f), .45f);
        CameraShake.Instance?.Shake(.18f, .06f);
    }
    private void OnPlayerDied() { if (!IsCompleted) FailEvent(); }
    protected override void CleanupEvent()
    {
        StopAllCoroutines();
        if (health != null) health.Died -= OnPlayerDied;
        foreach (var point in signalPoints)
            if (point != null) { point.gameObject.SetActive(false); Destroy(point.gameObject); }
        signalPoints.Clear();
        if (ownedZone != null) anomalies?.CollapseSiteZone(ownedZone);
        ownedZone = null;
        if (startVisual != null) startVisual.SetActive(false);
        GetComponent<CircleCollider2D>().enabled = false;
    }
    private void OnDisable() { if (!IsCompleted) DisposeForOwnerReset(); }
    public override void CollectTacticalMapMarkers(List<TacticalMapMarkerDescriptor> result)
    {
        base.CollectTacticalMapMarkers(result);
        if (result == null || !IsStarted || IsCompleted) return;
        foreach (var point in signalPoints)
            if (point != null && point.State != FalseSignalPointState.Disabled)
                result.Add(new TacticalMapMarkerDescriptor(TacticalMapMarkerKind.Objective, point.transform.position));
    }
}
