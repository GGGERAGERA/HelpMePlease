using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;   // ПРЕФАБ врага (перетащите из Assets)
    public float spawnRadius = 10f;
    public float spawnInterval = 2f;
    public int maxEnemies = 10;

    private Transform player;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        InvokeRepeating("SpawnEnemy", 1f, spawnInterval);
    }

    void SpawnEnemy()
    {
        // Проверяем, что префаб существует
        if (enemyPrefab == null)
        {
            Debug.LogError("enemyPrefab не назначен в спавнере!");
            return;
        }

        // Проверяем, что игрок существует
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null) return;
        }

        // Считаем врагов на сцене
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemies.Length >= maxEnemies) return;

        // Выбираем случайную позицию вокруг игрока
        Vector3 randomDirection = Random.insideUnitSphere * spawnRadius;
        randomDirection.y = 0; // фиксируем Y, чтобы враги не появлялись в воздухе
        Vector3 spawnPos = player.position + randomDirection;

        // Создаём врага
        Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
    }
}