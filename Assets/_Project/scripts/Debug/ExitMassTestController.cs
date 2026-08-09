using System.Collections.Generic;
using UnityEngine;

public sealed class ExitMassTestController : MonoBehaviour
{
    [System.Serializable]
    private readonly struct PhaseSettings
    {
        public readonly float StartTime;
        public readonly float SpawnInterval;
        public readonly int BatchSize;
        public readonly int MaxAlive;
        public readonly float LateralSpread;

        public PhaseSettings(
            float startTime,
            float spawnInterval,
            int batchSize,
            int maxAlive,
            float lateralSpread)
        {
            StartTime = startTime;
            SpawnInterval = spawnInterval;
            BatchSize = batchSize;
            MaxAlive = maxAlive;
            LateralSpread = lateralSpread;
        }
    }

    // Aggressive sandbox-only values. Keep enemy HP at prefab defaults.
    private static readonly PhaseSettings[] Phases =
    {
        new(0f, 0.55f, 3, 28, 3.5f),
        new(10f, 0.32f, 5, 65, 5.5f),
        new(22f, 0.22f, 7, 115, 7.2f),
        new(36f, 0.16f, 8, 170, 8.5f)
    };
    private static readonly string[] PhaseLabels = { "I", "II", "III", "IV" };

    private const float MinimumPlayerSpawnDistance = 2.8f;
    private const float SpawnScatterRadius = 1.25f;
    private const float RollingKillWindow = 5f;

    private readonly HashSet<EnemyHealth> testEnemies = new();
    private readonly Queue<float> recentKillTimes = new();

    private EnemySpawner enemySpawner;
    private GameplayAreaService gameplayArea;
    private GameObject[] enemyPrefabs;
    private Transform player;
    private PlayerHealth playerHealth;
    private ExitMassTestGoal exitGoal;
    private Material exitMaterial;
    private Vector2 startPosition;
    private Vector2 exitPosition;
    private float testStartTime;
    private float spawnTimer;
    private float originalIncomingDamageMultiplier = 1f;
    private int phaseIndex;
    private int testKills;
    private bool active;
    private bool completed;
    private bool damageOverrideApplied;

    public void Configure(
        EnemySpawner spawner,
        GameplayAreaService area,
        GameObject[] prefabs)
    {
        enemySpawner = spawner;
        gameplayArea = area;
        enemyPrefabs = prefabs;
        ConfigurePositions();
        CreateExit();
    }

    private void Update()
    {
        ResolvePlayer();

        if (Input.GetKeyDown(KeyCode.F2))
        {
            ResetTest();
            return;
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            StopTest();
            return;
        }

        if (!active || completed || player == null)
            return;

        float elapsed = Time.time - testStartTime;
        int nextPhase = GetPhaseIndex(elapsed);
        if (nextPhase != phaseIndex)
        {
            phaseIndex = nextPhase;
            spawnTimer = Phases[phaseIndex].SpawnInterval;
        }

        TrimRecentKills();
        spawnTimer += Time.deltaTime;
        PhaseSettings phase = Phases[phaseIndex];

        if (spawnTimer < phase.SpawnInterval ||
            testEnemies.Count >= phase.MaxAlive)
        {
            return;
        }

        spawnTimer = 0f;
        SpawnBatch(phase.BatchSize);
    }

    private void ResetTest()
    {
        GetComponent<PowerTestController>()?.StopTest();
        ResolvePlayer();
        if (player == null || enemySpawner == null ||
            enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogWarning(
                "[ExitMassTest] Player, EnemySpawner, or enemy prefabs are missing."
            );
            return;
        }

        ClearTestEnemies();
        ApplyDamageOverride();
        MovePlayerToStart();
        testKills = 0;
        recentKillTimes.Clear();
        phaseIndex = 0;
        spawnTimer = 0f;
        testStartTime = Time.time;
        completed = false;
        active = true;
        exitGoal?.SetActive(true);
        SpawnBatch(6);

        Debug.Log(
            "[ExitMassTest] EXIT MASS TEST STARTED. " +
            "Reach the EXIT on the right. F2 resets; F3 stops."
        );
    }

    private void StopTest()
    {
        ClearTestEnemies();
        RestoreDamageOverride();
        active = false;
        completed = false;
        exitGoal?.SetActive(false);
        Debug.Log("[ExitMassTest] EXIT MASS TEST STOPPED.");
    }

    public void StopForOtherDebugTest()
    {
        if (active)
            StopTest();
    }

