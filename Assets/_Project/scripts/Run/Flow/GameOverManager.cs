using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance;

    [SerializeField] private RunResultView runResultView;
    private int runId;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal void DebugBindRun(int id) => runId = id;
#endif

    void Awake()
    {
        runId = RunStateManager.Instance != null ? RunStateManager.Instance.RunId : 0;
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

    }

    public void GameOver()
    {
        if (RunStateManager.Instance == null || !RunStateManager.Instance.IsActiveRun(runId)) return;
        if (RunFlowController.Instance != null && RunFlowController.Instance.IsVictoryConfirmed) return;
        UpgradeManager.Instance?.CancelPendingRewards();
        HUDManager.Instance?.HideLowHpVignette();

        if (runResultView != null)
            runResultView.Show(false);

        Time.timeScale = 0f;
    }

    public void RestartGame()
    {
        if (RunStateManager.Instance == null || !RunStateManager.Instance.IsActiveRun(runId)) return;
        RunEndService.Instance?.RestartRun(RunEndReason.PlayerDied);
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public void MainMenu()
    {
        if (RunStateManager.Instance == null || !RunStateManager.Instance.IsActiveRun(runId)) return;
        if (RunEndService.Instance == null)
        {
            Debug.LogError(
                "[GameOverManager] RunEndService is missing."
            );

            return;
        }

        RunEndService.Instance.EndRunAfterDeath();
    }
}
