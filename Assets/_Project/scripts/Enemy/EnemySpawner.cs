using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class EnemySpawnStage
{
    public float startTime;
    public GameObject[] enemyPrefabs;
}

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject[] enemyPrefabs;      
    public float spawnInterval = 2f;       
    public int maxEnemies = 10;            
    private float spawnTimer;
    private bool spawningEnabled = true;

    [Header("Spawn Distance")]
    public float minSpawnDistance = 5f;    
    public float maxSpawnDistance = 12f;   
    public float spawnRadius = 360f;       

    private float difficultyTimer;

    private float currentHealthMultiplier = 1f;
    private float currentSpeedMultiplier = 1f;

    [Header("Difficulty Scaling")]
    [SerializeField] private float difficultyIncreaseInterval = 30f;

    [SerializeField] private float spawnRateMultiplier = 0.9f;
    [SerializeField] private float enemyHealthMultiplier = 1.1f;
    [SerializeField] private float enemySpeedMultiplier = 1.1f;

    [Header("Difficulty Limits")]
    [SerializeField] private float minSpawnInterval = 0.4f;
    [SerializeField] private float maxHealthMultiplier = 3f;
    [SerializeField] private float maxSpeedMultiplier = 1.8f;

    [Header("Spawn Stages")]
    [SerializeField] private EnemySpawnStage[] spawnStages;

    private float baseSpawnInterval;
    private int baseMaxEnemies;
    private float baseHealthMultiplier;
    private float baseSpeedMultiplier;

    private float runTime;

    private Transform player;
    private readonly List<EnemyHealth> activeEnemies = new();
    private bool initialized;


    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        baseSpawnInterval = spawnInterval;
        baseMaxEnemies = maxEnemies;
        baseHealthMultiplier = currentHealthMultiplier;
        baseSpeedMultiplier = currentSpeedMultiplier;
        initialized = true;
    }
    private void Update()
    {
        if (!spawningEnabled)
            return;

        if (Time.timeScale == 0f)
            return;

        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (player == null)
            return;

        runTime += Time.deltaTime;
        difficultyTimer += Time.deltaTime;

        if (difficultyTimer >= difficultyIncreaseInterval)
        {
            difficultyTimer = 0f;
            IncreaseDifficulty();
        }
        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            SpawnEnemy();
        }
    }
    private void IncreaseDifficulty()
    {
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
            $"Difficulty increased! " +
            $"SpawnInterval: {spawnInterval}, " +
            $"HP x{currentHealthMultiplier}, " +
            $"Speed x{currentSpeedMultiplier}"
        );
    }

    void SpawnEnemy()
    {
        if (player == null) return;

        
        activeEnemies.RemoveAll(enemy => enemy == null);
        if (activeEnemies.Count >= maxEnemies) return;

        if (enemyPrefabs.Length == 0) return;
        GameObject[] availableEnemies = GetAvailableEnemies();

        if (availableEnemies == null || availableEnemies.Length == 0)
            return;

        GameObject selectedEnemy = availableEnemies[Random.Range(0, availableEnemies.Length)];

        
        Vector2 randomDirection = Random.insideUnitCircle.normalized;

       
        float distance = Random.Range(minSpawnDistance, maxSpawnDistance);

       
        Vector3 spawnPos = player.position + (Vector3)(randomDirection * distance);

        GameObject enemy = Instantiate(
    selectedEnemy,
    spawnPos,
    Quaternion.identity
);
        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();

        if (enemyHealth != null)
        {
            enemyHealth.SetMaxHealthMultiplier(currentHealthMultiplier);
            activeEnemies.Add(enemyHealth);
        }

        EnemyMovement enemyMovement = enemy.GetComponent<EnemyMovement>();

        if (enemyMovement != null)
        {
            enemyMovement.SetSpeedMultiplier(currentSpeedMultiplier);
        }

    }

    private GameObject[] GetAvailableEnemies()
    {
        GameObject[] result = enemyPrefabs;

        if (spawnStages == null)
            return result;

        for (int i = 0; i < spawnStages.Length; i++)
        {
            if (runTime >= spawnStages[i].startTime)
            {
                result = spawnStages[i].enemyPrefabs;
            }
        }

        return result;
    }
    public void StartSurvivalMode()
    {
        spawnInterval = 0.01f;
        maxEnemies = 400;

        currentHealthMultiplier *= 3.0f;
        currentSpeedMultiplier *= 3.0f;

        Debug.Log("EnemySpawner: Survival mode started.");
    }

    public void ResetSpawner()
    {
        spawnInterval = baseSpawnInterval;
        maxEnemies = baseMaxEnemies;
        currentHealthMultiplier = baseHealthMultiplier;
        currentSpeedMultiplier = baseSpeedMultiplier;
    }
    public void ResetForNewLevel()
    {
        spawnTimer = 0f;
        difficultyTimer = 0f;
        runTime = 0f;
        spawningEnabled = true;
    }
    public void SetLevelScaling(
    float healthMultiplier,
    float speedMultiplier,
    float spawnRateMultiplier
)
    {
        if (!initialized)
        {
            baseSpawnInterval = spawnInterval;
            baseMaxEnemies = maxEnemies;
            baseHealthMultiplier = currentHealthMultiplier;
            baseSpeedMultiplier = currentSpeedMultiplier;
            initialized = true;
        }

        currentHealthMultiplier = Mathf.Max(0.1f, healthMultiplier);
        currentSpeedMultiplier = Mathf.Max(0.1f, speedMultiplier);

        float safeSpawnRateMultiplier = Mathf.Max(0.1f, spawnRateMultiplier);

        spawnInterval = baseSpawnInterval / safeSpawnRateMultiplier;
        spawnInterval = Mathf.Max(minSpawnInterval, spawnInterval);

        maxEnemies = Mathf.RoundToInt(baseMaxEnemies * safeSpawnRateMultiplier);
        maxEnemies = Mathf.Clamp(
            maxEnemies,
            baseMaxEnemies,
            220
        );

        Debug.Log(
            $"[EnemySpawner] Level scaling applied: " +
            $"HP x{currentHealthMultiplier:F2}, " +
            $"Speed x{currentSpeedMultiplier:F2}, " +
            $"Spawn x{safeSpawnRateMultiplier:F2}, " +
            $"Interval {spawnInterval:F2}, " +
            $"Max enemies {maxEnemies}."
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
}
