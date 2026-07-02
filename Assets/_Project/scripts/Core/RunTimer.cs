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

    private bool survivalPhaseStarted;
    private float survivalTime;

    private void Start()
    {
        timeLeft = runDuration;
        HUDManager.Instance?.SetTimer(timeLeft);
    }

    private void Update()
    {
        if (survivalPhaseStarted)
        {
            survivalTime += Time.deltaTime;
            HUDManager.Instance?.SetTimer(survivalTime);
            return;
        }

        if (bossSpawned)
            return;

        timeLeft -= Time.deltaTime;
        HUDManager.Instance?.SetTimer(timeLeft);

        if (timeLeft <= 0f && !bossSpawned)
        {
            timeLeft = 0f;
            bossSpawned = true;
            StartCoroutine(BossSpawnRoutine());
        }
    }

    private void SpawnBossObject()
    {
        Debug.Log("BOSS SPAWNED");

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
        RunMessageService.Instance?.Show(RunMessageType.BossIncoming);


        if (bossWarningSound != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(
                bossWarningSound,
                Camera.main.transform.position,
                bossWarningVolume
            );
        }

        CameraShake.Instance?.Shake(2f, 0.05f);

        yield return new WaitForSeconds(bossSpawnDelay);

        SpawnBossObject();
    }
    public void StartSurvivalPhase()
    {
        if (survivalPhaseStarted)
            return;

        survivalPhaseStarted = true;
        survivalTime = 0f;

        HUDManager.Instance?.SetTimer(0f);

        EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();

        if (spawner != null)
            spawner.StartSurvivalMode();

        Debug.Log("Survival phase started.");
    }
    public float GetSurvivalTime()
    {
        return survivalTime;
    }

    public bool IsSurvivalPhaseStarted()
    {
        return survivalPhaseStarted;
    }
    public void RestartBossTimer()
    {
        survivalPhaseStarted = false;
        survivalTime = 0f;

        bossSpawned = false;
        timeLeft = runDuration;

        HUDManager.Instance?.SetTimer(timeLeft);
    }


}