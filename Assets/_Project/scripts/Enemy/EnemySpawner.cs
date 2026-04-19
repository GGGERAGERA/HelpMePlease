using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public EnemyData enemyData;   // сюда перетащите созданный ассет (GoblinData)
    public float spawnRadius = 10f;
    public float spawnInterval = 2f;
    public int maxEnemies = 10;
    private Transform player;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        InvokeRepeating("SpawnEnemy", 1f, spawnInterval);
    }

    void SpawnEnemy()
    {
        if (enemyData == null || player == null) return;
        // проверка количества врагов (по тегу "Enemy")
        if (GameObject.FindGameObjectsWithTag("Enemy").Length >= maxEnemies) return;

        Vector3 randomPos = player.position + Random.insideUnitSphere * spawnRadius;
        randomPos.y = 0; // для 3D; для 2D можно randomPos.z = 0

        // Создаём врага из префаба, который лежит в enemyData.prefab
        Instantiate(enemyData.prefab, randomPos, Quaternion.identity);
    }
}