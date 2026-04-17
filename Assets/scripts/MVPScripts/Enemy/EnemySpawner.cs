using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;   // ������ ����� (���������� �� Assets)
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
        // ���������, ��� ������ ����������
        if (enemyPrefab == null)
        {
            Debug.LogError("enemyPrefab �� �������� � ��������!");
            return;
        }

        // ���������, ��� ����� ����������
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null) return;
        }

        // ������� ������ �� �����
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemies.Length >= maxEnemies) return;

        // �������� ��������� ������� ������ ������
        Vector3 randomDirection = Random.insideUnitSphere * spawnRadius;
        randomDirection.z = 0; // ��������� Y, ����� ����� �� ���������� � �������
        Vector3 spawnPos = player.position + randomDirection;

        // ������ �����
        Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
    }
}