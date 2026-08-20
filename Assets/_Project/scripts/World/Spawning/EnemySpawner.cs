using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemySpawnStage
{
    public float startTime;
    public GameObject[] enemyPrefabs;
}

public class EnemySpawner : MonoBehaviour
{
    private static Collider2D[] additionalWaveOverlapBuffer =
        new Collider2D[32];

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public enum DebugEnemyArchetype
    {
        Basic,
        Shooter,
        Bomber,
        Eyes,
        Turret
    }
#endif
    [Header("Legacy Spawn Settings")]
    [Tooltip("Used when the selected level has no EnemySpawnProfile.")]
    public GameObject[] enemyPrefabs;
    public float spawnInterval = 2f;
    public int maxEnemies = 10;

    [Header("Spawn Distance")]
    public float minSpawnDistance = 5f;
    public float maxSpawnDistance = 12f;
    public float spawnRadius = 360f;
    [SerializeField, Min(1)] private int spawnPositionAttempts = 16;
    [SerializeField] private GameplayAreaService gameplayArea;

    [Header("Enemy Projectile Pools")]
    [SerializeField, Min(0)] private int projectilePrewarmPerPrefab = 24;
    [SerializeField, Min(1)] private int projectilePoolMaximum = 256;

    [Header("Enemy Feedback Pools")]
    [SerializeField, Min(0)] private int feedbackPrewarmPerPrefab = 16;
    [SerializeField, Min(1)] private int feedbackPoolMaximum = 256;

    [Header("Legacy Difficulty Scaling")]
    [SerializeField] private float difficultyIncreaseInterval = 30f;
    [SerializeField] private float spawnRateMultiplier = 0.9f;
    [SerializeField] private float enemyHealthMultiplier = 1.1f;
    [SerializeField] private float enemySpeedMultiplier = 1.1f;
    [SerializeField, Min(0)] private int maxEnemiesIncreasePerStep = 5;
    [SerializeField, Min(1)] private int legacyStepsPerBatchIncrease = 1;

    [Header("Spawn Density Scaling")]
    [SerializeField, Min(1)] private int baseEnemiesPerCycle = 1;
    [SerializeField, Min(1)] private int phasesPerBatchIncrease = 2;
    [SerializeField, Min(0.01f)] private float spawnPressurePerBatchIncrease = 0.5f;
    [SerializeField, Min(1)] private int maxEnemiesPerCycle = 4;

    [Header("Difficulty Limits")]
    [SerializeField] private float minSpawnInterval = 0.4f;
    [SerializeField] private float maxHealthMultiplier = 3f;
    [SerializeField] private float maxSpeedMultiplier = 1.8f;
    [SerializeField, Min(1)] private int maxAliveLimit = 120;

    [Header("Legacy Spawn Stages")]
    [SerializeField] private EnemySpawnStage[] spawnStages;

    private sealed class SpawnedEnemy
    {
        public GameObject instance;
        public GameObject sourcePrefab;
        public string enemyId;
        public EnemyMovement movement;
        public float baseSpeedMultiplier;
    }

    private readonly List<SpawnedEnemy> activeEnemies = new();
    private readonly Dictionary<GameObject, SimplePrefabPool>
        enemyProjectilePools = new();
    private readonly Dictionary<GameObject, SimplePrefabPool>
        enemyFeedbackPools = new();

    private Transform player;
    private EnemySpawnProfile spawnProfile;
    private EnemySpawnPhase activePhase;
    private int activePhaseIndex = -1;
    private int currentRunLevel = 1;

    private float spawnTimer;
    private float difficultyTimer;
    private float runTime;
    private int legacyDifficultySteps;
    private bool spawningEnabled = true;
    private bool initialized;

    private float currentHealthMultiplier = 1f;
    private float currentSpeedMultiplier = 1f;
    private float currentSpawnPressure = 1f;
    private float worldRuleSpawnPressureMultiplier = 1f;
    private float worldEventSpawnPressureMultiplier = 1f;
    private float worldAccelerationMultiplier = 1f;
    private bool runThreatControlsPhase;
    private float runThreatSpawnIntervalMultiplier = 1f;
    private int runThreatMaxAliveCap;
    private int runThreatBatchSize = 1;

    public float WorldEventSpawnPressureMultiplier =>
        worldEventSpawnPressureMultiplier;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool IsSpawningEnabled => spawningEnabled;
#endif

