using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BunkerRunStarter : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "MVP";

    [Header("Starting Sector")]
    [SerializeField] private StageProfileData startingStageProfile;
    [SerializeField] private WorldRuleData startingWorldRule;
    [SerializeField] private LocalAnomalyData startingLocalAnomaly;

    [Header("Run Transition")]
    [SerializeField] private Camera transitionCamera;
    [SerializeField] private Transform cameraRig;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private Behaviour playerMovement;
    [SerializeField] private BunkerCursorInteractor bunkerCursor;
    [SerializeField] private BunkerGateVisual runGate;
    [SerializeField, Min(0.5f)] private float targetOrthographicSize = 3.5f;
    [SerializeField, Range(0f, 1f)] private float gateOpenNormalizedTime = 0.35f;

    private bool isTransitioning;

    public bool IsTransitioning => isTransitioning;

    private BunkerNotificationManager Notifications =>
    BunkerContext.Instance != null ? BunkerContext.Instance.Notifications : null;

    public void StartRun(Transform transitionTarget)
    {
        if (isTransitioning || SceneTransitionOverlay.IsTransitioning)
            return;

        if (!TryValidateRun(out CharacterData character))
            return;

        if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            Debug.LogError($"[BunkerRunStarter] Scene '{gameplaySceneName}' is not available.", this);
            return;
        }

        if (transitionTarget == null ||
            transitionCamera == null ||
            cameraRig == null ||
            cameraFollow == null ||
            runGate == null)
        {
            Debug.LogError(
                "[BunkerRunStarter] Run transition references are incomplete.",
                this);
            Notifications?.ShowError("Переход запуска не настроен.");
            return;
        }

        Vector3 startPosition = cameraRig.position;
        Vector3 targetPosition = transitionTarget.position;
        targetPosition.z = startPosition.z;
        float startSize = transitionCamera.orthographicSize;
        bool gateOpened = false;
        SceneTransitionOverlay.Load(gameplaySceneName, () =>
        {
            isTransitioning = true;
            BunkerContext.Instance?.Panels?.CloseAll(false);
            AnomalyStabilizerData stabilizer = RunSelectionManager.Instance.ConsumeAnomalyStabilizer();
            RunStateManager.EnsureExists().BeginNewRun(character, null, startingStageProfile,
                startingWorldRule, startingLocalAnomaly, stabilizer);
            AudioService.Instance?.Play(AudioCueId.StartRun);
        }, t =>
        {
            if (this == null || cameraFollow == null) return;
            cameraFollow.enabled = false;
            if (!gateOpened && t >= gateOpenNormalizedTime) { runGate.Open(); gateOpened = true; }
            cameraRig.position = Vector3.LerpUnclamped(startPosition, targetPosition, t);
            transitionCamera.orthographicSize = Mathf.Lerp(startSize, targetOrthographicSize, t);
        });
    }

    private bool TryValidateRun(out CharacterData character)
    {
        character = null;

        if (RunSelectionManager.Instance == null)
        {
            Notifications?.ShowError("Система выбора не найдена.");
            return false;
        }

        if (RunSelectionManager.Instance.SelectedCharacter == null)
        {
            Notifications?.ShowWarning("Сначала выбери персонажа.");
            return false;
        }

        character = RunSelectionManager.Instance.SelectedCharacter;

        if (startingStageProfile == null ||
            startingWorldRule == null ||
            startingLocalAnomaly == null)
        {
            Debug.LogError(
                "[BunkerRunStarter] Starting sector configuration is incomplete.",
                this
            );
            return false;
        }

        if (startingStageProfile.SectorNumber != 1)
        {
            Debug.LogError(
                "[BunkerRunStarter] Starting StageProfile must be sector 1.",
                this
            );
            return false;
        }

        return true;
    }

}
