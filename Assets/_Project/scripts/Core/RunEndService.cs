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
}