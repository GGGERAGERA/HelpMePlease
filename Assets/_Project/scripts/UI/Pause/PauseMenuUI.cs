using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private AudioSettingsPanel audioSettingsPanel;

    private bool isPaused;
    private bool settingsOpen;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsOpen)
            {
                audioSettingsPanel?.Close();
                return;
            }

            if (isPaused)
                Resume();
            else
                Pause();
        }
    }

    public void Pause()
    {
        if (!isPaused && Time.timeScale <= 0f)
            return;

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

        if (settingsOpen)
        {
            settingsOpen = false;
            audioSettingsPanel?.Close();
        }

        if (pausePanel != null)
            pausePanel.SetActive(false);

        Time.timeScale = 1f;
    }

    public void OpenSettings()
    {
        if (!isPaused || audioSettingsPanel == null)
            return;

        settingsOpen = true;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        audioSettingsPanel.Open(ReturnFromSettings);
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
        RunStateManager runState = RunStateManager.Instance;

        if (runState != null)
        {
            CharacterData character = runState.SelectedCharacter;
            WeaponData weapon = runState.SelectedWeapon;
            runState.BeginNewRun(character, weapon);
        }

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

    private void ReturnFromSettings()
    {
        settingsOpen = false;

        if (!isPaused)
            return;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        UpdateStats();
    }
}
