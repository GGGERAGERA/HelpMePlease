using UnityEngine;

public class RunTimer : MonoBehaviour
{
    [Header("Level")]
    [SerializeField] private LevelNodeData defaultLevel;
    [SerializeField] private float runDuration = 70f;

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
        ApplySelectedLevel();
        timeLeft = runDuration;
        HUDManager.Instance?.SetTimer(timeLeft);
    }

    private void Update()
    {
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

    private void ApplySelectedLevel()
    {
        LevelNodeData level = RunStateManager.Instance != null
            ? RunStateManager.Instance.SelectedLevelNode
            : null;

        if (level == null)
            level = defaultLevel;

        if (level == null)
            return;

        runDuration = level.Duration;

        if (level.BossPrefab != null)
            bossPrefab = level.BossPrefab;

        Debug.Log(
            $"[RunTimer] Level '{level.nodeName}': {runDuration:F0}s, " +
            $"boss '{(bossPrefab != null ? bossPrefab.name : "not assigned")}'."
        );
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
        RunMessageService.Instance?.Show(RunMessageType.BossIncoming);

        FindFirstObjectByType<EnemySpawner>()?.StopSpawning();


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

    // Kept for the existing result UI. The finite level flow no longer enters survival mode.
    public bool IsSurvivalPhaseStarted()
    {
        return false;
    }

    public float GetSurvivalTime()
    {
        return 0f;
    }
}
