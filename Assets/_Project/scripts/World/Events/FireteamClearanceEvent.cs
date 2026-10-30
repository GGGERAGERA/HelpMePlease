using System.Collections.Generic;
using UnityEngine;

/// <summary>Closed experiment. No public catalogue entry; requires an authored prefab and opt-in entry.</summary>
[RequireComponent(typeof(CircleCollider2D))]
public sealed class FireteamClearanceEvent : WorldEvent, IWorldEventObjectiveProvider
{
    private const int TargetCount = 3;
    private const float TimeLimit = 35f;
    [Header("Authored content")]
    [SerializeField] private GameObject shooterPrefab;
    [SerializeField] private GameObject startVisual;
    [SerializeField] private EnemySpawner enemySpawner;
    [Header("Experimental placement")]
    [SerializeField, Min(1f)] private float groupRadius = 7f;
    [SerializeField, Min(.1f)] private float targetClearance = 1f;
    [SerializeField, Min(.1f)] private float startClearance = 2.5f;
    [SerializeField, Min(1f)] private float minimumPlayerDistance = 4f;
    [SerializeField, Min(1)] private int placementAttempts = 24;

    private readonly Vector3[] positions = new Vector3[TargetCount];
    private readonly EnemyHealth[] targets = new EnemyHealth[TargetCount];
    private readonly bool[] confirmedDead = new bool[TargetCount];
    private readonly WorldEventMarker[] indicators = new WorldEventMarker[TargetCount];
    private Rect startFootprint;
    private CircleCollider2D startTrigger;
    private PlayerHealth playerHealth;
    private HUDManager indicatorHud;
    private bool placementReady;
    private bool spawning;
    private bool cleaning;
    private int kills;
    private float remaining;

    public int ConfirmedKills => kills;
    public float RemainingSeconds => remaining;

    public override bool TryValidateConfiguration(out string error)
    {
        error = "Fireteam requires an ordinary shooter prefab, authored child start sprite, trigger and finite separated placement settings.";
        var trigger = GetComponent<CircleCollider2D>();
        var health = shooterPrefab != null ? shooterPrefab.GetComponent<EnemyHealth>() : null;
        if (health == null || health.IsBoss || !shooterPrefab.activeSelf ||
            shooterPrefab.GetComponent<EnemyShooterMovement>() == null ||
            shooterPrefab.GetComponent<TurretEnemyBehaviour>() != null ||
            shooterPrefab.GetComponent<CarrierEscapeBehaviour>() != null ||
            startVisual == null || startVisual == gameObject ||
            !startVisual.transform.IsChildOf(transform) ||
            startVisual.GetComponentInChildren<SpriteRenderer>(true) == null ||
            trigger == null || !trigger.isTrigger || !PositiveFinite(trigger.radius) ||
            !PositiveFinite(groupRadius) || !PositiveFinite(targetClearance) ||
            !PositiveFinite(startClearance) || !PositiveFinite(minimumPlayerDistance) ||
            groupRadius <= startClearance + targetClearance || placementAttempts < 1)
            return false;
        error = null;
        return true;
    }

    private static bool PositiveFinite(float value) =>
        value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    private Rect TargetFootprint(Vector2 point) =>
        new Rect(point - Vector2.one * targetClearance, Vector2.one * targetClearance * 2f);
    private static bool Contains(Rect bounds, Rect footprint) =>
        bounds.Contains(footprint.min) && bounds.Contains(footprint.max);
    private static bool Fits(WorldEventPlacementContext context, Rect footprint) =>
        Contains(context.PlayableArea, footprint) &&
        (!context.SiteStartBounds.HasValue || Contains(context.SiteStartBounds.Value, footprint)) &&
        context.IsStaticFootprintClear(footprint);

    public override bool TryPreparePlacement(WorldEventPlacementContext context, out string error)
    {
        placementReady = false;
        if (!TryValidateConfiguration(out error)) return false;
        startFootprint = new Rect(context.Origin - Vector2.one * startClearance,
            Vector2.one * startClearance * 2f);
        if (!Fits(context, startFootprint))
        { error = "Fireteam start area is obstructed or outside placement bounds."; return false; }
        var random = new System.Random(context.Seed);
        float initialAngle = (float)random.NextDouble() * Mathf.PI * 2f;
        for (int attempt = 0; attempt < placementAttempts; attempt++)
        {
            float rotation = initialAngle + attempt * Mathf.PI * 2f / placementAttempts;
            bool valid = true;
            for (int i = 0; i < TargetCount; i++)
            {
                float angle = rotation + i * Mathf.PI * 2f / TargetCount;
                Vector2 point = context.Origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * groupRadius;
                positions[i] = new Vector3(point.x, point.y, transform.position.z);
                Rect footprint = TargetFootprint(point);
                if (!Fits(context, footprint) || footprint.Overlaps(startFootprint)) valid = false;
                for (int j = 0; j < i; j++)
                    if (footprint.Overlaps(TargetFootprint(positions[j]))) valid = false;
            }
            if (!valid) continue;
            placementReady = true;
            error = null;
            return true;
        }
        error = "No clear separated three-shooter layout fits the event bounds.";
        return false;
    }

    public override void CollectReservedFootprints(List<Rect> footprints)
    {
        if (footprints == null || !placementReady || IsCompleted) return;
        footprints.Add(startFootprint);
        foreach (Vector3 point in positions) footprints.Add(TargetFootprint(point));
    }

