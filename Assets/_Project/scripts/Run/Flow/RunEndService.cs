using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RunEndService : MonoBehaviour
{
    public static RunEndService Instance { get; private set; }

    [SerializeField] private string bunkerSceneName = "MainMenu";

    private bool isEndingRun;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ReturnToBunker()
    {
        EndRun(RunEndReason.ReturnedToBunker);
    }

    public void EndRunAfterDeath()
    {
        EndRun(RunEndReason.PlayerDied);
    }

    public void CompleteRunVictory()
    {
        if (isEndingRun)
            return;

        RunStateManager runState = RunStateManager.EnsureExists();
        RunSector sector = runState.CurrentSector;

        if (sector == null || !RunRoute.IsBossSector(sector.SectorNumber))
        {
            Debug.LogError(
                $"[RunEndService] Victory requires CurrentSector " +
                $"{RunRoute.FinalBossSector}."
            );
            return;
        }

        isEndingRun = true;
        StopActiveGameplay();
        runState.CommitCurrentSceneStats();
        runState.RegisterCompletedLevel();

        UnlockProgressService.Instance?.AddProgressByCondition(
            UnlockConditionType.CompleteRun,
            string.Empty,
            1
        );

        RunSummary summary = runState.EndRun(RunEndReason.Victory);
        ClearActiveSectorEffects();

        Debug.Log(
            $"[RunEndService] Victory. Returning to bunker. " +
            $"Gold earned: {summary?.GoldEarned ?? 0}"
        );

        Time.timeScale = 1f;
        SceneManager.LoadScene(bunkerSceneName);
    }

    private void EndRun(RunEndReason reason)
    {
        if (isEndingRun)
            return;

        isEndingRun = true;

        RunStateManager runState = RunStateManager.EnsureExists();
        RunSummary summary = runState.EndRun(reason);

        Debug.Log(
            $"[RunEndService] Returning to bunker. " +
            $"Gold earned: {summary?.GoldEarned ?? 0}"
        );

        Time.timeScale = 1f;
        SceneManager.LoadScene(bunkerSceneName);
    }

    private static void StopActiveGameplay()
    {
        FindFirstObjectByType<EnemySpawner>()?.StopSpawning();
        FindFirstObjectByType<RunTimer>()?.StopTimer();

        WorldEventSpawner eventSpawner =
            FindFirstObjectByType<WorldEventSpawner>();

        if (eventSpawner != null)
            eventSpawner.enabled = false;
    }

    private static void ClearActiveSectorEffects()
    {
        WorldRuleController.Instance?.Clear();
        LevelAnomalyController.Instance?.Clear();
    }
}
