using UnityEngine;

[System.Serializable]
public class EnemySpawnStage
{
    public float startTime;
    public GameObject[] enemyPrefabs;
}

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject[] enemyPrefabs;      // массив префабов врагов
    public float spawnInterval = 2f;       // интервал между спавнами
    public int maxEnemies = 10;            // максимум врагов на сцене
    private float spawnTimer;

    [Header("Spawn Distance")]
    public float minSpawnDistance = 5f;    // минимальное расстояние от игрока
    public float maxSpawnDistance = 12f;   // максимальное расстояние от игрока
    public float spawnRadius = 360f;       // угол разброса (по кругу)

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

    private float runTime;

    private Transform player;


    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }
    private void Update()
    {
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

        // Проверяем количество врагов на сцене
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemies.Length >= maxEnemies) return;

        if (enemyPrefabs.Length == 0) return;
        GameObject[] availableEnemies = GetAvailableEnemies();

        if (availableEnemies == null || availableEnemies.Length == 0)
            return;

        GameObject selectedEnemy = availableEnemies[Random.Range(0, availableEnemies.Length)];

        // Выбираем случайное направление
        Vector2 randomDirection = Random.insideUnitCircle.normalized;

        // Выбираем случайное расстояние (от min до max)
        float distance = Random.Range(minSpawnDistance, maxSpawnDistance);

        // Вычисляем позицию спавна
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
        spawnInterval = 0.1f;
        maxEnemies = 300;

        currentHealthMultiplier *= 2.0f;
        currentSpeedMultiplier *= 2.0f;

        Debug.Log("EnemySpawner: Survival mode started.");
    }
}