    private float baseSpawnInterval;
    private int baseMaxEnemies;
    private float baseHealthMultiplier;
    private float baseSpeedMultiplier;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool debugFixedExplorationPressure;
#endif

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        ResolveGameplayArea();
        CaptureBaseSettings();
        PrewarmProjectilePools(enemyPrefabs);
    }

    private void Update()
    {
        if (!spawningEnabled || Time.timeScale == 0f)
            return;

        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (player == null)
            return;

        runTime += Time.deltaTime;
        if (!runThreatControlsPhase)
            UpdateActivePhase();

        if (spawnProfile != null && activePhase == null)
            return;

        if (spawnProfile == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!debugFixedExplorationPressure)
#endif
                UpdateLegacyDifficulty();
        }

        spawnTimer += Time.deltaTime;

        if (spawnTimer < GetCurrentSpawnInterval())
            return;

        spawnTimer = 0f;
        SpawnCycle();
    }

    public void SetSpawnProfile(EnemySpawnProfile profile, int runLevel)
    {
        spawnProfile = profile;
        currentRunLevel = Mathf.Max(1, runLevel);
        activePhase = null;
        activePhaseIndex = -1;
        runThreatControlsPhase = false;
        runThreatSpawnIntervalMultiplier = 1f;
        runThreatMaxAliveCap = 0;
        runThreatBatchSize = 1;
        runTime = 0f;
        spawnTimer = 0f;
        legacyDifficultySteps = 0;

        if (spawnProfile == null)
        {
            Debug.Log("[EnemySpawner] No spawn profile selected; using legacy Inspector settings.");
            return;
        }

        PrewarmProjectilePools(spawnProfile);
        UpdateActivePhase();
    }

    public void SetRunThreatPreset(
        int presetIndex,
        float spawnIntervalMultiplier,
        int maxAliveCap,
        int batchSize)
    {
        runThreatControlsPhase = true;
        runThreatSpawnIntervalMultiplier = Mathf.Max(
            0.1f,
            spawnIntervalMultiplier
        );
        runThreatMaxAliveCap = Mathf.Clamp(
            maxAliveCap,
            1,
            Mathf.Min(40, Mathf.Max(1, maxAliveLimit))
        );
        runThreatBatchSize = Mathf.Clamp(
            batchSize,
            1,
            Mathf.Max(1, maxEnemiesPerCycle)
        );

        ApplyProfilePhase(presetIndex);
    }

    public void SetLevelScaling(
        float healthMultiplier,
        float speedMultiplier,
        float spawnPressure)
    {
        if (!initialized)
            CaptureBaseSettings();

        currentHealthMultiplier = Mathf.Max(0.1f, healthMultiplier);
        currentSpeedMultiplier = Mathf.Max(0.1f, speedMultiplier);
        currentSpawnPressure = Mathf.Max(0.1f, spawnPressure);

        if (spawnProfile == null)
        {
            spawnInterval = Mathf.Max(
                minSpawnInterval,
                baseSpawnInterval / currentSpawnPressure
            );
            maxEnemies = Mathf.Clamp(
                Mathf.RoundToInt(baseMaxEnemies * currentSpawnPressure),
                baseMaxEnemies,
                Mathf.Max(baseMaxEnemies, maxAliveLimit)
            );
        }

        Debug.Log(
            $"[EnemySpawner] Level scaling: HP x{currentHealthMultiplier:F2}, " +
            $"speed x{currentSpeedMultiplier:F2}, pressure x{currentSpawnPressure:F2}."
        );
    }

    public void StopSpawning()
    {
        spawningEnabled = false;
        Debug.Log("[EnemySpawner] Spawning stopped.");
    }

    public void ResumeSpawning()
    {
        spawningEnabled = true;
        Debug.Log("[EnemySpawner] Spawning resumed.");
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public GameObject FindDebugEnemyPrefab(DebugEnemyArchetype archetype)
    {
        GameObject match = FindDebugEnemyPrefab(enemyPrefabs, archetype);
        if (match != null)
            return match;

        if (spawnStages != null)
        {
            for (int i = 0; i < spawnStages.Length; i++)
            {
                match = FindDebugEnemyPrefab(
                    spawnStages[i]?.enemyPrefabs,
                    archetype
                );
                if (match != null)
                    return match;
            }
        }

        EnemySpawnPhase[] phases = spawnProfile != null
            ? spawnProfile.Phases
            : null;
        if (phases == null)
            return null;

        for (int i = 0; i < phases.Length; i++)
        {
            EnemySpawnEntry[] entries = phases[i]?.enemies;
            if (entries == null)
                continue;

            for (int j = 0; j < entries.Length; j++)
            {
                GameObject prefab = entries[j]?.enemyPrefab;
                if (MatchesDebugArchetype(prefab, archetype))
                    return prefab;
            }
        }

        return null;
    }

    private static GameObject FindDebugEnemyPrefab(
        GameObject[] prefabs,
        DebugEnemyArchetype archetype)
    {
        if (prefabs == null)
            return null;

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (MatchesDebugArchetype(prefabs[i], archetype))
                return prefabs[i];
        }

        return null;
    }

    private static bool MatchesDebugArchetype(
        GameObject prefab,
        DebugEnemyArchetype archetype)
    {
        if (prefab == null)
            return false;

        bool shooter = prefab.GetComponent<EnemyShooterMovement>() != null;
        bool bomber = prefab.GetComponent<EnemyBomberMovement>() != null;
        bool eyes = prefab.GetComponent<EyesEnemyBehaviour>() != null;
        bool turret = prefab.GetComponent<TurretEnemyBehaviour>() != null;

        return archetype switch
        {
            DebugEnemyArchetype.Shooter => shooter,
            DebugEnemyArchetype.Bomber => bomber,
            DebugEnemyArchetype.Eyes => eyes,
            DebugEnemyArchetype.Turret => turret,
            _ => !shooter && !bomber && !eyes && !turret
        };
    }

    public void ConfigureDebugExplorationPressure(
        GameObject[] prefabs,
        float interval,
        int maxAlive,
        int batchSize,
        float minimumSpawnDistance = 8f,
        float maximumSpawnDistance = 16f)
    {
        spawnProfile = null;
        activePhase = null;
        activePhaseIndex = -1;
        enemyPrefabs = prefabs ?? System.Array.Empty<GameObject>();
        spawnInterval = Mathf.Max(0.1f, interval);
        maxEnemies = Mathf.Max(1, maxAlive);
        baseEnemiesPerCycle = Mathf.Max(1, batchSize);
        maxEnemiesPerCycle = Mathf.Max(maxEnemiesPerCycle, batchSize);
        minSpawnDistance = Mathf.Max(1f, minimumSpawnDistance);
        maxSpawnDistance = Mathf.Max(
            minSpawnDistance,
            maximumSpawnDistance
        );
        currentHealthMultiplier = 1f;
        currentSpeedMultiplier = 1f;
        currentSpawnPressure = 1f;
        worldRuleSpawnPressureMultiplier = 1f;
        legacyDifficultySteps = 0;
        difficultyTimer = 0f;
        spawnTimer = 0f;
        debugFixedExplorationPressure = true;
        spawningEnabled = true;
    }

    public void StopDebugExplorationPressure()
    {
        debugFixedExplorationPressure = false;
        worldEventSpawnPressureMultiplier = 1f;
        spawningEnabled = false;
    }

    public void ClearDebugSpawnedEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            GameObject instance = activeEnemies[i].instance;
            if (instance != null)
                Destroy(instance);
        }

        activeEnemies.Clear();
    }
