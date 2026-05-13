using UnityEngine;

public class RunTimer : MonoBehaviour
{
    [SerializeField] private float runDuration = 300f; // 5 минут

    [Header("Boss Spawn")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private float spawnDistanceFromPlayer = 8f;

    private float timeLeft;
    private bool bossSpawned;

    private void Start()
    {
        timeLeft = runDuration;
        HUDManager.Instance?.SetTimer(timeLeft);
    }

    private void Update()
    {
        if (bossSpawned)
            return;

        timeLeft -= Time.deltaTime;
        HUDManager.Instance?.SetTimer(timeLeft);

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            SpawnBoss();
        }
    }

    private void SpawnBoss()
    {
        bossSpawned = true;

        HUDManager.Instance?.SetTimer(0f);
        HUDManager.Instance?.ShowBossText("BOSS INCOMING", 5f);

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (bossPrefab != null && player != null)
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            Vector3 spawnPosition = player.transform.position + (Vector3)(randomDirection * spawnDistanceFromPlayer);

            Instantiate(bossPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("RunTimer: bossPrefab или Player не найден.");
        }
    }
}