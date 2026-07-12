using TMPro;
using UnityEngine;

public class RunResultView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI aiCommentText;


    public void Show(bool victory)
    {
        gameObject.SetActive(true);

        int kills = RunStatsManager.Instance != null ? RunStatsManager.Instance.Kills : 0;
        float time = RunStatsManager.Instance != null ? RunStatsManager.Instance.RunTime : 0f;
        int level = ExperienceManager.Instance != null ? ExperienceManager.Instance.currentLevel : 1;



        int totalGold = CurrencyManager.Instance != null ? CurrencyManager.Instance.TotalGold : 0;

        if (titleText != null)
        {
            titleText.text = victory ? "VICTORY" : "YOU DIED";
            titleText.color = victory ? Color.green : Color.red;
        }

        if (statsText != null)
        {
            string result =
                $"TIME: {FormatTime(time)}\n" +
                $"KILLS: {kills}\n" +
                $"LEVEL: {level}\n";

            RunTimer runTimer = FindAnyObjectByType<RunTimer>();

            if (runTimer != null && runTimer.IsSurvivalPhaseStarted())
            {
                result += $"SURVIVED: {FormatTime(runTimer.GetSurvivalTime())}\n";
            }

            result +=
                $"TOTAL GOLD: {totalGold}";

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