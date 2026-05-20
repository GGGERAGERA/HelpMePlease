using UnityEngine;

public class RunTimer : MonoBehaviour
{
    [SerializeField] private float runDuration = 300f; // 5 минут

    [Header("Boss Spawn")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private float spawnDistanceFromPlayer = 8f;

    [SerializeField] private AudioClip bossWarningSound;
    [SerializeField] private float bossWarningVolume = 0.8f;
    [SerializeField] private float bossSpawnDelay = 1f;

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
            StartCoroutine(BossSpawnRoutine());
        }
    }

    private void SpawnBossObject()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (bossPrefab != null && player != null)
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;

            Vector3 spawnPosition =
                player.transform.position
                + (Vector3)(randomDirection * spawnDistanceFromPlayer);

            Instantiate(bossPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("RunTimer: bossPrefab или Player не найден.");
        }
    }

    private System.Collections.IEnumerator BossSpawnRoutine()
    {
        bossSpawned = true;

        HUDManager.Instance?.SetTimer(0f);

        HUDManager.Instance?.ShowBossText(
            "BOSS INCOMING",
            5f
        );

        if (bossWarningSound != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(
                bossWarningSound,
                Camera.main.transform.position,
                bossWarningVolume
            );
        }

        CameraShake.Instance?.Shake(0.3f, 0.4f);

        yield return new WaitForSeconds(bossSpawnDelay);

        SpawnBossObject();
    }
}