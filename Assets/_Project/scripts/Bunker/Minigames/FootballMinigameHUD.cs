using TMPro;
using UnityEngine;

public sealed class FootballMinigameHUD : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private TMP_Text resultText;

    public void ShowIdle(float duration, int bestScore)
    {
        SetValues(duration, 0, bestScore);
        SetResult(string.Empty);
        SetVisible(false);
    }

    public void ShowRunning(float remainingTime, int score, int bestScore)
    {
        SetVisible(true);
        SetValues(remainingTime, score, bestScore);
        SetResult(string.Empty);
    }

    public void ShowCompleted(int score, int bestScore, bool newRecord)
    {
        SetVisible(true);
        SetValues(0f, score, bestScore);
        SetResult(newRecord ? "НОВЫЙ РЕКОРД!" : "РАУНД ЗАВЕРШЁН");
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
