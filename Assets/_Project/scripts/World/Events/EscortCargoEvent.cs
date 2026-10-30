using System.Collections.Generic;
using UnityEngine;

/// <summary>Closed experiment: escort a cargo along one prevalidated straight route.</summary>
[RequireComponent(typeof(CircleCollider2D))]
public sealed class EscortCargoEvent : WorldEvent, IWorldEventObjectiveProvider
{
    [Header("Authored presentation")]
    [SerializeField] private Transform cargo;
    [SerializeField] private Transform destinationVisual;
    [Header("Short straight route (experimental tuning)")]
    [SerializeField, Min(1f)] private float routeLength = 10f;
    [SerializeField, Min(.1f)] private float routeClearance = 2f;
    [SerializeField, Min(.1f)] private float cargoSpeed = 1f;
    [SerializeField, Min(.1f)] private float escortRadius = 3f;
    [SerializeField, Min(1f)] private float timeLimit = 30f;
    [SerializeField, Min(1)] private int placementAttempts = 16;

    private Transform player;
    private PlayerHealth health;
    private CircleCollider2D startTrigger;
    private Vector3 destination;
    private Rect reservedRoute;
    private bool placementReady;
    private float remaining;

    public override Vector3 RewardPosition => destination;
    public float RemainingSeconds => remaining;
    public bool IsEscorted => IsStarted && !IsCompleted && player != null &&
        health != null && !health.IsDead && cargo != null &&
        ((Vector2)(player.position - cargo.position)).sqrMagnitude <= escortRadius * escortRadius;

    public override bool TryValidateConfiguration(out string error)
    {
        error = "Escort Cargo requires two distinct authored child visuals, a trigger, finite positive route/timing settings and time to finish.";
        var trigger = GetComponent<CircleCollider2D>();
        if (cargo == null || destinationVisual == null || cargo == destinationVisual ||
            cargo == transform || destinationVisual == transform ||
            !cargo.IsChildOf(transform) || !destinationVisual.IsChildOf(transform) ||
            cargo.IsChildOf(destinationVisual) || destinationVisual.IsChildOf(cargo) ||
            cargo.GetComponentInChildren<SpriteRenderer>(true) == null ||
            destinationVisual.GetComponentInChildren<SpriteRenderer>(true) == null ||
            trigger == null || !trigger.isTrigger || !PositiveFinite(routeLength) ||
            !PositiveFinite(routeClearance) || !PositiveFinite(cargoSpeed) ||
            !PositiveFinite(escortRadius) || !PositiveFinite(timeLimit) ||
            timeLimit <= routeLength / cargoSpeed || placementAttempts < 1)
            return false;
        error = null;
        return true;
    }

    private static bool PositiveFinite(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);

    public override bool TryPreparePlacement(WorldEventPlacementContext context, out string error)
    {
        placementReady = false;
        if (!TryValidateConfiguration(out error)) return false;
        var random = new System.Random(context.Seed);
        float initialAngle = (float)random.NextDouble() * Mathf.PI * 2;
        for (int i = 0; i < placementAttempts; i++)
        {
            float angle = initialAngle + i * Mathf.PI * 2 / placementAttempts;
            Vector2 end = context.Origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * routeLength;
            // One conservative swept AABB covers the entire segment AND both end pads.
            Vector2 min = Vector2.Min(context.Origin, end) - Vector2.one * routeClearance;
            Vector2 max = Vector2.Max(context.Origin, end) + Vector2.one * routeClearance;
            var footprint = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            if (!Contains(context.PlayableArea, footprint) ||
                (context.SiteStartBounds.HasValue && !Contains(context.SiteStartBounds.Value, footprint)) ||
                !context.IsStaticFootprintClear(footprint)) continue;
            reservedRoute = footprint;
            destination = new Vector3(end.x, end.y, transform.position.z);
            cargo.position = new Vector3(context.Origin.x, context.Origin.y, transform.position.z);
            destinationVisual.position = destination;
            placementReady = true;
            error = null;
            return true;
        }
        error = "No clear straight cargo route and end pads fit playable/site bounds without blocking static footprints or exits.";
        return false;
    }

