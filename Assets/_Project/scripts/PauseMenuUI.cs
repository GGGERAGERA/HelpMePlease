using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI titleText;

    private bool isPaused;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }
    }

    public void Pause()
    {
        isPaused = true;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        if (titleText != null)
            titleText.text = "PAUSED";

        UpdateStats();

        Time.timeScale = 0f;
    }

    public void Resume()
    {
        isPaused = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        Time.timeScale = 1f;
    }

    public void MainMenu()
    {
        if (RunEndService.Instance == null)
        {
            Debug.LogError(
                "[PauseMenuUI] RunEndService is missing."
            );

            return;
        }

        RunEndService.Instance.ReturnToBunker();
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        isPaused = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void UpdateStats()
    {
        int kills = RunStatsManager.Instance != null
            ? RunStatsManager.Instance.Kills
            : 0;

        float time = RunStatsManager.Instance != null
            ? RunStatsManager.Instance.RunTime
            : 0f;

        int level = ExperienceManager.Instance != null
            ? ExperienceManager.Instance.currentLevel
            : 1;

        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);

        if (statsText != null)
        {
            statsText.text =
                $"TIME: {minutes:00}:{seconds:00}\n" +
                $"KILLS: {kills}\n" +
                $"LEVEL: {level}";
        }
    }
}