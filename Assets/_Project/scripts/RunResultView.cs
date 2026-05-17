using TMPro;
using UnityEngine;

public class RunResultView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI resultText;
    private bool goldAdded;

    public void Show(bool victory)
    {
        int kills = RunStatsManager.Instance != null ? RunStatsManager.Instance.Kills : 0;
        float time = RunStatsManager.Instance != null ? RunStatsManager.Instance.RunTime : 0f;
        int level = ExperienceManager.Instance != null ? ExperienceManager.Instance.currentLevel : 1;

        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);

        int goldEarned = RunRewardCalculator.CalculateGold(victory);

        if (!goldAdded)
        {
            CurrencyManager.Instance?.AddGold(goldEarned);
            goldAdded = true;
        }

        int totalGold = CurrencyManager.Instance != null ? CurrencyManager.Instance.TotalGold : 0;

        if (resultText != null)
        {
            resultText.text =
                $"TIME: {minutes:00}:{seconds:00}\n" +
                $"KILLS: {kills}\n" +
                $"LEVEL: {level}\n" +
                $"GOLD: +{goldEarned}\n" +
                $"TOTAL GOLD: {totalGold}";
        }
    }
}