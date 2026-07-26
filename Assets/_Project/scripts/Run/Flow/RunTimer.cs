using UnityEngine;

public class RunTimer : MonoBehaviour
{
    [Header("Level")]
    [SerializeField] private LevelNodeData defaultLevel;
    [SerializeField] private float runDuration = 70f;

    [Header("Boss Spawn")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private float spawnDistanceFromPlayer = 8f;
    [SerializeField, Min(0f)] private float bossEdgePadding = 2f;
    [SerializeField, Min(1)] private int spawnPositionAttempts = 24;
    [SerializeField] private GameplayAreaService gameplayArea;

#pragma warning disable CS0414
    [SerializeField] private AudioClip bossWarningSound;
    [SerializeField] private float bossWarningVolume = 0.8f;
#pragma warning restore CS0414
    [SerializeField] private float bossSpawnDelay = 1f;

    private float timeLeft;
    private bool bossSpawned;

    private void Start()
    {
        ResolveGameplayArea();
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
            if (gameplayArea == null)
                ResolveGameplayArea();

            if (gameplayArea == null ||
                !gameplayArea.TryGetSpawnPosition(
                    player.transform.position,
                    spawnDistanceFromPlayer,
                    spawnDistanceFromPlayer,
                    spawnPositionAttempts,
                    bossEdgePadding,
                    out Vector3 spawnPosition))
            {
                Debug.LogWarning(
                    "[RunTimer] No valid boss position exists inside the spawn area.",
                    this
                );
                return;
            }

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
        AudioService.Instance?.Play(AudioCueId.BossSpawn);

        //  FindFirstObjectByType<EnemySpawner>()?.StopSpawning(); спаун врагов во время босса отключен 

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

    private void ResolveGameplayArea()
    {
        if (gameplayArea == null)
            gameplayArea = GameplayAreaService.Instance;

        if (gameplayArea == null)
            gameplayArea = FindFirstObjectByType<GameplayAreaService>();
    }
}