    private static bool Contains(Rect bounds, Rect footprint) =>
        bounds.Contains(footprint.min) && bounds.Contains(footprint.max);

    public override void CollectReservedFootprints(List<Rect> footprints)
    {
        if (footprints != null && placementReady && !IsCompleted) footprints.Add(reservedRoute);
    }

    public override void Initialize(WorldEventSpawner spawner)
    {
        base.Initialize(spawner);
        startTrigger = GetComponent<CircleCollider2D>();
        player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
        health = player != null ? player.GetComponent<PlayerHealth>() : null;
        remaining = timeLimit;
        if (health != null) health.Died += OnPlayerDied;
        if (!placementReady || health == null || health.IsDead) { DisposeForOwnerReset(); return; }
        cargo.gameObject.SetActive(true);
        destinationVisual.gameObject.SetActive(true);
        startTrigger.enabled = true;
    }

    protected override bool CanStartFrom(Vector2 position) => placementReady &&
        health != null && !health.IsDead && startTrigger != null && startTrigger.OverlapPoint(position);

    protected override void OnEventStarted()
    {
        if (!placementReady || health == null || health.IsDead) { FailEvent(); return; }
        startTrigger.enabled = false;
        remaining = timeLimit;
    }

    private void Update()
    {
        if (!IsStarted || IsCompleted) return;
        if (player == null || health == null || health.IsDead || cargo == null)
        { FailEvent(); return; }
        float elapsed = Time.deltaTime;
        if (elapsed <= 0) return;
        // The deadline keeps running even while the player retreats outside escort range.
        remaining = Mathf.Max(0, remaining - elapsed);
        if (remaining <= 0) { FailEvent(); return; }
        if (!IsEscorted) return;
        cargo.position = Vector3.MoveTowards(cargo.position, destination, cargoSpeed * elapsed);
        if ((cargo.position - destination).sqrMagnitude <= .000001f) CompleteEvent();
    }

    private void OnPlayerDied() { if (!IsCompleted) FailEvent(); }

    protected override void CleanupEvent()
    {
        if (health != null) health.Died -= OnPlayerDied;
        health = null;
        player = null;
        if (startTrigger != null) startTrigger.enabled = false;
        if (cargo != null) cargo.gameObject.SetActive(false);
        if (destinationVisual != null) destinationVisual.gameObject.SetActive(false);
    }

    private void OnDisable() { if (!IsCompleted) DisposeForOwnerReset(); }

    public WorldEventObjectiveGuidance GetObjectiveGuidance()
    {
        float distanceLeft = cargo != null ? Vector2.Distance(cargo.position, destination) : routeLength;
        string instruction = !IsStarted ? EventDescription :
            $"{(IsEscorted ? "Сопровождай груз" : "Вернись к грузу (3 м)")} · {Mathf.CeilToInt(remaining)} с";
        return new WorldEventObjectiveGuidance(EventDisplayName, instruction,
            cargo != null ? (Vector2)cargo.position : (Vector2)transform.position,
            Mathf.FloorToInt(Mathf.Clamp(routeLength - distanceLeft, 0, routeLength)), Mathf.CeilToInt(routeLength));
    }

    public override void CollectTacticalMapMarkers(List<TacticalMapMarkerDescriptor> markers)
    {
        if (markers == null || IsCompleted) return;
        markers.Add(new TacticalMapMarkerDescriptor(IsStarted ? TacticalMapMarkerKind.Objective : TacticalMapMarkerKind.Event,
            IsStarted && cargo != null ? cargo.position : transform.position));
        if (placementReady) markers.Add(new TacticalMapMarkerDescriptor(TacticalMapMarkerKind.Objective, destination));
    }
}
