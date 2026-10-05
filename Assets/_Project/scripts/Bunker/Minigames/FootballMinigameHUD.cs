using TMPro;
using UnityEngine;

public sealed class FootballMinigameHUD : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text goalStatsText;

    private int displayedGoalCount;
    private int displayedGoalPoints;
    private bool completed;
    private bool completedRecord;
    private bool completedBenchmark;

    private void OnEnable() => LocalizationService.Instance.LanguageChanged += HandleLanguageChanged;
    private void OnDisable()
    {
        if (LocalizationService.Instance != null)
            LocalizationService.Instance.LanguageChanged -= HandleLanguageChanged;
    }
    private void HandleLanguageChanged(GameLanguage language)
    {
        SetGoalStats(displayedGoalCount, displayedGoalPoints);
        if (completed) RefreshCompletedResult();
    }

    public void SetGoalStats(int count, int points)
    {
        displayedGoalCount = count;
        displayedGoalPoints = points;
        goalStatsText.text = string.Format(LocalizationService.Instance.Get("bunker.football_goals"), count, points);
    }

    public void ShowIdle(float duration, int bestScore)
    {
        completed = false;
        SetValues(duration, 0, bestScore);
        SetGoalStats(0, 0);
        SetResult(string.Empty);
        SetVisible(false);
    }

    public void ShowRunning(float remainingTime, int score, int bestScore)
    {
        completed = false;
        SetVisible(true);
        SetValues(remainingTime, score, bestScore);
        SetResult(string.Empty);
    }

    public void ShowCompleted(int score, int bestScore, bool newRecord, bool devReward = false)
    {
        SetVisible(false);
        SetValues(0f, score, bestScore);
        completed = true;
        completedRecord = newRecord;
        completedBenchmark = devReward;
        RefreshCompletedResult();
    }

    private void RefreshCompletedResult()
    {
        bool newRecord = completedRecord;
        bool devReward = completedBenchmark;
        string result = newRecord
            ? string.Format(LocalizationService.Instance.Get("bunker.football_record"), FootballMinigame.PersonalRecordGoldReward)
            : LocalizationService.Instance.Get("bunker.football_complete");
        if (devReward)
            result = (newRecord ? result + "\n" : string.Empty)
                + string.Format(LocalizationService.Instance.Get("bunker.football_benchmark_reward"), FootballMinigame.DevRecordGoldReward);
        SetResult(result);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    private void SetValues(float time, int score, int best)
    {
        if (timeText != null)
            timeText.text = $"{time:0.0}";
        if (scoreText != null)
            scoreText.text = score.ToString();
        if (bestScoreText != null)
            bestScoreText.text = best.ToString();
    }

    private void SetResult(string value)
    {
        if (resultText != null)
            resultText.text = value;
    }

    private void SetVisible(bool visible)
    {
        if (panelRoot != null && panelRoot.activeSelf != visible)
            panelRoot.SetActive(visible);
    }
}
