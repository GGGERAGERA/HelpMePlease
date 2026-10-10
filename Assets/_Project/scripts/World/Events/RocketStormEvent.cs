using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public sealed class RocketStormEvent : WorldEvent, IWorldEventObjectiveProvider
{
    [Serializable]
    public sealed class Settings
    {
        public Vector2 zoneSize = new(24f, 20f);
        [Min(1f)] public float duration = 30f;
        [Min(.5f)] public float warningTime = 2f;
        [Min(.5f)] public float waveInterval = 3.5f;
        [Range(.5f, 1.5f)] public float rocketDensity = 1f;
        [Min(.1f)] public float chaseDuration = 4f;
        [Min(.1f)] public float chaseRocketInterval = .65f;
        [Min(0f)] public float phasePause = 1f;
        public Settings Snapshot() => (Settings)MemberwiseClone();
    }

    [Header("Rocket Storm")]
    [SerializeField] private Settings settings = new();
    [SerializeField] private GameObject rocketPrefab, markerPrefab;
    [SerializeField] private ParticleSystem explosionPrefab;
    [Header("Authored zone and launch beacon")]
    [SerializeField] private CircleCollider2D startArea;
    [SerializeField] private Transform arenaVisual;
    [SerializeField] private SpriteRenderer floor;
    [SerializeField] private SpriteRenderer[] borders;
    [SerializeField] private BoxCollider2D[] walls;
    [SerializeField] private GameObject interactionArrow, launchArea;
    [SerializeField] private TMP_Text prompt;

    private const float Radius = 2f, FallTime = .4f, Preparation = 2f;
    private PlayerHealth health;
    private CharacterMovement2D movement;
    private Transform player;
    private RocketAttackRunner runner;
    private Rect arena;
    private Vector2 center, halfSize, reachableSafe;
    private enum Phase { Preparation, MassStrike, RestBeforeChase, Chase, RestBeforeMass, Draining }
    private Phase phase;
    private float elapsed, phaseAt, chaseUntil, nextChaseShot, lastMassAt, finishAt, clearance;
    private int wave, displayedSecond = -1;
    public Action<string> Finished;
    public bool IsRunning => IsStarted && !IsCompleted;
    public int PendingRockets => runner?.PendingCount ?? 0;
    public int WavesLaunched => wave;
    public string Status { get; private set; } = "Подойдите к маяку и нажмите E";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void SetLabSettings(Settings tuning) => settings = tuning.Snapshot();
#endif

    public override bool TryValidateConfiguration(out string error)
    {
        error = "Rocket Storm requires rocket FX, an authored zone, beacon, prompt and positive finite timing/size settings.";
        if (rocketPrefab == null || markerPrefab == null || explosionPrefab == null || startArea == null ||
            arenaVisual == null || floor == null || interactionArrow == null || launchArea == null || prompt == null ||
            borders == null || borders.Length != 4 || walls == null || walls.Length != 4 ||
            settings == null || !Valid(settings.zoneSize.x, 12) || !Valid(settings.zoneSize.y, 12) ||
            !Valid(settings.duration, 1) || !Valid(settings.warningTime, .5f) || !Valid(settings.waveInterval, .5f) ||
            !Valid(settings.chaseDuration, .1f) || !Valid(settings.chaseRocketInterval, .1f) || !Valid(settings.phasePause, 0) ||
            !Valid(settings.rocketDensity, .5f) || settings.rocketDensity > 1.5f) return false;
        foreach (var item in borders) if (item == null) return false;
        foreach (var item in walls) if (item == null || item.isTrigger) return false;
        error = null; return true;
    }
    private static bool Valid(float value, float minimum) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum;

    public override bool TryPreparePlacement(WorldEventPlacementContext context, out string error)
    {
        Rect available = context.PlayableArea;
        if (context.SiteStartBounds.HasValue)
        {
            Rect site = context.SiteStartBounds.Value;
            available = Rect.MinMaxRect(Mathf.Max(available.xMin, site.xMin), Mathf.Max(available.yMin, site.yMin),
                Mathf.Min(available.xMax, site.xMax), Mathf.Min(available.yMax, site.yMax));
        }
        // Keep the whole survival area, including room for the player's body, away from territorial edges/walls.
        Vector2 size = Vector2.Min(settings.zoneSize, available.size - Vector2.one * 2f);
        if (size.x < 12f || size.y < 12f) { error = "No accessible 12 x 12 Rocket Storm area fits this territory."; return false; }
        Vector2 half = size * .5f;
        Vector2 position = new(Mathf.Clamp(context.Origin.x, available.xMin + half.x + 1f, available.xMax - half.x - 1f),
            Mathf.Clamp(context.Origin.y, available.yMin + half.y + 1f, available.yMax - half.y - 1f));
        Rect candidate = new(position - half, size);
        if (!context.IsStaticFootprintClear(candidate)) { error = "Rocket Storm survival area intersects an obstacle or reserved exit."; return false; }
        arena = candidate; center = position; halfSize = half;
        transform.position = position;
        RenderZone(false);
        error = null; return true;
    }

    public override void CollectReservedFootprints(List<Rect> footprints)
    {
        if (!IsCompleted) footprints.Add(arena);
    }

    public override void Initialize(WorldEventSpawner spawner)
    {
        base.Initialize(spawner);
        player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
        health = player != null ? player.GetComponent<PlayerHealth>() : null;
        movement = player != null ? player.GetComponent<CharacterMovement2D>() : null;
        if (health == null || movement == null) { FailEvent(); return; }
        health.Died += PlayerDied;
        clearance = .5f;
        foreach (var collider in player.GetComponentsInChildren<Collider2D>())
            if (!collider.isTrigger) clearance = Mathf.Max(clearance, ((Vector2)collider.bounds.extents).magnitude);
        PrepareRocketPools();
        RenderZone(false);
    }

    private void PrepareRocketPools()
    {
        float spacing = Radius * 1.3f / settings.rocketDensity;
        int grid = (Mathf.CeilToInt(arena.width / spacing) + 1) * (Mathf.CeilToInt(arena.height / spacing) + 1);
        int chase = Mathf.CeilToInt(Mathf.Min(settings.chaseDuration, settings.duration) / settings.chaseRocketInterval) + 1;
        int shots = Mathf.Max(grid, chase);
        float interval = Mathf.Max(settings.warningTime + .3f, settings.waveInterval);
        int impacts = shots * (Mathf.CeilToInt(explosionPrefab.main.duration / interval) + 1);
        // Prepare before interaction; retain a full wave and use the existing particle warnings without CPU raster meshes.
        runner = new RocketAttackRunner(this, rocketPrefab, markerPrefab, explosionPrefab, shots, impacts, pixelWarnings: false);
    }

    protected override bool CanStartFrom(Vector2 position) => health != null && !health.IsDead && startArea.OverlapPoint(position);

    protected override void OnEventStarted()
    {
        // Match Corridor: instance-owned walls block the player's physical layers, leaving enemies free to enter.
        int playerLayers = 0;
        foreach (var collider in player.GetComponentsInChildren<Collider2D>(true))
            if (!collider.isTrigger) playerLayers |= 1 << collider.gameObject.layer;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (playerLayers == 0 || (enemyLayer >= 0 && (playerLayers & (1 << enemyLayer)) != 0))
        { Cancel(); return; }
        foreach (var wall in walls)
        {
            wall.includeLayers = playerLayers;
            wall.excludeLayers = ~playerLayers;
            wall.layerOverridePriority = 100;
            wall.enabled = true;
        }
        elapsed = 0; wave = 0; phase = Phase.Preparation; phaseAt = Preparation;
        chaseUntil = nextChaseShot = lastMassAt = 0; finishAt = settings.duration;
        startArea.enabled = false;
        RenderZone(true);
        Status = "Подготовка";
    }

    private bool AttemptAllowed()
    {
        return IsRunning && health != null && !health.IsDead && health.isActiveAndEnabled;
    }

    private void Update()
    {
        if (!IsRunning) return;
        if (!AttemptAllowed()) { Finish(false, "Попытка прекращена"); return; }
        elapsed += Time.deltaTime;
        runner.Tick(Time.deltaTime, 16f, Radius, 25, AttemptAllowed);
        if (!AttemptAllowed()) return; // Death callbacks clean up synchronously inside the runner.
        if (elapsed >= finishAt) phase = Phase.Draining;
        switch (phase)
        {
            case Phase.Preparation:
            case Phase.RestBeforeMass:
                if (elapsed < phaseAt) break;
                LaunchWave(settings.warningTime); lastMassAt = elapsed; phase = Phase.MassStrike;
                break;
            case Phase.MassStrike:
                if (runner.PendingCount != 0) break;
                phase = Phase.RestBeforeChase; phaseAt = elapsed + settings.phasePause; Status = "Передышка";
                break;
            case Phase.RestBeforeChase:
                if (elapsed < phaseAt) break;
                phase = Phase.Chase; chaseUntil = elapsed + settings.chaseDuration; nextChaseShot = elapsed;
                LaunchChaseRocket();
                break;
            case Phase.Chase:
                if (elapsed < chaseUntil && nextChaseShot < chaseUntil)
                {
                    if (elapsed >= nextChaseShot) LaunchChaseRocket();
                    break;
                }
                if (runner.PendingCount != 0) break;
                phase = Phase.RestBeforeMass;
                phaseAt = Mathf.Max(elapsed + settings.phasePause, lastMassAt + settings.waveInterval); Status = "Передышка";
                break;
            case Phase.Draining:
                Status = "Последние ракеты";
                break;
        }
        int second = Mathf.CeilToInt(Mathf.Max(0, finishAt - elapsed));
        if (second != displayedSecond)
        {
            displayedSecond = second;
            prompt.text = second > 0 ? $"Переживите обстрел · {second} с" : "Переживите последние взрывы";
        }
        if (phase == Phase.Draining && runner.PendingCount == 0) Finish(true, "Обстрел пережит!");
    }

    private void LaunchChaseRocket()
    {
        nextChaseShot = elapsed + settings.chaseRocketInterval;
        if (!movement.enabled || movement.WalkingSpeed <= 0f || movement.WalkingAcceleration <= 0f) return;
        float walkWarning = .25f + (Radius + clearance + .2f) / movement.WalkingSpeed +
            movement.WalkingSpeed / (2f * movement.WalkingAcceleration);
        float warning = Mathf.Max(.5f, settings.warningTime * .5f, walkWarning);
        float fallTime = FallTime * .5f;
        Vector2 intent = movement.HasMovementInput ? movement.LastMoveDirection : Vector2.zero;
        // One immutable target per marker. Subsequent rockets forecast the next processed input snapshot.
        Vector2 target = (Vector2)player.position + movement.ForecastWalkingDisplacement(intent, warning);
        runner.Launch(target, null, warning - fallTime, fallTime, Radius);
        Status = "Погоня";
    }

    private void LaunchWave(float warning)
    {
        int pattern = wave % 6;
        float safeRadius = 1.8f;
        Vector2 p0 = (Vector2)player.position - center;
        Vector2 desired;
        if (pattern < 3) desired = new Vector2((pattern - 1) * halfSize.x * .5f, p0.y);
        else if (pattern == 3) desired = Vector2.Dot(p0, halfSize) < 0 ? -halfSize * .5f : halfSize * .5f;
        else if (pattern == 4) desired = Vector2.zero;
        else desired = new Vector2(p0.x < 0 ? -halfSize.x + safeRadius : halfSize.x - safeRadius, p0.y);
        desired.x = Mathf.Clamp(desired.x, -halfSize.x + safeRadius, halfSize.x - safeRadius);
        desired.y = Mathf.Clamp(desired.y, -halfSize.y + safeRadius, halfSize.y - safeRadius);
        Vector2 direction = (desired - p0).normalized;
        float budget = Mathf.Max(0, Vector2.Dot(movement.ForecastWalkingDisplacement(direction, warning * .5f), direction));
        reachableSafe = Vector2.MoveTowards(p0, desired, budget);
        float exclusion = safeRadius + Radius + clearance;
        float spacing = Radius * 1.3f / settings.rocketDensity;
        int columns = Mathf.CeilToInt(halfSize.x * 2 / spacing), rows = Mathf.CeilToInt(halfSize.y * 2 / spacing);
        for (int x = 0; x <= columns; x++)
        for (int y = 0; y <= rows; y++)
        {
            Vector2 p = new(-halfSize.x + x * halfSize.x * 2 / columns, -halfSize.y + y * halfSize.y * 2 / rows);
            if (Vector2.Distance(p, reachableSafe) <= exclusion) continue;
            if (pattern < 3 && Mathf.Abs(p.x - desired.x) <= exclusion) continue;
            if (pattern == 3 && (Vector2.Distance(p, halfSize * .5f) <= exclusion || Vector2.Distance(p, -halfSize * .5f) <= exclusion)) continue;
            if (pattern == 4 && p.magnitude <= exclusion) continue;
            if (pattern == 5 && Mathf.Abs(p.x) >= halfSize.x - exclusion) continue;
            runner.Launch(center + p, null, warning - FallTime, FallTime, Radius);
        }
        wave++; Status = "Массовый обстрел";
    }

    private void RenderZone(bool active)
    {
        arenaVisual.localPosition = Vector3.zero;
        SetSize(floor, arena.size);
        floor.color = active ? new Color(.4f, .1f, .08f, .12f) : new Color(.1f, .4f, .45f, .09f);
        Color color = active ? new Color(1f, .4f, .2f, .9f) : new Color(.65f, .9f, 1f, .8f);
        for (int i = 0; i < 4; i++)
        {
            bool horizontal = i < 2;
            Vector2 position = horizontal ? new Vector2(0, (i == 0 ? -1 : 1) * halfSize.y) : new Vector2((i == 2 ? -1 : 1) * halfSize.x, 0);
            float length = (horizontal ? arena.width : arena.height) + .32f;
            borders[i].transform.localPosition = position;
            borders[i].transform.localRotation = Quaternion.Euler(0, 0, horizontal ? 0 : 90);
            borders[i].size = new Vector2(length, .6f);
            walls[i].size = new Vector2(length, .32f);
            borders[i].color = color;
        }
        interactionArrow.SetActive(!active); launchArea.SetActive(!active);
        prompt.text = active ? $"Переживите обстрел · {Mathf.CeilToInt(settings.duration)} с" : "[E] Начать обстрел";
    }

    private static void SetSize(SpriteRenderer sprite, Vector2 size) => sprite.transform.localScale =
        new Vector3(size.x / sprite.sprite.bounds.size.x, size.y / sprite.sprite.bounds.size.y, 1);

    private void PlayerDied() { if (!IsCompleted) Finish(false, "Попытка прекращена: вы погибли"); }
    private void Finish(bool success, string message)
    {
        if (IsCompleted) return;
        Status = message;
        Finished?.Invoke(message);
        RunMessageService.Instance?.ShowCustom(success ? "Ракетный шторм: успех" : "Ракетный шторм", message);
        if (success) CompleteEvent(); else FailEvent();
    }
    public override void Cancel() => Finish(false, "Попытка прекращена");
    protected override void CleanupEvent()
    {
        if (health != null) health.Died -= PlayerDied;
        foreach (var wall in walls) wall.enabled = false;
        phaseAt = chaseUntil = nextChaseShot = 0;
        runner?.Dispose(); runner = null;
        interactionArrow.SetActive(false);
        Finished = null;
    }
    private void OnDisable() { if (!IsCompleted) DisposeForOwnerReset(); }
    public WorldEventObjectiveGuidance GetObjectiveGuidance() => new("Переживите обстрел", "Оставайтесь внутри отмеченной области",
        center, Mathf.FloorToInt(Mathf.Min(elapsed, settings.duration)), Mathf.CeilToInt(settings.duration));
}