    internal void NotifyExitReached()
    {
        if (!active || completed)
            return;

        completed = true;
        float elapsed = Time.time - testStartTime;
        Debug.Log(
            $"TEST EXIT REACHED | Time: {elapsed:F1}s | " +
            $"Kills: {testKills} | Core: {WeaponCoreDebugSelector.ActiveCore}"
        );
    }

    private void SpawnBatch(int requestedCount)
    {
        if (player == null || completed)
            return;

        PhaseSettings phase = Phases[phaseIndex];
        int available = Mathf.Max(0, phase.MaxAlive - testEnemies.Count);
        int count = Mathf.Min(requestedCount, available);
        Vector2 toExit = exitPosition - (Vector2)player.position;
        float distanceToExit = toExit.magnitude;

        if (distanceToExit < 1.5f)
            return;

        Vector2 forward = toExit / distanceToExit;
        Vector2 lateral = new(-forward.y, forward.x);

        for (int i = 0; i < count; i++)
        {
            float minimumForward = Mathf.Min(3.2f, distanceToExit * 0.3f);
            float maximumForward = Mathf.Max(
                minimumForward + 0.2f,
                distanceToExit * Random.Range(0.62f, 0.88f)
            );
            Vector2 anchor = (Vector2)player.position +
                forward * Random.Range(minimumForward, maximumForward) +
                lateral * Random.Range(
                    -phase.LateralSpread,
                    phase.LateralSpread
                );
            GameObject prefab = enemyPrefabs[
                Random.Range(0, enemyPrefabs.Length)
            ];
            GameObject instance = enemySpawner.SpawnSpecificEnemyAround(
                prefab,
                anchor,
                0f,
                SpawnScatterRadius,
                MinimumPlayerSpawnDistance,
                false,
                0f
            );

            if (instance == null)
                continue;

            EnemyHealth health = instance.GetComponent<EnemyHealth>();
            if (health == null)
            {
                Destroy(instance);
                continue;
            }

            testEnemies.Add(health);
            health.OnDied += HandleEnemyDied;
        }
    }

    private void HandleEnemyDied(EnemyHealth enemy)
    {
        if (enemy != null)
            enemy.OnDied -= HandleEnemyDied;

        if (!testEnemies.Remove(enemy))
            return;

        testKills++;
        recentKillTimes.Enqueue(Time.time);
    }

    private void ClearTestEnemies()
    {
        foreach (EnemyHealth enemy in testEnemies)
        {
            if (enemy == null)
                continue;

            enemy.OnDied -= HandleEnemyDied;
            Destroy(enemy.gameObject);
        }

        testEnemies.Clear();
        recentKillTimes.Clear();
    }

    private void ResolvePlayer()
    {
        if (player != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
            return;

        player = playerObject.transform;
        playerHealth = playerObject.GetComponent<PlayerHealth>();
    }

    private void ConfigurePositions()
    {
        Collider2D area = gameplayArea != null
            ? gameplayArea.PlayableArea
            : null;
        Bounds bounds = area != null
            ? area.bounds
            : new Bounds(Vector3.zero, new Vector3(32f, 20f, 0f));
        float horizontalInset = Mathf.Min(2.2f, bounds.extents.x * 0.2f);
        startPosition = new Vector2(
            bounds.min.x + horizontalInset,
            bounds.center.y
        );
        exitPosition = new Vector2(
            bounds.max.x - horizontalInset,
            bounds.center.y
        );
    }

    private void CreateExit()
    {
        if (exitGoal != null)
            return;

        float height = gameplayArea != null &&
            gameplayArea.PlayableArea != null
            ? Mathf.Max(4f, gameplayArea.PlayableArea.bounds.size.y - 0.5f)
            : 18f;
        GameObject root = new("EXIT MASS TEST Goal");
        root.transform.position = exitPosition;
        BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1.4f, height);
        exitGoal = root.AddComponent<ExitMassTestGoal>();
        exitGoal.Configure(this);

        LineRenderer line = root.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 4;
        line.SetPosition(0, new Vector3(-0.7f, -height * 0.5f));
        line.SetPosition(1, new Vector3(0.7f, -height * 0.5f));
        line.SetPosition(2, new Vector3(0.7f, height * 0.5f));
        line.SetPosition(3, new Vector3(-0.7f, height * 0.5f));
        line.startWidth = 0.1f;
        line.endWidth = 0.1f;
        line.startColor = new Color(0.15f, 1f, 0.45f, 1f);
        line.endColor = line.startColor;
        line.sharedMaterial = GetExitMaterial();
        line.sortingLayerName = "Effects";
        line.sortingOrder = 30;

        GameObject label = new("EXIT Label");
        label.transform.SetParent(root.transform, false);
        TextMesh text = label.AddComponent<TextMesh>();
        text.text = "EXIT";
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = 0.32f;
        text.fontSize = 48;
        text.color = new Color(0.2f, 1f, 0.5f, 1f);
        text.GetComponent<MeshRenderer>().sortingLayerName = "Effects";
        text.GetComponent<MeshRenderer>().sortingOrder = 31;
        exitGoal.SetActive(false);
    }