#endif

    public void SetWorldAcceleration(float multiplier)
    {
        worldAccelerationMultiplier = Mathf.Max(0.1f, multiplier);
        RefreshActiveEnemySpeeds();
    }

    public void SetWorldRuleSpawnPressureMultiplier(float multiplier)
    {
        worldRuleSpawnPressureMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public void SetWorldEventSpawnPressureMultiplier(float multiplier)
    {
        worldEventSpawnPressureMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public void ResetForNewLevel()
    {
        spawnTimer = 0f;
        difficultyTimer = 0f;
        runTime = 0f;
        activePhase = null;
        activePhaseIndex = -1;
        legacyDifficultySteps = 0;
        worldEventSpawnPressureMultiplier = 1f;
        runThreatControlsPhase = false;
        runThreatSpawnIntervalMultiplier = 1f;
        runThreatMaxAliveCap = 0;
        runThreatBatchSize = 1;
        spawningEnabled = true;
    }

    public void ResetSpawner()
    {
        if (!initialized)
            CaptureBaseSettings();

        spawnInterval = baseSpawnInterval;
        maxEnemies = baseMaxEnemies;
        currentHealthMultiplier = baseHealthMultiplier;
        currentSpeedMultiplier = baseSpeedMultiplier;
        currentSpawnPressure = 1f;
        worldRuleSpawnPressureMultiplier = 1f;
        worldEventSpawnPressureMultiplier = 1f;
        legacyDifficultySteps = 0;
        runThreatControlsPhase = false;
        runThreatSpawnIntervalMultiplier = 1f;
        runThreatMaxAliveCap = 0;
        runThreatBatchSize = 1;
    }

    public void SpawnAdditionalWave(
        Vector3 origin,
        int enemyCount,
        float minDistance = 1f,
        float maxDistance = 3f,
        float minimumDistanceFromPlayer = 0f)
    {
        if (gameplayArea == null)
            ResolveGameplayArea();

        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        RemoveDestroyedEnemies();

        int count = Mathf.Max(0, enemyCount);

        for (int i = 0; i < count; i++)
        {
            GameObject selectedPrefab = activePhase != null
                ? SelectWeightedEnemy(activePhase)
                : SelectLegacyEnemy();

            if (selectedPrefab == null ||
                gameplayArea == null ||
                !TryGetAdditionalWaveSpawnPosition(
                    origin,
                    minDistance,
                    maxDistance,
                    minimumDistanceFromPlayer,
                    0f,
                    out Vector3 spawnPosition))
            {
                break;
            }

            SpawnEnemyAt(selectedPrefab, spawnPosition);
        }
    }

    private bool TryGetAdditionalWaveSpawnPosition(
        Vector3 origin,
        float minDistance,
        float maxDistance,
        float minimumDistanceFromPlayer,
        float spawnClearance,
        out Vector3 spawnPosition)
    {
        float safePlayerDistance = Mathf.Max(0f, minimumDistanceFromPlayer);
        float minimumRadius = Mathf.Max(0f, minDistance);
        float maximumRadius = Mathf.Max(minimumRadius, maxDistance);

        for (int i = 0; i < Mathf.Max(1, spawnPositionAttempts); i++)
        {
            if (!gameplayArea.TryGetSpawnPosition(
                    origin,
                    minDistance,
                    maxDistance,
                    1,
                    out Vector3 candidate))
            {
                continue;
            }

            float originDistance = Vector2.Distance(candidate, origin);

            if (originDistance < minimumRadius ||
                originDistance > maximumRadius)
            {
                continue;
            }

            if (player != null &&
                Vector2.Distance(candidate, player.position) <
                safePlayerDistance)
            {
                continue;
            }

            if (!IsAdditionalWaveSpawnPositionClear(
                    candidate,
                    spawnClearance))
            {
                continue;
            }

            spawnPosition = candidate;
            return true;
        }

        spawnPosition = default;
        return false;
    }

    private static bool IsAdditionalWaveSpawnPositionClear(
        Vector2 position,
        float clearance)
    {
        float safeClearance = Mathf.Max(0f, clearance);

        if (safeClearance <= 0f)
            return true;

        ContactFilter2D filter = ContactFilter2D.noFilter;
        filter.useTriggers = true;
        int overlapCount;

        do
        {
            overlapCount = Physics2D.OverlapCircle(
                position,
                safeClearance,
                filter,
                additionalWaveOverlapBuffer);

            if (overlapCount < additionalWaveOverlapBuffer.Length)
                break;

            System.Array.Resize(
                ref additionalWaveOverlapBuffer,
                additionalWaveOverlapBuffer.Length * 2);
        }
        while (true);

        bool isClear = true;
        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D overlap = additionalWaveOverlapBuffer[i];
            additionalWaveOverlapBuffer[i] = null;

            if (overlap != null && overlap.enabled && !overlap.isTrigger)
                isClear = false;
        }

        return isClear;
    }

    private void CaptureBaseSettings()
    {
        baseSpawnInterval = Mathf.Max(0.1f, spawnInterval);
        baseMaxEnemies = Mathf.Max(1, maxEnemies);
        baseHealthMultiplier = currentHealthMultiplier;
        baseSpeedMultiplier = currentSpeedMultiplier;
        initialized = true;
    }

    private void UpdateLegacyDifficulty()
    {
        if (difficultyIncreaseInterval <= 0f)
            return;

        difficultyTimer += Time.deltaTime;

        if (difficultyTimer < difficultyIncreaseInterval)
            return;

        difficultyTimer = 0f;
        legacyDifficultySteps++;
        spawnInterval = Mathf.Max(minSpawnInterval, spawnInterval * spawnRateMultiplier);
        maxEnemies = Mathf.Min(
            Mathf.Max(baseMaxEnemies, maxAliveLimit),
            maxEnemies + Mathf.Max(0, maxEnemiesIncreasePerStep)
        );
        currentHealthMultiplier = Mathf.Min(
            maxHealthMultiplier,
            currentHealthMultiplier * enemyHealthMultiplier
        );
        currentSpeedMultiplier = Mathf.Min(
            maxSpeedMultiplier,
            currentSpeedMultiplier * enemySpeedMultiplier
        );

        Debug.Log(
            $"[EnemySpawner] Legacy difficulty increased: interval {spawnInterval:F2}, " +
            $"batch {GetCurrentEnemiesPerCycle()}, max alive {maxEnemies}, " +
            $"HP x{currentHealthMultiplier:F2}, speed x{currentSpeedMultiplier:F2}."
        );
    }

    private void UpdateActivePhase()
    {
        if (spawnProfile == null || spawnProfile.Phases == null)
            return;

        EnemySpawnPhase[] phases = spawnProfile.Phases;
        int nextIndex = -1;
        float latestStartTime = float.MinValue;

        for (int i = 0; i < phases.Length; i++)
        {
            EnemySpawnPhase phase = phases[i];

            if (phase != null &&
                runTime >= phase.startTime &&
                phase.startTime >= latestStartTime)
            {
                nextIndex = i;
                latestStartTime = phase.startTime;
            }
        }

        ApplyProfilePhase(nextIndex);
    }

    private void ApplyProfilePhase(int requestedIndex)
    {
        EnemySpawnPhase[] phases = spawnProfile != null
            ? spawnProfile.Phases
            : null;

        if (phases == null || phases.Length == 0)
        {
            activePhaseIndex = -1;
            activePhase = null;
            return;
        }

        int nextIndex = Mathf.Clamp(requestedIndex, 0, phases.Length - 1);

        if (nextIndex == activePhaseIndex && activePhase != null)
            return;

        activePhaseIndex = nextIndex;
        activePhase = phases[nextIndex];
        spawnTimer = 0f;

        if (activePhase != null)
        {
            Debug.Log(
                $"[EnemySpawner] Phase {activePhaseIndex + 1}: " +
                $"interval {activePhase.spawnInterval:F2}, " +
                $"max alive {activePhase.maxAlive}."
            );
        }
    }

    private float GetCurrentSpawnInterval()
    {
        float interval = activePhase != null
            ? activePhase.spawnInterval / currentSpawnPressure
            : spawnInterval;

        interval *= runThreatSpawnIntervalMultiplier;
        float limitedInterval = Mathf.Max(minSpawnInterval, interval);
        return Mathf.Max(
            0.1f,
            limitedInterval /
            GetExternalSpawnPressureMultiplier() /
            worldAccelerationMultiplier
        );
    }

    public GameObject SpawnSpecificEnemyAround(
        GameObject enemyPrefab,
        Vector3 origin,
        float minDistance,
        float maxDistance,
        float minimumDistanceFromPlayer = 0f,
        bool countTowardSpawnLimits = true,
        float spawnClearance = 0f)
    {
        if (enemyPrefab == null)
            return null;

        if (gameplayArea == null)
            ResolveGameplayArea();

        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        RemoveDestroyedEnemies();

        if (gameplayArea == null ||
            !TryGetAdditionalWaveSpawnPosition(
                origin,
                minDistance,
                maxDistance,
                minimumDistanceFromPlayer,
                spawnClearance,
                out Vector3 spawnPosition))
        {
            return null;
        }

        return SpawnEnemyAt(
            enemyPrefab,
            spawnPosition,
            countTowardSpawnLimits
        );
    }

    private int GetCurrentMaxAlive()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugFixedExplorationPressure)
            return Mathf.Max(1, maxEnemies);
