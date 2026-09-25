using Subject42.Combat.OrbitalStation;
using UnityEngine;

public class PauseMenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private PauseBuildOverview overview;
    [SerializeField] private AudioSettingsPanel audioSettingsPanel;
    [SerializeField] private CharacterSpawner characterSpawner;
    [SerializeField] private BunkerPanelManager bunkerPanels;
    [SerializeField] private BunkerIntroController bunkerIntro;

    private bool isPaused;
    private bool settingsOpen;
    private float resumeTimeScale = 1f;
    private int runId;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal void DebugBindRun(int id) => runId = id;
#endif
    public bool IsPaused => isPaused;
    private bool IsBunker => bunkerPanels != null;
    private bool RewardsOpen => !IsBunker && UpgradeManager.Instance != null && !UpgradeManager.Instance.IsRewardQueueIdle;
    private bool RunEnded => !IsBunker && RunStateManager.Instance != null && RunStateManager.Instance.IsRunEnded;

    private OrbitalInteractionController Interaction => characterSpawner != null && characterSpawner.SpawnedPlayer != null
        ? characterSpawner.SpawnedPlayer.GetComponentInChildren<OrbitalInteractionController>()
        : null;

    private void Awake()
    {
        if (pausePanel != null && overview != null && audioSettingsPanel != null &&
            (IsBunker ? bunkerIntro != null : characterSpawner != null)) return;
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
        if (SceneTransitionOverlay.IsTransitioning || (bunkerIntro != null && bunkerIntro.IsPlaying)) return;
        if (isPaused && overview.IsConfirming) { overview.CancelConfirmation(); return; }
        if (settingsOpen) { audioSettingsPanel?.Close(); return; }
        if (isPaused) { Resume(); return; }
        if (IsBunker && bunkerPanels.TryCloseForEscape()) return;
        var interaction = Interaction;
        if (interaction != null && interaction.TryConsumeEscape()) return;
        Pause();
    }

    public void Pause()
    {
        if (Interaction != null && Interaction.IsCustomDrawing) return;
        if (SceneTransitionOverlay.IsTransitioning || isPaused || RunEnded || RewardsOpen ||
            (bunkerIntro != null && bunkerIntro.IsPlaying) || (IsBunker && bunkerPanels.IsAnyPanelOpen) || Time.timeScale <= 0f) return;

        Interaction?.PrepareForExternalPause();
        resumeTimeScale = Time.timeScale;
        isPaused = true;
        TutorialController.Active?.SetHintVisible(false);

        if (pausePanel != null)
            pausePanel.SetActive(true);

        UpdateLocalizedContent();

        Time.timeScale = 0f;
    }

    public void Resume()
    {
        if (SceneTransitionOverlay.IsTransitioning || !isPaused) return;
        ReleaseRunScene();
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
        if (IsBunker || !isPaused || settingsOpen) return;
        overview.AskConfirmation("pause.confirmBunker", ReturnToBunker);
    }

    private void ReturnToBunker()
    {
        if (RunStateManager.Instance == null || !RunStateManager.Instance.IsActiveRun(runId)) return;
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
        if (IsBunker || !isPaused || settingsOpen) return;
        overview.AskConfirmation("pause.confirmRestart", RestartConfirmed);
    }

    private void RestartConfirmed()
    {
        if (RunStateManager.Instance == null || !RunStateManager.Instance.IsActiveRun(runId)) return;
        RunEndService.Instance?.RestartRun(RunEndReason.ReturnedToBunker);
    }

    private void OnEnable()
    {
        if (IsBunker) return;
        var run = RunStateManager.EnsureExists();
        runId = run.RunId;
        run.RegisterSceneCleanup(ReleaseRunScene);
    }
    private void OnDisable()
    {
        if (!IsBunker) RunStateManager.Instance?.UnregisterSceneCleanup(ReleaseRunScene);
        ReleaseRunScene();
    }

    private void ReleaseRunScene()
    {
        if (isPaused && !SceneTransitionOverlay.IsTransitioning && !RewardsOpen && !RunEnded)
            Time.timeScale = resumeTimeScale;
        bool closeSettings = settingsOpen;
        isPaused = settingsOpen = false;
        resumeTimeScale = 1f;
        overview?.CancelConfirmation();
        if (closeSettings) audioSettingsPanel?.Close();
        if (pausePanel != null) pausePanel.SetActive(false);
        TutorialController.Active?.SetHintVisible(true);
    }

    private void UpdateLocalizedContent()
    {
        if (!IsBunker) overview.Refresh(RunStateManager.Instance);
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
