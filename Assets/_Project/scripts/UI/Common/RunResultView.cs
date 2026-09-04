using TMPro;
using UnityEngine;

public class RunResultView : MonoBehaviour
{
    [SerializeField] private DeathResultPresentation death;
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI aiCommentText;


    private bool missingReported;

    public void Show(bool victory)
    {
        if (death == null || titleText == null || statsText == null || aiCommentText == null)
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


        if (!victory && death != null)
        {
            RunSummary summary = RunStateManager.Instance != null
                ? RunStateManager.Instance.GetRunSummarySnapshot(RunEndReason.PlayerDied)
                : new RunSummary(RunEndReason.PlayerDied, 0,
                    RunStatsManager.Instance?.Kills ?? 0,
                    RunStatsManager.Instance?.RunTime ?? 0f, 0)
                {
                    PlayerLevel = ExperienceManager.Instance?.currentLevel ?? 1,
                    SectorNumber = 1
                };
            if (summary != null)
            {
                HUDManager.Instance?.SetCurrentRunCurrency(summary.GoldEarned);
                death.Show(summary, AICommentGenerator.GetComment(false));
                return;
            }
        }
        death?.RestoreLegacyView();

        LocalizationService localization =
            LocalizationService.EnsureExists();

        RunStateManager runState = RunStateManager.Instance;
        int kills = runState != null
            ? runState.GetCurrentRunKills()
            : RunStatsManager.Instance != null
                ? RunStatsManager.Instance.Kills
                : 0;
        float time = runState != null
            ? runState.GetCurrentRunTime()
            : RunStatsManager.Instance != null
                ? RunStatsManager.Instance.RunTime
                : 0f;
        int level = ExperienceManager.Instance != null ? ExperienceManager.Instance.currentLevel : 1;



        RunEndReason endReason = victory
            ? RunEndReason.ReturnedToBunker
            : RunEndReason.PlayerDied;
        int runGold = runState != null
            ? runState.GetCurrentGoldReward(endReason)
            : 0;

        HUDManager.Instance?.SetCurrentRunCurrency(runGold);

        if (titleText != null)
        {
            titleText.text = localization.Get(
                victory ? "result.victory" : "result.defeat"
            );
            titleText.color = victory ? Color.green : Color.red;
        }

        if (statsText != null)
        {
            string result =
                $"{localization.Get("stats.time")}: " +
                $"{FormatTime(time)}\n" +
                $"{localization.Get("stats.kills")}: {kills}\n" +
                $"{localization.Get("stats.level")}: {level}\n";

            RunTimer runTimer = FindAnyObjectByType<RunTimer>();

            if (runTimer != null && runTimer.IsSurvivalPhaseStarted())
            {
                result +=
                    $"{localization.Get("stats.survived")}: " +
                    $"{FormatTime(runTimer.GetSurvivalTime())}\n";
            }

            result +=
                $"{localization.Get("stats.runGold")}: {runGold}";

            statsText.text = result;
        }
        if (aiCommentText != null)
        {
            aiCommentText.text = AICommentGenerator.GetComment(victory);
        }
    }

    private string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        return $"{minutes:00}:{seconds:00}";
    }
}
