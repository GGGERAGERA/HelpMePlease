using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BunkerRunStarter : MonoBehaviour
{
    [SerializeField] private DepthCatalog depths;
    public DepthCatalog Depths => depths;

    [Header("Starting Sector")]
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

    public void StartRun(Transform transitionTarget, int depthId = DepthCatalog.SurfaceId)
    {
        if (isTransitioning || SceneTransitionOverlay.IsTransitioning)
            return;

        DepthCatalog.Entry depth = depths.Find(depthId);
        if (depth == null || depth.GetAvailability(MetaProgressionManager.EnsureExists().EscapeAccess) != DepthAvailability.Available)
            return;
        string gameplaySceneName = depth.gameplaySceneName;
        StageProfileData startingStageProfile = depth.startingStageProfile;

        if (!TryValidateRun(startingStageProfile, out CharacterData character))
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
            Notifications?.ShowError("bunker.run_transition_unavailable");
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
                startingWorldRule, startingLocalAnomaly, stabilizer, depth.id);
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

    private bool TryValidateRun(StageProfileData startingStageProfile, out CharacterData character)
    {
        character = null;

        if (RunSelectionManager.Instance == null)
        {
            Notifications?.ShowError("bunker.run_selection_unavailable");
            return false;
        }

        if (RunSelectionManager.Instance.SelectedCharacter == null)
        {
            Notifications?.ShowWarning("bunker.run_choose_subject");
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
