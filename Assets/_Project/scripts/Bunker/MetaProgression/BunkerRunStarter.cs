using System.Collections;
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
    [SerializeField, Range(0.8f, 1.2f)] private float transitionDuration = 1f;
    [SerializeField, Min(0.5f)] private float targetOrthographicSize = 2.2f;

    private bool isTransitioning;

    public bool IsTransitioning => isTransitioning;

    private BunkerNotificationManager Notifications =>
    BunkerContext.Instance != null ? BunkerContext.Instance.Notifications : null;

    public void StartRun(Transform transitionTarget)
    {
        if (isTransitioning)
            return;

        if (!TryValidateRun(out CharacterData character, out WeaponData weapon))
            return;

        if (transitionTarget == null ||
            transitionCamera == null ||
            cameraRig == null ||
            cameraFollow == null)
        {
            Debug.LogError(
                "[BunkerRunStarter] Run transition references are incomplete.",
                this);
            Notifications?.ShowError("Переход запуска не настроен.");
            return;
        }

        StartCoroutine(PlayTransitionAndStartRun(
            transitionTarget,
            character,
            weapon));
    }

    private bool TryValidateRun(out CharacterData character, out WeaponData weapon)
    {
        character = null;
        weapon = null;

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

        if (RunSelectionManager.Instance.SelectedWeapon == null)
        {
            Notifications?.ShowWarning("Сначала выбери оружие.");
            return false;
        }

        character = RunSelectionManager.Instance.SelectedCharacter;
        weapon = RunSelectionManager.Instance.SelectedWeapon;

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

    private IEnumerator PlayTransitionAndStartRun(
        Transform transitionTarget,
        CharacterData character,
        WeaponData weapon)
    {
        isTransitioning = true;
        Time.timeScale = 1f;
        BunkerContext.Instance?.Panels?.CloseAll(false);

        if (playerMovement != null)
            playerMovement.enabled = false;
        if (bunkerCursor != null)
            bunkerCursor.enabled = false;
        cameraFollow.enabled = false;

        Vector3 startPosition = cameraRig.position;
        Vector3 targetPosition = transitionTarget.position;
        targetPosition.z = startPosition.z;
        float startSize = transitionCamera.orthographicSize;
        float duration = Mathf.Clamp(transitionDuration, 0.8f, 1.2f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeIn = t * t;
            cameraRig.position = Vector3.LerpUnclamped(
                startPosition,
                targetPosition,
                easeIn);
            transitionCamera.orthographicSize = Mathf.Lerp(
                startSize,
                Mathf.Max(0.5f, targetOrthographicSize),
                easeIn);
            yield return null;
        }

        AnomalyStabilizerData stabilizer =
            RunSelectionManager.Instance.ConsumeAnomalyStabilizer();

        RunStateManager.EnsureExists().BeginNewRun(
            character,
            weapon,
            startingStageProfile,
            startingWorldRule,
            startingLocalAnomaly,
            stabilizer
        );

        AudioService.Instance?.Play(AudioCueId.StartRun);
        SceneManager.LoadScene(gameplaySceneName);
    }
}
