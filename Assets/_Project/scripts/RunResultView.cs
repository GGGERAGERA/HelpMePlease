using TMPro;
using UnityEngine;

public class RunResultView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI resultText;

    public void Show()
    {
        int kills = RunStatsManager.Instance != null ? RunStatsManager.Instance.Kills : 0;
        float time = RunStatsManager.Instance != null ? RunStatsManager.Instance.RunTime : 0f;
        int level = ExperienceManager.Instance != null ? ExperienceManager.Instance.currentLevel : 1;

        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);

        if (resultText != null)
        {
            resultText.text =
                $"TIME: {minutes:00}:{seconds:00}\n" +
                $"KILLS: {kills}\n" +
                $"LEVEL: {level}";
        }
    }
}