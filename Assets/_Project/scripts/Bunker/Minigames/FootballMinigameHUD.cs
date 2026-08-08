using TMPro;
using UnityEngine;

public sealed class FootballMinigameHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private TMP_Text resultText;

    public void ShowIdle(float duration, int bestScore)
    {
        SetValues(duration, 0, bestScore);
        SetResult(string.Empty);
    }

    public void ShowRunning(float remainingTime, int score, int bestScore)
    {
        SetValues(remainingTime, score, bestScore);
        SetResult(string.Empty);
    }

    public void ShowCompleted(int score, int bestScore, bool newRecord)
    {
        SetValues(0f, score, bestScore);
        SetResult(newRecord ? "NEW RECORD!" : "ROUND COMPLETE");
    }

    private void SetValues(float time, int score, int best)
    {
        if (timeText != null)
            timeText.text = $"TIME {time:0.0}";
        if (scoreText != null)
            scoreText.text = $"SCORE {score}";
        if (bestScoreText != null)
            bestScoreText.text = $"BEST {best}";
    }

    private void SetResult(string value)
    {
        if (resultText != null)
            resultText.text = value;
    }
}
