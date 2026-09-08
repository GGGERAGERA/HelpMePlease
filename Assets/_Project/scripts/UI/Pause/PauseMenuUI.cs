using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private PauseBuildOverview overview;
    [SerializeField] private AudioSettingsPanel audioSettingsPanel;
    [SerializeField] private CharacterSpawner characterSpawner;

    private bool isPaused;
    private bool settingsOpen;
    private float resumeTimeScale = 1f;
    public bool IsPaused => isPaused;

    private OrbitalInteractionController Interaction => characterSpawner.SpawnedPlayer != null
        ? characterSpawner.SpawnedPlayer.GetComponentInChildren<OrbitalInteractionController>()
        : null;

    private void Awake()
    {
        if (pausePanel != null && overview != null && audioSettingsPanel != null && characterSpawner != null) return;
        Debug.LogError("[PauseMenuUI] Authored overview or scene references are missing.", this);
        enabled = false;
    }

    private void Update()
    {
        if (SceneTransitionOverlay.IsTransitioning) return;
        if (Input.GetKeyDown(KeyCode.Escape)) HandleEscape();
    }

    private void HandleEscape()
    {
        if (isPaused && overview.IsConfirming) { overview.CancelConfirmation(); return; }
        if (settingsOpen) { audioSettingsPanel?.Close(); return; }
        if (isPaused) { Resume(); return; }
        var interaction = Interaction;
        if (interaction != null && interaction.TryConsumeEscape()) return;
        Pause();
    }

    public void Pause()
    {
        if (SceneTransitionOverlay.IsTransitioning || isPaused || (RunStateManager.Instance != null && RunStateManager.Instance.IsRunEnded)) return;
        UpgradeManager rewards = UpgradeManager.Instance;
        bool rewardPaused = rewards != null && !rewards.IsRewardQueueIdle;
        if (Time.timeScale <= 0f && !rewardPaused) return;

        Interaction?.PrepareForExternalPause();
        resumeTimeScale = rewardPaused ? rewards.TimeScaleAfterRewards : Time.timeScale;
        isPaused = true;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        UpdateLocalizedContent();

        Time.timeScale = 0f;
    }

    public void Resume()
    {
        if (SceneTransitionOverlay.IsTransitioning || !isPaused) return;
        overview.CancelConfirmation();
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
        if (!isPaused || settingsOpen || overview.IsConfirming || audioSettingsPanel == null)
            return;

        settingsOpen = true;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        audioSettingsPanel.Open(ReturnFromSettings);
    }

    public void MainMenu()
    {
        if (!isPaused || settingsOpen) return;
        overview.AskConfirmation("pause.confirmBunker", ReturnToBunker);
    }

    private void ReturnToBunker()
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
        if (!isPaused || settingsOpen) return;
        overview.AskConfirmation("pause.confirmRestart", RestartConfirmed);
    }

    private void RestartConfirmed()
    {
        SceneTransitionOverlay.Load(SceneManager.GetActiveScene().name, () =>
        {
            RunStateManager runState = RunStateManager.Instance;

            if (runState != null)
            {
                CharacterData character = runState.SelectedCharacter;
                runState.BeginNewRun(character, null);
            }

            isPaused = false;
        });
    }

    private void UpdateLocalizedContent()
    {
        overview.Refresh(RunStateManager.Instance);
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
