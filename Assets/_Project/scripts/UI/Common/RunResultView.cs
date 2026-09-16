using UnityEngine;

public class RunResultView : MonoBehaviour
{
    [SerializeField] private DeathResultPresentation death;
    private bool missingReported;

    public void ShowDeath()
    {
        if (death == null)
        {
            if (!missingReported)
            {
                Debug.LogError("[RunResultView] Authored result references are missing.", this);
                missingReported = true;
            }
            enabled = false;
            return;
        }

        gameObject.SetActive(true);
        RunSummary summary = RunStateManager.Instance != null
            ? RunStateManager.Instance.GetRunSummarySnapshot(RunEndReason.PlayerDied)
            : new RunSummary(RunEndReason.PlayerDied, 0,
                RunStatsManager.Instance?.Kills ?? 0,
                RunStatsManager.Instance?.RunTime ?? 0f, 0)
            {
                PlayerLevel = ExperienceManager.Instance?.currentLevel ?? 1,
                SectorNumber = 1
            };
        if (summary == null) return;

        HUDManager.Instance?.SetCurrentRunCurrency(summary.GoldEarned);
        death.Show(summary, AICommentGenerator.GetComment(false));
    }
}
