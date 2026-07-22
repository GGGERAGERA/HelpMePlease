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
    [Header("Legacy Spawn Settings")]
    [Tooltip("Used when the selected level has no EnemySpawnProfile.")]
    public GameObject[] enemyPrefabs;
    public float spawnInterval = 2f;
    public int maxEnemies = 10;

    [Header("Spawn Distance")]
    public float minSpawnDistance = 5f;
    public float maxSpawnDistance = 12f;
    public float spawnRadius = 360f;

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
        public EnemyMovement movement;
        public float baseSpeedMultiplier;
    }

    private readonly List<SpawnedEnemy> activeEnemies = new();

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
    private float worldAccelerationMultiplier = 1f;

    private float baseSpawnInterval;
    private int baseMaxEnemies;
    private float baseHealthMultiplier;
    private float baseSpeedMultiplier;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        CaptureBaseSettings();
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
        UpdateActivePhase();

        if (spawnProfile != null && activePhase == null)
            return;

        if (spawnProfile == null)
            UpdateLegacyDifficulty();

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
        runTime = 0f;
        spawnTimer = 0f;
        legacyDifficultySteps = 0;

        if (spawnProfile == null)
        {
            Debug.Log("[EnemySpawner] No spawn profile selected; using legacy Inspector settings.");
            return;
        }

        UpdateActivePhase();
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

    public void SetWorldAcceleration(float multiplier)
    {
        worldAccelerationMultiplier = Mathf.Max(0.1f, multiplier);
        RefreshActiveEnemySpeeds();
    }

    public void ResetForNewLevel()
    {
        spawnTimer = 0f;
        difficultyTimer = 0f;
        runTime = 0f;
        activePhase = null;
        activePhaseIndex = -1;
        legacyDifficultySteps = 0;
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
        legacyDifficultySteps = 0;
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

        if (nextIndex == activePhaseIndex)
            return;

        activePhaseIndex = nextIndex;
        activePhase = nextIndex >= 0 ? phases[nextIndex] : null;
        spawnTimer = 0f;

        if (activePhase != null)
        {
            Debug.Log(
                $"[EnemySpawner] Phase {activePhaseIndex + 1} started at {runTime:F1}s: " +
                $"interval {activePhase.spawnInterval:F2}, max alive {activePhase.maxAlive}."
            );
        }
    }

    private float GetCurrentSpawnInterval()
    {
        float interval = activePhase != null
            ? activePhase.spawnInterval / currentSpawnPressure
            : spawnInterval;

        float limitedInterval = Mathf.Max(minSpawnInterval, interval);
        return Mathf.Max(0.1f, limitedInterval / worldAccelerationMultiplier);
    }

    private int GetCurrentMaxAlive()
    {
        if (activePhase == null)
            return Mathf.Max(1, maxEnemies);

        int scaledMaxAlive = Mathf.RoundToInt(
            activePhase.maxAlive * currentSpawnPressure
        );

        int minimumAlive = Mathf.Max(1, activePhase.maxAlive);

        return Mathf.Clamp(
            scaledMaxAlive,
            minimumAlive,
            Mathf.Max(minimumAlive, maxAliveLimit)
        );
    }

    private int GetCurrentEnemiesPerCycle()
    {
        int phaseBonus = activePhase != null
            ? Mathf.Max(0, activePhaseIndex) / Mathf.Max(1, phasesPerBatchIncrease)
            : 0;
        int pressureBonus = Mathf.FloorToInt(
            Mathf.Max(0f, currentSpawnPressure - 1f) /
            Mathf.Max(0.01f, spawnPressurePerBatchIncrease)
        );
        int legacyBonus = activePhase == null
            ? legacyDifficultySteps / Mathf.Max(1, legacyStepsPerBatchIncrease)
            : 0;

        return Mathf.Clamp(
            baseEnemiesPerCycle + phaseBonus + pressureBonus + legacyBonus,
            1,
            Mathf.Max(1, maxEnemiesPerCycle)
        );
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
        Vector2 direction = Random.insideUnitCircle.normalized;
        float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
        Vector3 spawnPosition = player.position + (Vector3)(direction * distance);

        GameObject enemy = Instantiate(selectedPrefab, spawnPosition, Quaternion.identity);
        EnemyHealth health = enemy.GetComponent<EnemyHealth>();

        if (health != null)
        {
            float phaseHealth = activePhase != null ? activePhase.healthMultiplier : 1f;
            health.SetMaxHealthMultiplier(currentHealthMultiplier * phaseHealth);
        }

        EnemyMovement movement = enemy.GetComponent<EnemyMovement>();
        float phaseSpeed = activePhase != null ? activePhase.speedMultiplier : 1f;
        float baseEnemySpeedMultiplier = currentSpeedMultiplier * phaseSpeed;

        activeEnemies.Add(new SpawnedEnemy
        {
            instance = enemy,
            sourcePrefab = selectedPrefab,
            movement = movement,
            baseSpeedMultiplier = baseEnemySpeedMultiplier
        });

        if (movement != null)
            movement.SetSpeedMultiplier(
                baseEnemySpeedMultiplier * worldAccelerationMultiplier
            );
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
        int scaledTypeLimit = Mathf.Clamp(
            Mathf.RoundToInt(entry.maxAliveOfType * currentSpawnPressure),
            minimumTypeLimit,
            Mathf.Max(minimumTypeLimit, maxAliveLimit)
        );

        return CountAlive(entry.enemyPrefab) < scaledTypeLimit;
    }

    private int CountAlive(GameObject sourcePrefab)
    {
        int count = 0;

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            if (activeEnemies[i].instance != null &&
                activeEnemies[i].sourcePrefab == sourcePrefab)
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
}
