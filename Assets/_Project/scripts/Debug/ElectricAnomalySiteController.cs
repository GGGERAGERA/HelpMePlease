using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public sealed class ElectricAnomalySiteController : MonoBehaviour
{
    private enum SiteState { Stopped, Dormant, Active, Collapsing, Completed }
    private enum HazardState { Waiting, Telegraph, Firing }

    private const float SiteRadius = 10.5f;
    private const float HoldSeconds = 9f;
    private const float DischargeInterval = 0.75f;
    private const float TelegraphSeconds = 0.55f;
    private const float DischargeSeconds = 0.24f;
    private const float DamageHalfWidth = 0.8f;
    private const float EnemyDamage = 5000f;
    private const float PlayerDamage = 80f;
    private const float CollapseSeconds = 0.75f;
    private const int EnemyTarget = 30;

    private static readonly Vector2[] NodePositions =
    {
        new(-7f, -3.5f), new(-5.5f, 4.5f), new(-0.5f, -6.5f),
        new(1.5f, 6.2f), new(6.5f, -3.8f), new(7.2f, 3.4f)
    };
    private static readonly Vector2Int[] NodePairs =
    {
        new(0, 5), new(1, 4), new(2, 3),
        new(0, 3), new(1, 5), new(2, 4)
    };

    private readonly HashSet<EnemyHealth> trialEnemies = new();
    private EnemySpawner enemySpawner;
    private WorldEventSpawner eventSpawner;
    private AnomalyPowerDebugController powerController;
    private PowerTestController powerTest;
    private CaptureZoneEvent capturePrefab;
    private GameObject[] enemyPrefabs;
    private Transform player;
    private PlayerHealth playerHealth;
    private float originalDamageMultiplier = 1f;
    private bool damageCaptured;
    private bool invulnerabilityRequested;
    private bool resetPlayerWhenAvailable;
    private CaptureZoneEvent activeEvent;
    private SiteState state = SiteState.Stopped;
    private HazardState hazardState;
    private float hazardTimer;
    private float collapseTimer;
    private int pairIndex;
    private Vector2 dischargeStart;
    private Vector2 dischargeEnd;
    private GameObject visualRoot;
    private readonly List<LineRenderer> nodeRings = new();
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
        CaptureZoneEvent capture,
        GameObject[] prefabs)
    {
        enemySpawner = spawner;
        eventSpawner = events;
        powerController = powers;
        powerTest = test;
        capturePrefab = capture;
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
        powerController?.BeginElectricSiteRewardLock();
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
        powerController?.ClearElectricSiteReward();
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
        if (capturePrefab == null || eventSpawner == null)
            return;

        powerTest?.StopTest();
        if (!eventSpawner.SpawnDebugEventAt(
                capturePrefab,
                new Vector3(-2.5f, -1.5f, 0f),
                true,
                out WorldEvent spawned))
        {
            return;
        }

        activeEvent = spawned as CaptureZoneEvent;
        if (activeEvent == null)
            return;

        activeEvent.ConfigureDebugHoldTime(HoldSeconds);
        EnsureEnemies(EnemyTarget);
        state = SiteState.Active;
        activeEvent.StartSelectedEvent();
    }

    private void UpdateHazard()
    {
        hazardTimer += Time.deltaTime;

        if (hazardState == HazardState.Waiting &&
            hazardTimer >= DischargeInterval)
        {
            BeginTelegraph();
        }
        else if (hazardState == HazardState.Telegraph &&
            hazardTimer >= TelegraphSeconds)
        {
            FireDischarge();
        }
        else if (hazardState == HazardState.Firing &&
            hazardTimer >= DischargeSeconds)
        {
            HideHazardLines();
            hazardState = HazardState.Waiting;
            hazardTimer = 0f;
        }
    }

    private void BeginTelegraph()
    {
        Vector2Int pair = NodePairs[pairIndex % NodePairs.Length];
        pairIndex++;
        dischargeStart = NodePositions[pair.x];
        dischargeEnd = NodePositions[pair.y];
        SetHazardLine(telegraph, dischargeStart, dischargeEnd);
        telegraph.enabled = true;
        glow.enabled = false;
        core.enabled = false;
        hazardState = HazardState.Telegraph;
        hazardTimer = 0f;
    }

    private void FireDischarge()
    {
        telegraph.enabled = false;
        SetHazardLine(glow, dischargeStart, dischargeEnd);
        SetHazardLine(core, dischargeStart, dischargeEnd);
        glow.enabled = true;
        core.enabled = true;
        hazardState = HazardState.Firing;
        hazardTimer = 0f;
        ApplyLineDamage(dischargeStart, dischargeEnd);
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
        powerController?.GrantArcNodeFromSite();
        message = "ELECTRIC ANOMALY CONQUERED\nARC NODE ACQUIRED";
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
            Vector2 start = new(-8f, 0f);
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
        pairIndex = 0;
        hazardState = HazardState.Waiting;
        hazardTimer = DischargeInterval - 0.35f;
        HideHazardLines();
    }

    private void BuildVisuals()
    {
        visualRoot = new GameObject("Electric Anomaly Site Visual");
        visualRoot.transform.SetParent(transform, false);
        boundary = CreateLine("Electric Boundary", 0.1f,
            new Color(0.2f, 0.75f, 1f, 0.8f), 30);
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

        for (int i = 0; i < NodePositions.Length; i++)
        {
            LineRenderer ring = CreateLine($"Electric Node {i + 1}", 0.13f,
                new Color(0.2f, 0.9f, 1f, 1f), 31);
            ring.loop = true;
            ring.useWorldSpace = false;
            ring.transform.localPosition = NodePositions[i];
            ring.positionCount = 20;
            for (int p = 0; p < ring.positionCount; p++)
            {
                float angle = Mathf.PI * 2f * p / ring.positionCount;
                ring.SetPosition(p, new Vector3(
                    Mathf.Cos(angle) * 0.42f,
                    Mathf.Sin(angle) * 0.42f,
                    0f
                ));
            }
            nodeRings.Add(ring);
        }

        telegraph = CreateLine("Electric Telegraph", 0.16f,
            new Color(1f, 0.85f, 0.2f, 0.8f), 34);
        glow = CreateLine("Electric Discharge Glow", 1.6f,
            new Color(0.1f, 0.55f, 1f, 0.35f), 35);
        core = CreateLine("Electric Discharge Core", 0.3f,
            new Color(0.75f, 0.95f, 1f, 1f), 36);
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

    private static void SetHazardLine(LineRenderer line, Vector2 a, Vector2 b)
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

    private void OnGUI()
    {
        if (state == SiteState.Stopped)
            return;
        string hazard = state == SiteState.Collapsing
            ? "COLLAPSING" : state == SiteState.Completed ? "OFF" : "ACTIVE";
        string eventName = activeEvent != null ? "Hold Zone" : "None";
        string reward = powerController != null && powerController.ArcNodeEnabled
            ? "ACQUIRED" : "LOCKED";
        string text = "ELECTRIC ANOMALY SITE\n" +
            $"Site: {state}\nHazard: {hazard}\n" +
            "Player Invulnerable: YES\n" +
            $"Current Event: {eventName}\nArc Node: {reward}\n" +
            $"Enemies Alive: {EnemyHealth.ActiveInstances.Count}";
        GUI.Box(new Rect(14f, Screen.height - 185f, 315f, 170f), text);

        if (state == SiteState.Dormant && IsPlayerInside())
            GUI.Box(new Rect(Screen.width * 0.5f - 165f, 55f, 330f, 70f),
                "ELECTRIC ANOMALY\n[E] ENTER / START TRIAL");
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
