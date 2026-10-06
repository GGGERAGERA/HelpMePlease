using System.Globalization;
using UnityEngine;

public sealed class FootballMinigameHUD : MonoBehaviour
{
    [SerializeField] private MinigameSidePanelView view;

    private int displayedGoalCount;
    private int displayedGoalPoints;
    private bool completed;
    private bool completedRecord;
    private bool completedBenchmark;
    private bool running;

    private void OnEnable()
    {
        LocalizationService.EnsureExists().LanguageChanged += HandleLanguageChanged;
        RefreshContent();
    }
    private void OnDisable()
    {
        if (LocalizationService.Instance != null)
            LocalizationService.Instance.LanguageChanged -= HandleLanguageChanged;
    }
    private void HandleLanguageChanged(GameLanguage language)
    {
        RefreshContent();
    }

    private void RefreshContent()
    {
        var localization = LocalizationService.EnsureExists();
        view.SetContent(localization.Get("football.panel_title"), localization.Get("football.score"),
            localization.Get("football.best"), localization.Get("football.time"), localization.Get("football.controls"));
        if (!completed) view.SetStatus(running ? localization.Get("football.running") : string.Empty);
        else RefreshCompletedResult();
        SetGoalStats(displayedGoalCount, displayedGoalPoints);
    }

    public void SetGoalStats(int count, int points)
    {
        displayedGoalCount = count;
        displayedGoalPoints = points;
        var localization = LocalizationService.EnsureExists();
        view.SetExtraSection(localization.Get("football.legend"),
            string.Format(localization.Get("bunker.football_goals"), count, points), true);
    }

    public void ShowIdle(float duration, int bestScore)
    {
        completed = false;
        running = false;
        SetValues(duration, 0, bestScore);
        SetGoalStats(0, 0);
        SetResult(string.Empty);
        SetVisible(false);
    }

    public void ShowRunning(float remainingTime, int score, int bestScore)
    {
        completed = false;
        running = true;
        SetVisible(true);
        SetValues(remainingTime, score, bestScore);
        SetResult(LocalizationService.Instance.Get("football.running"));
    }

    public void ShowCompleted(int score, int bestScore, bool newRecord, bool devReward = false)
    {
        SetVisible(false);
        SetValues(0f, score, bestScore);
        completed = true;
        running = false;
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
        view.SetValues(time.ToString("0.0", CultureInfo.InvariantCulture),
            score.ToString(CultureInfo.InvariantCulture), best.ToString(CultureInfo.InvariantCulture));
    }

    private void SetResult(string value)
    {
        view.SetStatus(value);
    }

    private void SetVisible(bool visible)
    {
        view.SetVisible(visible);
    }
}
