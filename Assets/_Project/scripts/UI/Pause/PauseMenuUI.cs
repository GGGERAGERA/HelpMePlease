using Subject42.Combat.OrbitalStation;
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
    private float resumeTimeScale = 1f;
    public bool IsPaused => isPaused;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) HandleEscape();
    }

    private void HandleEscape()
    {
        var interaction = FindFirstObjectByType<OrbitalInteractionController>();
        if (interaction != null && interaction.TryConsumeEscape()) return;
        if (settingsOpen) { audioSettingsPanel?.Close(); return; }
        if (isPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (isPaused) return;
        UpgradeManager rewards = UpgradeManager.Instance;
        bool rewardPaused = rewards != null && !rewards.IsRewardQueueIdle;
        if (Time.timeScale <= 0f && !rewardPaused) return;

        FindFirstObjectByType<OrbitalInteractionController>()?.PrepareForExternalPause();
        resumeTimeScale = rewardPaused ? rewards.TimeScaleAfterRewards : Time.timeScale;
        isPaused = true;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        UpdateLocalizedContent();

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

        Time.timeScale = UpgradeManager.Instance != null && !UpgradeManager.Instance.IsRewardQueueIdle
            ? 0f : resumeTimeScale;
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
            runState.BeginNewRun(character, null);
        }

        Time.timeScale = 1f;
        isPaused = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void UpdateStats(LocalizationService localization)
    {
        RunStateManager runState = RunStateManager.Instance;
        int kills = runState != null
            ? runState.GetCurrentRunKills()
            : RunStatsManager.Instance != null
                ? RunStatsManager.Instance.Kills
                : 0;

        float time = runState != null
            ? runState.GetCurrentRunTime()
            : RunStatsManager.Instance != null
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
                $"{localization.Get("stats.time")}: " +
                $"{minutes:00}:{seconds:00}\n" +
                $"{localization.Get("stats.kills")}: {kills}\n" +
                $"{localization.Get("stats.level")}: {level}";
        }
    }

    private void UpdateLocalizedContent()
    {
        LocalizationService localization =
            LocalizationService.EnsureExists();

        if (titleText != null)
            titleText.text = localization.Get("pause.title");

        UpdateStats(localization);
    }

    private void ReturnFromSettings()
    {
        settingsOpen = false;

        if (!isPaused)
            return;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        UpdateLocalizedContent();
    }
}
