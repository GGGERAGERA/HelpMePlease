using UnityEngine;

public class RunTimer : MonoBehaviour
{
    [Header("Level")]
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

    public void StopTimer()
    {
        StopAllCoroutines();
        enabled = false;
    }

    private void Start()
    {
        ResolveGameplayArea();

        if (!ApplyCurrentSector())
        {
            enabled = false;
            return;
        }

        RunStateManager runState = RunStateManager.Instance;
        int sectorNumber = runState != null && runState.CurrentSector != null
            ? runState.CurrentSector.SectorNumber
            : 0;

        if (RunRoute.IsExplorationSector(sectorNumber))
        {
            HUDManager.Instance?.SetTimerVisible(false);
            enabled = false;
            return;
        }

        timeLeft = runDuration;
        HUDManager.Instance?.SetTimerVisible(true);
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

    private bool ApplyCurrentSector()
    {
        RunStateManager runState = RunStateManager.Instance;
        RunSector sector = runState != null
            ? runState.CurrentSector
            : null;

        if (sector == null)
        {
            Debug.LogError(
                "[RunTimer] CurrentSector is missing. " +
                "The timer and boss spawn are disabled.",
                this
            );
            return false;
        }

        runDuration = sector.Duration;
        bossPrefab = sector.BossPrefab;

        Debug.Log(
            $"[RunTimer] Sector {sector.SectorNumber}: " +
            $"{runDuration:F0}s, boss " +
            $"'{(bossPrefab != null ? bossPrefab.name : "not assigned")}'."
        );

        return true;
    }

    private bool SpawnBossObject()
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
                return false;
            }

            Instantiate(bossPrefab, spawnPosition, Quaternion.identity);
            return true;
        }
        else
        {
            Debug.LogWarning("RunTimer: bossPrefab или Player не найден.");
        }

        return false;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool CanDebugSpawnBoss =>
        isActiveAndEnabled &&
        !bossSpawned &&
        bossPrefab != null &&
        GameObject.FindGameObjectWithTag("Player") != null;

    public bool TryDebugSpawnBoss()
    {
        if (!CanDebugSpawnBoss)
            return false;

        StopAllCoroutines();
        bossSpawned = true;
        timeLeft = 0f;
        HUDManager.Instance?.SetTimer(0f);
        RunMessageService.Instance?.Show(RunMessageType.BossIncoming);
        AudioService.Instance?.Play(AudioCueId.BossSpawn);
        CameraShake.Instance?.Shake(2f, 0.05f);

        if (SpawnBossObject())
            return true;

        bossSpawned = false;
        return false;
    }
#endif

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
