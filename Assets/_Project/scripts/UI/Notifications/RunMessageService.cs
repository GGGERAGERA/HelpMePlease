using System.Collections;
using UnityEngine;
using TMPro;

public sealed class RunMessageService : MonoBehaviour
{
    private const float InitialLevelMessageDuration = 4.5f;
    private const float FirstRunHintDuration = 4.5f;
    public const string MovementHintPreferenceKey = "onboarding.mvp.movement.seen";
    public static RunMessageService Instance { get; private set; }

    [SerializeField] private RunMessageView view;
    public RunMessageView View => view;
    [SerializeField] private RunMessageData[] messages;
    [SerializeField] private GameObject movementHint;
    [SerializeField] private TextMeshProUGUI movementText;
    [SerializeField] private HUDManager hud;
    private float movementHintRemaining;

    private void Awake()
    {
        movementHint.SetActive(false);
    }

    private void OnEnable() => Instance = this;

    private IEnumerator Start()
    {
        if (PlayerPrefs.GetInt(MovementHintPreferenceKey, 0) != 0) yield break;
        LevelAnomalyController anomalyController =
            LevelAnomalyController.Instance;
        WorldRuleController worldRuleController =
            WorldRuleController.Instance;

        if (anomalyController != null || worldRuleController != null)
        {
            while ((anomalyController != null &&
                    !anomalyController.IsIntroComplete) ||
                   (worldRuleController != null &&
                    !worldRuleController.IsIntroComplete))
            {
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSecondsRealtime(
                InitialLevelMessageDuration
            );
        }

        RunStateManager runState = RunStateManager.Instance;

        if (runState == null || runState.CurrentSector == null ||
            runState.CurrentSector.SectorNumber != RunRoute.FirstSector) yield break;
        movementHintRemaining = FirstRunHintDuration;
    }

    private void LateUpdate()
    {
        bool visible = movementHintRemaining > 0f && hud.IsInformationVisible;
        movementHint.SetActive(visible);
        if (!visible) return;
        movementText.text = "WASD\n" + LocalizationService.Instance.Get("hud.movement");
        movementHintRemaining = Mathf.Max(0f, movementHintRemaining - Time.unscaledDeltaTime);
        if (movementHintRemaining > 0f) return;
        movementHint.SetActive(false);
        PlayerPrefs.SetInt(MovementHintPreferenceKey, 1);
        PlayerPrefs.Save();
    }

    public void Show(RunMessageType type)
    {
        RunMessageData data = FindMessage(type);

        if (data == null)
        {
            Debug.LogWarning($"[RunMessageService] Message not found: {type}");
            return;
        }

        Show(data, type == RunMessageType.BossIncoming);
    }

    public void ShowCustom(
        string title,
        string description,
        float duration = 3f,
        bool useTypewriter = false)
    {
        if (view == null)
            return;

        view.Show(title, description, duration, useTypewriter);
    }

    public void ShowWorldEventFeedback(
        string title,
        string description,
        Color accentColor,
        float duration = 0.45f)
    {
        if (view == null)
            return;

        view.ShowWorldEventFeedback(
            title,
            description,
            accentColor,
            duration
        );
    }

    private void OnDisable()
    {
        movementHint.SetActive(false);
        if (Instance == this)
            Instance = null;
    }

    private void Show(RunMessageData data, bool useTypewriter)
    {
        if (view == null || data == null)
            return;

        view.Show(
            data.title,
            data.description,
            data.duration,
            useTypewriter
        );

        if (data.sound != null)
        {
            Vector3 position = Camera.main != null
                ? Camera.main.transform.position
                : transform.position;

            AudioService.Instance?.PlayExternalOneShot(
                data.sound,
                position,
                data.volume,
                AudioCategory.UI
            );
        }
    }

    private RunMessageData FindMessage(RunMessageType type)
    {
        if (messages == null)
            return null;

        foreach (RunMessageData message in messages)
        {
            if (message != null && message.messageType == type)
                return message;
        }

        return null;
    }
}
