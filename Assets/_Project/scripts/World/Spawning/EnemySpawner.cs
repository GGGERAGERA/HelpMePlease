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

    [Header("Difficulty Limits")]
    [SerializeField] private float minSpawnInterval = 0.4f;
    [SerializeField] private float maxHealthMultiplier = 3f;
    [SerializeField] private float maxSpeedMultiplier = 1.8f;

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
        SpawnEnemy();
    }

    public void SetSpawnProfile(EnemySpawnProfile profile, int runLevel)
    {
        spawnProfile = profile;
        currentRunLevel = Mathf.Max(1, runLevel);
        activePhase = null;
        activePhaseIndex = -1;
        runTime = 0f;
        spawnTimer = 0f;

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
                220
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
        spawnInterval = Mathf.Max(minSpawnInterval, spawnInterval * spawnRateMultiplier);
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

        return Mathf.Max(0.1f, interval / worldAccelerationMultiplier);
    }

    private int GetCurrentMaxAlive()
    {
        return activePhase != null
            ? Mathf.Max(1, activePhase.maxAlive)
            : Mathf.Max(1, maxEnemies);
    }

    private void SpawnEnemy()
    {
        RemoveDestroyedEnemies();

        if (activeEnemies.Count >= GetCurrentMaxAlive())
            return;

        GameObject selectedPrefab = activePhase != null
            ? SelectWeightedEnemy(activePhase)
            : SelectLegacyEnemy();

        if (selectedPrefab == null)
            return;

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

        return entry.maxAliveOfType <= 0 ||
               CountAlive(entry.enemyPrefab) < entry.maxAliveOfType;
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
