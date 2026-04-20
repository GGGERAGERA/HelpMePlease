using UnityEngine;
using UnityEngine.UI;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance; // синглтон для простого доступа

    public Text scoreText;               // перетащите сюда ScoreText
    private int currentScore = 0;

    void Awake()
    {
        // Реализуем синглтон
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        UpdateUI();
    }

    public void AddScore(int amount)
    {
        currentScore += amount;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + currentScore;
        else
            Debug.LogError("ScoreManager: scoreText not assigned!");
    }
}