    public override void Initialize(WorldEventSpawner spawner)
    {
        base.Initialize(spawner);
        startTrigger = GetComponent<CircleCollider2D>();
        Transform player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
        playerHealth = player != null ? player.GetComponent<PlayerHealth>() : null;
        if (enemySpawner == null) enemySpawner = FindFirstObjectByType<EnemySpawner>();
        kills = 0;
        remaining = TimeLimit;
        cleaning = false;
        spawning = false;
        System.Array.Clear(confirmedDead, 0, confirmedDead.Length);
        if (!placementReady || playerHealth == null || playerHealth.IsDead || enemySpawner == null)
        { DisposeForOwnerReset(); return; }
        playerHealth.Died += OnPlayerDied;
        startTrigger.enabled = true;
        startVisual.SetActive(true);
        ShowEventMarker(transform, EventDisplayName);
    }

    protected override bool CanStartFrom(Vector2 position) => placementReady &&
        playerHealth != null && !playerHealth.IsDead && startTrigger != null && startTrigger.OverlapPoint(position);

    protected override void OnEventStarted()
    {
        startTrigger.enabled = false;
        startVisual.SetActive(false);
        HideEventMarker();
        remaining = TimeLimit;
        EnemyHealth.Despawned += OnTargetDespawned;
        indicatorHud = HUDManager.Instance;
        spawning = true;
        for (int i = 0; i < TargetCount; i++)
        {
            confirmedDead[i] = false;
            EnemyHealth target = enemySpawner.SpawnEventEnemyAt(shooterPrefab, positions[i],
                targetClearance, minimumPlayerDistance);
            // Spawn callbacks can cancel/reset the event synchronously.
            if (IsCompleted)
            {
                if (target != null && !target.IsDead) { target.gameObject.SetActive(false); Destroy(target.gameObject); }
                spawning = false;
                return;
            }
            targets[i] = target;
            if (target == null || target.IsDead || !target.isActiveAndEnabled)
            { spawning = false; FailEvent(); return; }
            target.OnDied += OnTargetDied;
            indicators[i] = indicatorHud != null
                ? indicatorHud.CreateWorldEventMarker(target.transform, string.Empty) : null;
        }
        spawning = false;
    }

    private void Update()
    {
        if (!IsStarted || IsCompleted || spawning) return;
        if (playerHealth == null || playerHealth.IsDead) { FailEvent(); return; }
        for (int i = 0; i < TargetCount; i++)
            if (!confirmedDead[i] && (targets[i] == null || !targets[i].isActiveAndEnabled))
            { FailEvent(); return; }
        remaining = Mathf.Max(0f, remaining - Time.deltaTime);
        if (remaining <= 0f) FailEvent();
    }

    private void OnTargetDied(EnemyHealth target)
    {
        if (cleaning || IsCompleted || !IsStarted) return;
        if (playerHealth == null || playerHealth.IsDead || remaining <= 0f)
        { FailEvent(); return; }
        for (int i = 0; i < TargetCount; i++)
        {
            if (!ReferenceEquals(targets[i], target) || confirmedDead[i]) continue;
            // Record before any marker/owner callback: the subsequent despawn is a death, not a lost objective.
            confirmedDead[i] = true;
            target.OnDied -= OnTargetDied;
            kills++;
            RemoveIndicator(i);
            if (kills == TargetCount && !spawning) CompleteEvent();
            return;
        }
    }

    private void OnTargetDespawned(EnemyHealth target)
    {
        if (cleaning || IsCompleted) return;
        for (int i = 0; i < TargetCount; i++)
            if (ReferenceEquals(targets[i], target) && !confirmedDead[i])
            {
                // An earlier death listener may disable the actor before our
                // OnDied callback runs. Only that callback can confirm the kill;
                // Update rejects a missing/disabled actor if confirmation never arrives.
                if (!target.IsDead) FailEvent();
                return;
            }
    }

    private void OnPlayerDied() { if (!IsCompleted) FailEvent(); }
    private void RemoveIndicator(int index)
    {
        if (indicatorHud != null && indicators[index] != null)
            indicatorHud.RemoveWorldEventMarker(indicators[index]);
        indicators[index] = null;
    }

    protected override void CleanupEvent()
    {
        cleaning = true;
        EnemyHealth.Despawned -= OnTargetDespawned;
        if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        playerHealth = null;
        if (startTrigger != null) startTrigger.enabled = false;
        if (startVisual != null) startVisual.SetActive(false);
        for (int i = 0; i < TargetCount; i++)
        {
            EnemyHealth target = targets[i];
            if (!ReferenceEquals(target, null)) target.OnDied -= OnTargetDied;
            targets[i] = null;
            RemoveIndicator(i);
            if (target == null || target.IsDead) continue;
            target.gameObject.SetActive(false);
            Destroy(target.gameObject);
        }
        indicatorHud = null;
    }

    private void OnDisable() { if (!IsCompleted) DisposeForOwnerReset(); }

    public WorldEventObjectiveGuidance GetObjectiveGuidance()
    {
        Vector2 next = transform.position;
        for (int i = 0; i < TargetCount; i++)
            if (!confirmedDead[i] && targets[i] != null) { next = targets[i].transform.position; break; }
        return new WorldEventObjectiveGuidance(EventDisplayName,
            IsStarted ? $"Уничтожь огневую группу · {kills}/{TargetCount} · {Mathf.CeilToInt(remaining)} с" : EventDescription,
            next, kills, TargetCount);
    }

    public override void CollectTacticalMapMarkers(List<TacticalMapMarkerDescriptor> markers)
    {
        if (markers == null || IsCompleted) return;
        if (!IsStarted) { base.CollectTacticalMapMarkers(markers); return; }
        for (int i = 0; i < TargetCount; i++)
            if (!confirmedDead[i] && targets[i] != null)
                markers.Add(new TacticalMapMarkerDescriptor(TacticalMapMarkerKind.Target, targets[i].transform.position));
    }
}
