using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public sealed class BeamAnomalySiteController : MonoBehaviour
{
    private enum SiteState { Stopped, Dormant, Active, Collapsing, Completed }
    private enum HazardState { Waiting, Telegraph, Firing }

    private const float SiteRadius = 10.5f;
    private const float BeamInterval = 2f;
    private const float TelegraphSeconds = 0.68f;
    private const float BeamSeconds = 0.3f;
    private const float DamageHalfWidth = 1.45f;
    private const float EnemyDamage = 5000f;
    private const float PlayerDamage = 100f;
    private const float CollapseSeconds = 0.75f;
    private const int EnemyTarget = 30;

    private static readonly Vector2[] Directions =
    {
        Vector2.right, Vector2.up,
        new Vector2(1f, 1f).normalized,
        new Vector2(1f, -1f).normalized
    };
    private static readonly float[] Offsets = { -2.5f, 2f, 0f, -3f };

    private readonly HashSet<EnemyHealth> trialEnemies = new();
    private EnemySpawner enemySpawner;
    private WorldEventSpawner eventSpawner;
    private AnomalyPowerDebugController powerController;
    private PowerTestController powerTest;
    private EvacuationCorridorEvent corridorPrefab;
    private GameObject[] enemyPrefabs;
    private Transform player;
    private PlayerHealth playerHealth;
    private float originalDamageMultiplier = 1f;
    private bool damageCaptured;
    private bool invulnerabilityRequested;
    private bool resetPlayerWhenAvailable;
    private EvacuationCorridorEvent activeEvent;
    private SiteState state = SiteState.Stopped;
    private HazardState hazardState;
    private float hazardTimer;
    private float collapseTimer;
    private int patternIndex;
    private Vector2 beamStart;
    private Vector2 beamEnd;
    private GameObject visualRoot;
    private LineRenderer boundary;
    private LineRenderer telegraph;
    private LineRenderer glow;
    private LineRenderer core;
    private string message;
    private float messageUntil;

    public void Configure(
        EnemySpawner spawner,
        WorldEventSpawner events,
        AnomalyPowerDebugController powers,
        PowerTestController test,
        GameObject[] prefabs)
    {
        enemySpawner = spawner;
        eventSpawner = events;
        powerController = powers;
        powerTest = test;
        corridorPrefab = FindCorridorPrefab(events);
        enemyPrefabs = prefabs ?? System.Array.Empty<GameObject>();
        eventSpawner.EventCompleted += HandleEventCompleted;
        eventSpawner.EventFailed += HandleEventFailed;
        BuildVisuals();
        StopSite();
    }

    public void StartOrResetSite()
    {
        powerTest?.StopTest();
        ClearEvent();
        ClearEnemies();
        powerController?.BeginBeamSiteRewardLock();
        state = SiteState.Dormant;
        visualRoot.SetActive(true);
        visualRoot.transform.localScale = Vector3.one;
        invulnerabilityRequested = true;
        resetPlayerWhenAvailable = true;
        ResolvePlayer();
        ResetHazard();
        message = string.Empty;
        messageUntil = 0f;
    }

    public void StopSite()
    {
        ClearEvent();
        ClearEnemies();
        HideHazardLines();
        powerController?.ClearBeamSiteReward();
        RestorePlayerDamage();
        state = SiteState.Stopped;
        resetPlayerWhenAvailable = false;
        if (visualRoot != null)
            visualRoot.SetActive(false);
    }

    private void Update()
    {
        ResolvePlayer();
        trialEnemies.RemoveWhere(enemy => enemy == null || enemy.IsDead);

        if (state == SiteState.Dormant && IsPlayerInside() &&
            Input.GetKeyDown(KeyCode.E))
        {
            StartTrial();
        }

        if (state == SiteState.Dormant || state == SiteState.Active)
            UpdateHazard();

        if (state == SiteState.Collapsing)
        {
            collapseTimer -= Time.deltaTime;
            float scale = Mathf.Clamp01(collapseTimer / CollapseSeconds);
            visualRoot.transform.localScale = Vector3.one * scale;
            if (collapseTimer <= 0f)
                CompleteCollapse();
        }
    }

    private void StartTrial()
    {
        if (corridorPrefab == null || eventSpawner == null)
            return;

        powerTest?.StopTest();
        Vector3 start = new(-5f, -3f, 0f);
        Vector3 end = new(5f, 3f, 0f);
        if (!eventSpawner.SpawnDebugEventAt(
                corridorPrefab,
                start,
                true,
                out WorldEvent spawned))
        {
            return;
        }

        activeEvent = spawned as EvacuationCorridorEvent;
        if (activeEvent == null)
            return;
        Vector2 path = end - start;
        activeEvent.ConfigureDebugPath(path, path.magnitude);
        EnsureEnemies(EnemyTarget);
        state = SiteState.Active;
        activeEvent.StartSelectedEvent();
    }

    private void UpdateHazard()
    {
        hazardTimer += Time.deltaTime;
        if (hazardState == HazardState.Waiting && hazardTimer >= BeamInterval)
            BeginTelegraph();
        else if (hazardState == HazardState.Telegraph &&
            hazardTimer >= TelegraphSeconds)
            FireBeam();
        else if (hazardState == HazardState.Firing &&
            hazardTimer >= BeamSeconds)
        {
            HideHazardLines();
            hazardState = HazardState.Waiting;
            hazardTimer = 0f;
        }
    }

    private void BeginTelegraph()
    {
        int index = patternIndex % Directions.Length;
        patternIndex++;
        Vector2 direction = Directions[index];
        Vector2 normal = new(-direction.y, direction.x);
        float offset = Offsets[index];
        Vector2 center = normal * offset;
        float halfLength = Mathf.Sqrt(
            Mathf.Max(0.1f, SiteRadius * SiteRadius - offset * offset)
        );
        beamStart = center - direction * halfLength;
        beamEnd = center + direction * halfLength;
        SetLine(telegraph, beamStart, beamEnd);
        telegraph.enabled = true;
        glow.enabled = false;
        core.enabled = false;
        hazardState = HazardState.Telegraph;
        hazardTimer = 0f;
    }

    private void FireBeam()
    {
        telegraph.enabled = false;
        SetLine(glow, beamStart, beamEnd);
        SetLine(core, beamStart, beamEnd);
        glow.enabled = true;
        core.enabled = true;
        hazardState = HazardState.Firing;
        hazardTimer = 0f;
        ApplyLineDamage(beamStart, beamEnd);
    }

    private void ApplyLineDamage(Vector2 start, Vector2 end)
    {
        foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
        {
            if (enemy == null || enemy.IsDead)
                continue;
            Vector2 position = enemy.transform.position;
            if (DistanceToSegment(position, start, end) <= DamageHalfWidth)
                enemy.TakeDamage(EnemyDamage, position, false);
        }

        if (playerHealth != null && !playerHealth.IsDead &&
            DistanceToSegment(player.position, start, end) <= DamageHalfWidth)
        {
            Vector2 nearest = ClosestPoint(player.position, start, end);
            playerHealth.TakeDamage(
                PlayerDamage,
                (Vector2)player.position - nearest
            );
        }
    }

    private void HandleEventCompleted(WorldEvent worldEvent)
    {
        if (activeEvent == null || worldEvent != activeEvent)
            return;
        activeEvent = null;
        HideHazardLines();
        state = SiteState.Collapsing;
        collapseTimer = CollapseSeconds;
    }

    private void HandleEventFailed(WorldEvent worldEvent)
    {
        if (activeEvent == null || worldEvent != activeEvent)
            return;
        activeEvent = null;
        ClearEnemies();
        state = SiteState.Dormant;
        ResetHazard();
    }

    private void CompleteCollapse()
    {
        state = SiteState.Completed;
        visualRoot.SetActive(false);
        powerController?.GrantRedBeamFromSite();
        message = "BEAM ANOMALY CONQUERED\nRED BEAM ACQUIRED";
        messageUntil = Time.unscaledTime + 3f;
    }

    private void EnsureEnemies(int target)
    {
        if (enemySpawner == null || enemyPrefabs.Length == 0)
            return;
        int attempts = Mathf.Max(0, target - trialEnemies.Count) * 3;
        for (int i = 0; i < attempts && trialEnemies.Count < target; i++)
        {
            GameObject instance = enemySpawner.SpawnSpecificEnemyAround(
                enemyPrefabs[i % enemyPrefabs.Length],
                Vector3.zero,
                1.5f,
                SiteRadius - 0.5f,
                1f,
                true,
                0.15f
            );
            EnemyHealth health = instance != null
                ? instance.GetComponent<EnemyHealth>()
                : null;
            if (health != null)
                trialEnemies.Add(health);
        }
    }

    private void ResolvePlayer()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
                return;
            player = playerObject.transform;
            playerHealth = playerObject.GetComponent<PlayerHealth>();
        }

        if (resetPlayerWhenAvailable)
        {
            Vector2 start = new(-6f, -3f);
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
                body.position = start;
            player.position = start;
            resetPlayerWhenAvailable = false;
        }

        if (invulnerabilityRequested && playerHealth != null)
        {
            if (!damageCaptured)
            {
                originalDamageMultiplier = playerHealth.IncomingDamageMultiplier;
                damageCaptured = true;
            }
            playerHealth.SetIncomingDamageMultiplier(0f);
        }
    }

    private void RestorePlayerDamage()
    {
        invulnerabilityRequested = false;
        if (playerHealth != null && damageCaptured)
            playerHealth.SetIncomingDamageMultiplier(originalDamageMultiplier);
        damageCaptured = false;
    }

    private bool IsPlayerInside() => player != null &&
        ((Vector2)player.position).sqrMagnitude <= SiteRadius * SiteRadius;

    private void ClearEvent()
    {
        if (activeEvent != null && eventSpawner != null)
            eventSpawner.ClearDebugEvent();
        activeEvent = null;
    }

    private void ClearEnemies()
    {
        foreach (EnemyHealth enemy in trialEnemies)
        {
            if (enemy != null)
                Destroy(enemy.gameObject);
        }
        trialEnemies.Clear();
    }

    private void ResetHazard()
    {
        patternIndex = 0;
        hazardState = HazardState.Waiting;
        hazardTimer = BeamInterval - 0.4f;
        HideHazardLines();
    }

    private void BuildVisuals()
    {
        visualRoot = new GameObject("Beam Anomaly Site Visual");
        visualRoot.transform.SetParent(transform, false);
        boundary = CreateLine("Beam Boundary", 0.12f,
            new Color(1f, 0.08f, 0.08f, 0.85f), 30);
        boundary.useWorldSpace = false;
        boundary.loop = true;
        boundary.positionCount = 72;
        for (int i = 0; i < boundary.positionCount; i++)
        {
            float angle = Mathf.PI * 2f * i / boundary.positionCount;
            boundary.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * SiteRadius,
                Mathf.Sin(angle) * SiteRadius,
                0f
            ));
        }
        telegraph = CreateLine("Environmental Beam Telegraph", 0.24f,
            new Color(1f, 0.12f, 0.08f, 0.75f), 34);
        glow = CreateLine("Environmental Beam Glow", 3f,
            new Color(1f, 0.01f, 0.01f, 0.3f), 35);
        core = CreateLine("Environmental Beam Core", 1.25f,
            new Color(1f, 0.32f, 0.12f, 1f), 36);
    }

    private LineRenderer CreateLine(
        string name, float width, Color color, int order)
    {
        GameObject lineObject = new(name);
        lineObject.transform.SetParent(visualRoot.transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = width;
        line.startColor = line.endColor = color;
        line.numCapVertices = 6;
        line.sharedMaterial = WeaponCoreDebugVisual.SharedLineMaterial;
        line.sortingLayerName = "Effects";
        line.sortingOrder = order;
        return line;
    }

    private static void SetLine(LineRenderer line, Vector2 a, Vector2 b)
    {
        line.SetPosition(0, a);
        line.SetPosition(1, b);
    }

    private void HideHazardLines()
    {
        if (telegraph != null) telegraph.enabled = false;
        if (glow != null) glow.enabled = false;
        if (core != null) core.enabled = false;
    }

    private static Vector2 ClosestPoint(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 segment = b - a;
        float denominator = segment.sqrMagnitude;
        if (denominator <= 0.0001f)
            return a;
        float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / denominator);
        return a + segment * t;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        => Vector2.Distance(point, ClosestPoint(point, a, b));

    private static EvacuationCorridorEvent FindCorridorPrefab(
        WorldEventSpawner spawner)
    {
        if (spawner == null)
            return null;
        foreach (WorldEvent prefab in spawner.EventPrefabs)
        {
            if (prefab is EvacuationCorridorEvent corridor)
                return corridor;
        }
        return null;
    }

    private void OnGUI()
    {
        if (state == SiteState.Stopped)
            return;
        string hazard = state == SiteState.Collapsing
            ? "COLLAPSING" : state == SiteState.Completed ? "OFF" : "ACTIVE";
        string eventName = activeEvent != null ? "Evacuation Corridor" : "None";
        string reward = powerController != null && powerController.RedBeamEnabled
            ? "ACQUIRED" : "LOCKED";
        string text = "BEAM ANOMALY SITE\n" +
            $"Site: {state}\nHazard: {hazard}\n" +
            "Player Invulnerable: YES\n" +
            $"Current Event: {eventName}\nRed Beam: {reward}\n" +
            $"Enemies Alive: {EnemyHealth.ActiveInstances.Count}";
        GUI.Box(new Rect(14f, Screen.height - 185f, 315f, 170f), text);

        if (state == SiteState.Dormant && IsPlayerInside())
            GUI.Box(new Rect(Screen.width * 0.5f - 165f, 55f, 330f, 70f),
                "BEAM ANOMALY\n[E] ENTER / START TRIAL");
        if (Time.unscaledTime < messageUntil)
            GUI.Box(new Rect(Screen.width * 0.5f - 190f, 55f, 380f, 60f), message);
    }

    private void OnDestroy()
    {
        if (eventSpawner != null)
        {
            eventSpawner.EventCompleted -= HandleEventCompleted;
            eventSpawner.EventFailed -= HandleEventFailed;
        }
        ClearEvent();
        ClearEnemies();
        RestorePlayerDamage();
    }
}
#endif
