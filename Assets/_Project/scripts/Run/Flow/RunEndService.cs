using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RunEndService : MonoBehaviour
{
    public static RunEndService Instance { get; private set; }

    [SerializeField] private string bunkerSceneName = "MainMenu";

    private bool isEndingRun;
    private int runId;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal void DebugBindRun(int id) => runId = id;
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        runId = RunStateManager.Instance != null ? RunStateManager.Instance.RunId : 0;
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
        if (isEndingRun || !SceneTransitionOverlay.CanLoad(bunkerSceneName))
            return;

        RunStateManager runState = RunStateManager.EnsureExists();
        RunSector sector = runState.CurrentSector;

        if (sector == null || !RunRoute.IsFinalSector(sector.SectorNumber) ||
            RunFlowController.Instance == null || !RunFlowController.Instance.IsVictoryConfirmed)
        {
            Debug.LogError(
                $"[RunEndService] Victory requires CurrentSector " +
                $"{RunRoute.FinalSector} and a confirmed final boss death."
            );
            return;
        }

        EndRun(RunEndReason.Victory);
    }

    private void EndRun(RunEndReason reason)
    {
        if (isEndingRun || !SceneTransitionOverlay.CanLoad(bunkerSceneName))
            return;
        var runState = RunStateManager.Instance;
        if (runState == null || !runState.IsActiveRun(runId)) return;

        SceneTransitionOverlay.Load(bunkerSceneName, () =>
        {
            isEndingRun = true;
            RunSummary summary = runState.EndRun(reason, runId);

            Debug.Log(
                $"[RunEndService] Returning to bunker. " +
                $"Gold earned: {summary?.GoldEarned ?? 0}"
            );

        });
    }

    public void RestartRun(RunEndReason reason)
    {
        var runState = RunStateManager.Instance;
        string scene = SceneManager.GetActiveScene().name;
        if (isEndingRun || runState == null || !runState.IsActiveRun(runId) ||
            !SceneTransitionOverlay.CanLoad(scene)) return;
        SceneTransitionOverlay.Load(scene, () =>
        {
            isEndingRun = true;
            runState.RestartRun(reason, runId);
        });
    }

    // Recovery can be offered after the gameplay scene has already unloaded.
    public static void RecoverToBunker()
    {
        var runState = RunStateManager.Instance;
        int expectedRunId = runState != null ? runState.RunId : 0;
        SceneTransitionOverlay.Load("MainMenu",
            () => runState?.EndRun(RunEndReason.ReturnedToBunker, expectedRunId));
    }
}
