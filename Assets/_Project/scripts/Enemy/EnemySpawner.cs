using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject[] enemyPrefabs;      // массив префабов врагов
    public float spawnInterval = 2f;       // интервал между спавнами
    public int maxEnemies = 10;            // максимум врагов на сцене

    [Header("Spawn Distance")]
    public float minSpawnDistance = 5f;    // минимальное расстояние от игрока
    public float maxSpawnDistance = 12f;   // максимальное расстояние от игрока
    public float spawnRadius = 360f;       // угол разброса (по кругу)

    private Transform player;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        InvokeRepeating(nameof(SpawnEnemy), 1f, spawnInterval);
    }

    void SpawnEnemy()
    {
        if (player == null) return;

        // Проверяем количество врагов на сцене
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemies.Length >= maxEnemies) return;

        if (enemyPrefabs.Length == 0) return;
        GameObject selectedEnemy = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];

        // Выбираем случайное направление
        Vector2 randomDirection = Random.insideUnitCircle.normalized;

        // Выбираем случайное расстояние (от min до max)
        float distance = Random.Range(minSpawnDistance, maxSpawnDistance);

        // Вычисляем позицию спавна
        Vector3 spawnPos = player.position + (Vector3)(randomDirection * distance);

        Instantiate(selectedEnemy, spawnPos, Quaternion.identity);
    }
}