#endif
        float effectivePressure = GetEffectiveSpawnPressure();

        if (activePhase == null)
        {
            int legacyMinimumAlive = Mathf.Max(1, maxEnemies);
            return Mathf.Clamp(
                Mathf.RoundToInt(maxEnemies *
                    GetExternalSpawnPressureMultiplier()),
                legacyMinimumAlive,
                Mathf.Max(legacyMinimumAlive, maxAliveLimit)
            );
        }

        int scaledMaxAlive = Mathf.RoundToInt(
            activePhase.maxAlive * effectivePressure
        );

        int minimumAlive = Mathf.Max(1, activePhase.maxAlive);

        int result = Mathf.Clamp(
            scaledMaxAlive,
            minimumAlive,
            Mathf.Max(minimumAlive, maxAliveLimit)
        );

        return runThreatMaxAliveCap > 0
            ? Mathf.Min(result, runThreatMaxAliveCap)
            : result;
    }

    private int GetCurrentEnemiesPerCycle()
    {
        float effectivePressure = GetEffectiveSpawnPressure();
        int phaseBonus = activePhase != null
            ? Mathf.Max(0, activePhaseIndex) / Mathf.Max(1, phasesPerBatchIncrease)
            : 0;
        int pressureBonus = Mathf.FloorToInt(
            Mathf.Max(0f, effectivePressure - 1f) /
            Mathf.Max(0.01f, spawnPressurePerBatchIncrease)
        );
        int legacyBonus = activePhase == null
            ? legacyDifficultySteps / Mathf.Max(1, legacyStepsPerBatchIncrease)
            : 0;

        int calculatedBatch = Mathf.Clamp(
            baseEnemiesPerCycle + phaseBonus + pressureBonus + legacyBonus,
            1,
            Mathf.Max(1, maxEnemiesPerCycle)
        );

        return runThreatControlsPhase
            ? Mathf.Max(calculatedBatch, runThreatBatchSize)
            : calculatedBatch;
    }

    private void SpawnCycle()
    {
        RemoveDestroyedEnemies();

        int availableSlots = GetCurrentMaxAlive() - activeEnemies.Count;

        if (availableSlots <= 0)
            return;

        int spawnCount = Mathf.Min(GetCurrentEnemiesPerCycle(), availableSlots);

        for (int i = 0; i < spawnCount; i++)
        {
            GameObject selectedPrefab = activePhase != null
                ? SelectWeightedEnemy(activePhase)
                : SelectLegacyEnemy();

            if (selectedPrefab == null)
                break;

            SpawnEnemy(selectedPrefab);
        }
    }

    private void SpawnEnemy(GameObject selectedPrefab)
    {
        if (gameplayArea == null)
            ResolveGameplayArea();

        if (gameplayArea == null ||
            !gameplayArea.TryGetSpawnPosition(
                player.position,
                minSpawnDistance,
                maxSpawnDistance,
                spawnPositionAttempts,
                out Vector3 spawnPosition))
        {
            Debug.LogWarning(
                "[EnemySpawner] No valid position exists inside the spawn area.",
                this
            );
            return;
        }

        SpawnEnemyAt(selectedPrefab, spawnPosition);
    }

    private GameObject SpawnEnemyAt(
        GameObject selectedPrefab,
        Vector3 spawnPosition,
        bool countTowardSpawnLimits = true)
    {
        GameObject enemy = Instantiate(
            selectedPrefab,
            spawnPosition,
            Quaternion.identity
        );
        ConfigureEnemyProjectilePool(enemy);
        EnemyHealth health = enemy.GetComponent<EnemyHealth>();

        if (health != null)
        {
            ConfigureEnemyFeedbackPools(health);
            float phaseHealth = activePhase != null ? activePhase.healthMultiplier : 1f;
            health.SetMaxHealthMultiplier(currentHealthMultiplier * phaseHealth);
        }

        EnemyMovement movement = enemy.GetComponent<EnemyMovement>();
        float phaseSpeed = activePhase != null ? activePhase.speedMultiplier : 1f;
        float baseEnemySpeedMultiplier = currentSpeedMultiplier * phaseSpeed;

        EnemyIdentity identity = enemy.GetComponent<EnemyIdentity>();

        if (countTowardSpawnLimits)
        {
            activeEnemies.Add(new SpawnedEnemy
            {
                instance = enemy,
                sourcePrefab = selectedPrefab,
                enemyId = identity != null ? identity.EnemyId : string.Empty,
                movement = movement,
                baseSpeedMultiplier = baseEnemySpeedMultiplier
            });
        }

        if (movement != null)
            movement.SetSpeedMultiplier(
                baseEnemySpeedMultiplier * worldAccelerationMultiplier
            );

        health?.NotifySpawnConfigured();
        return enemy;
    }

    private void ConfigureEnemyProjectilePool(GameObject enemy)
    {
        EnemyShooterMovement shooter =
            enemy.GetComponent<EnemyShooterMovement>();
        if (shooter != null)
        {
            shooter.SetProjectilePool(
                GetEnemyProjectilePool(shooter.ProjectilePrefab));
        }

        TurretEnemyBehaviour turret =
            enemy.GetComponent<TurretEnemyBehaviour>();
        if (turret != null)
        {
            turret.SetProjectilePool(
                GetEnemyProjectilePool(turret.ProjectilePrefab));
        }
    }

    private SimplePrefabPool GetEnemyProjectilePool(GameObject projectile)
    {
        if (projectile == null)
            return null;

        if (enemyProjectilePools.TryGetValue(
                projectile,
                out SimplePrefabPool existing))
        {
            return existing;
        }

        SimplePrefabPool created = new(
            this,
            projectile,
            projectilePrewarmPerPrefab,
            projectilePoolMaximum);
        enemyProjectilePools.Add(projectile, created);
        return created;
    }

    private void ConfigureEnemyFeedbackPools(EnemyHealth health)
    {
        health.SetFeedbackPools(
            GetEnemyFeedbackPool(health.DamagePopupPrefab),
            GetEnemyFeedbackPool(health.BloodHitPrefab),
            GetEnemyFeedbackPool(health.DeathFxPrefab));
    }

    private SimplePrefabPool GetEnemyFeedbackPool(GameObject prefab)
    {
        if (prefab == null)
            return null;

        if (enemyFeedbackPools.TryGetValue(
                prefab,
                out SimplePrefabPool existing))
        {
            return existing;
        }

        SimplePrefabPool created = new(
            this,
            prefab,
            feedbackPrewarmPerPrefab,
            feedbackPoolMaximum);
        enemyFeedbackPools.Add(prefab, created);
        return created;
    }

    private void PrewarmProjectilePools(GameObject[] prefabs)
    {
        if (prefabs == null)
            return;

        for (int i = 0; i < prefabs.Length; i++)
            PrewarmProjectilePool(prefabs[i]);
    }

    private void PrewarmProjectilePools(EnemySpawnProfile profile)
    {
        EnemySpawnPhase[] phases = profile != null ? profile.Phases : null;
        if (phases == null)
            return;

        for (int phaseIndex = 0; phaseIndex < phases.Length; phaseIndex++)
        {
            EnemySpawnEntry[] entries = phases[phaseIndex]?.enemies;
            if (entries == null)
                continue;

            for (int entryIndex = 0;
                 entryIndex < entries.Length;
                 entryIndex++)
            {
                PrewarmProjectilePool(entries[entryIndex]?.enemyPrefab);
            }
        }
    }

    private void PrewarmProjectilePool(GameObject enemyPrefab)
    {
        if (enemyPrefab == null)
            return;

        EnemyShooterMovement shooter =
            enemyPrefab.GetComponent<EnemyShooterMovement>();
        if (shooter != null)
            GetEnemyProjectilePool(shooter.ProjectilePrefab);

        TurretEnemyBehaviour turret =
            enemyPrefab.GetComponent<TurretEnemyBehaviour>();
        if (turret != null)
            GetEnemyProjectilePool(turret.ProjectilePrefab);

        EnemyHealth health = enemyPrefab.GetComponent<EnemyHealth>();
        if (health != null)
        {
            GetEnemyFeedbackPool(health.DamagePopupPrefab);
            GetEnemyFeedbackPool(health.BloodHitPrefab);
            GetEnemyFeedbackPool(health.DeathFxPrefab);
        }
    }

    private void OnDestroy()
    {
        foreach (SimplePrefabPool projectilePool in
                 enemyProjectilePools.Values)
        {
            projectilePool.Dispose();
        }

        enemyProjectilePools.Clear();

        foreach (SimplePrefabPool feedbackPool in enemyFeedbackPools.Values)
            feedbackPool.Dispose();

        enemyFeedbackPools.Clear();
    }

    private GameObject SelectWeightedEnemy(EnemySpawnPhase phase)
    {
        if (phase.enemies == null || phase.enemies.Length == 0)
            return null;

        float totalWeight = 0f;

        for (int i = 0; i < phase.enemies.Length; i++)
        {
            EnemySpawnEntry entry = phase.enemies[i];

            if (CanSpawn(entry))
                totalWeight += entry.weight;
        }

        if (totalWeight <= 0f)
            return null;

        float roll = Random.value * totalWeight;

        for (int i = 0; i < phase.enemies.Length; i++)
        {
            EnemySpawnEntry entry = phase.enemies[i];

            if (!CanSpawn(entry))
                continue;

            roll -= entry.weight;

            if (roll <= 0f)
                return entry.enemyPrefab;
        }

        return null;
    }

    private bool CanSpawn(EnemySpawnEntry entry)
    {
        if (entry == null || entry.enemyPrefab == null || entry.weight <= 0f)
            return false;

        if (currentRunLevel < Mathf.Max(1, entry.minimumRunLevel))
            return false;

        if (entry.maxAliveOfType <= 0)
            return true;

        int minimumTypeLimit = Mathf.Max(1, entry.maxAliveOfType);

        if (entry.useExactAliveLimit)
            return CountAlive(entry) < minimumTypeLimit;

        float effectivePressure = GetEffectiveSpawnPressure();
        int scaledTypeLimit = Mathf.Clamp(
            Mathf.RoundToInt(entry.maxAliveOfType * effectivePressure),
            minimumTypeLimit,
            Mathf.Max(minimumTypeLimit, maxAliveLimit)
        );

        return CountAlive(entry) < scaledTypeLimit;
    }

    private float GetEffectiveSpawnPressure()
    {
        return currentSpawnPressure * GetExternalSpawnPressureMultiplier();
    }

    private float GetExternalSpawnPressureMultiplier()
    {
        return worldRuleSpawnPressureMultiplier *
            worldEventSpawnPressureMultiplier;
    }

    private int CountAlive(EnemySpawnEntry entry)
    {
        int count = 0;
        EnemyIdentity prefabIdentity =
            entry.enemyPrefab.GetComponent<EnemyIdentity>();
        string targetEnemyId = prefabIdentity != null
            ? prefabIdentity.EnemyId
            : string.Empty;
        bool useIdentity = !string.IsNullOrWhiteSpace(targetEnemyId);

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            SpawnedEnemy activeEnemy = activeEnemies[i];

            if (activeEnemy.instance != null &&
                (useIdentity
                    ? activeEnemy.enemyId == targetEnemyId
                    : activeEnemy.sourcePrefab == entry.enemyPrefab))
            {
                count++;
            }
        }

        return count;
    }

    private void RemoveDestroyedEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i].instance == null)
                activeEnemies.RemoveAt(i);
        }
    }

    private void RefreshActiveEnemySpeeds()
    {
        RemoveDestroyedEnemies();

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            SpawnedEnemy enemy = activeEnemies[i];

            if (enemy.movement != null)
            {
                enemy.movement.SetSpeedMultiplier(
                    enemy.baseSpeedMultiplier * worldAccelerationMultiplier
                );
            }
        }
    }

    private GameObject SelectLegacyEnemy()
    {
        GameObject[] available = GetLegacyAvailableEnemies();

        if (available == null || available.Length == 0)
            return null;

        return available[Random.Range(0, available.Length)];
    }

    private GameObject[] GetLegacyAvailableEnemies()
    {
        GameObject[] result = enemyPrefabs;

        if (spawnStages == null)
            return result;

        for (int i = 0; i < spawnStages.Length; i++)
        {
            if (runTime >= spawnStages[i].startTime)
                result = spawnStages[i].enemyPrefabs;
        }

        return result;
    }

    private void ResolveGameplayArea()
    {
        if (gameplayArea == null)
            gameplayArea = GameplayAreaService.Instance;

        if (gameplayArea == null)
            gameplayArea = FindFirstObjectByType<GameplayAreaService>();
    }
}