    private Material GetExitMaterial()
    {
        if (exitMaterial != null)
            return exitMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        shader ??= Shader.Find("Hidden/Internal-Colored");
        exitMaterial = new Material(shader)
        {
            name = "Exit Mass Test Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        return exitMaterial;
    }

    private void MovePlayerToStart()
    {
        player.position = startPosition;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = startPosition;
            body.linearVelocity = Vector2.zero;
        }

        CharacterMovement2D movement = player.GetComponent<CharacterMovement2D>();
        if (movement != null)
            movement.enabled = true;

        if (playerHealth != null)
            playerHealth.SetRuntimeHealth(playerHealth.MaxHealth, playerHealth.MaxHealth);
    }

    private void ApplyDamageOverride()
    {
        if (playerHealth == null || damageOverrideApplied)
            return;

        originalIncomingDamageMultiplier = playerHealth.IncomingDamageMultiplier;
        playerHealth.SetIncomingDamageMultiplier(0f);
        damageOverrideApplied = true;
    }

    private void RestoreDamageOverride()
    {
        if (playerHealth == null || !damageOverrideApplied)
            return;

        playerHealth.SetIncomingDamageMultiplier(
            originalIncomingDamageMultiplier
        );
        damageOverrideApplied = false;
    }

    private int GetPhaseIndex(float elapsed)
    {
        for (int i = Phases.Length - 1; i >= 0; i--)
        {
            if (elapsed >= Phases[i].StartTime)
                return i;
        }

        return 0;
    }

    private void TrimRecentKills()
    {
        float cutoff = Time.time - RollingKillWindow;
        while (recentKillTimes.Count > 0 &&
            recentKillTimes.Peek() < cutoff)
        {
            recentKillTimes.Dequeue();
        }
    }

    private void OnGUI()
    {
        if (!active)
        {
            GUI.Box(
                new Rect(14f, 14f, 310f, 52f),
                "EXIT MASS TEST\nF2: START / RESET    F3: STOP"
            );
            return;
        }

        TrimRecentKills();
        float elapsed = Time.time - testStartTime;
        float killsPerSecond = recentKillTimes.Count / RollingKillWindow;
        float distance = player != null
            ? Vector2.Distance(player.position, exitPosition)
            : 0f;
        string phase = PhaseLabels[phaseIndex];
        string metrics =
            $"EXIT MASS TEST\n" +
            $"Threat Phase: {phase}\n" +
            $"Enemies Alive: {testEnemies.Count}\n" +
            $"Kills: {testKills}\n" +
            $"Kills/sec (5s): {killsPerSecond:F1}\n" +
            $"Distance to Exit: {distance:F1}\n" +
            $"Core: {WeaponCoreDebugSelector.ActiveCore}\n" +
            $"Elapsed Time: {elapsed:F1}s\n" +
            "F2: RESET    F3: STOP";
        GUI.Box(new Rect(14f, 14f, 265f, 190f), metrics);

        if (completed)
        {
            GUI.Box(
                new Rect(Screen.width * 0.5f - 180f, 30f, 360f, 70f),
                $"TEST EXIT REACHED\n{elapsed:F1}s  |  {testKills} kills"
            );
        }
    }

    private void OnDestroy()
    {
        ClearTestEnemies();
        RestoreDamageOverride();

        if (exitMaterial != null)
            Destroy(exitMaterial);

        if (exitGoal != null)
            Destroy(exitGoal.gameObject);
    }
}

public sealed class ExitMassTestGoal : MonoBehaviour
{
    private ExitMassTestController controller;

    public void Configure(ExitMassTestController owner)
    {
        controller = owner;
    }

    public void SetActive(bool value)
    {
        gameObject.SetActive(value);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            controller?.NotifyExitReached();
    }
}
