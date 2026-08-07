using System.Collections;
using UnityEngine;

public sealed class RunMessageService : MonoBehaviour
{
    private const float InitialLevelMessageDuration = 4.5f;
    private const float FirstRunHintDuration = 4.5f;
    public static RunMessageService Instance { get; private set; }

    [SerializeField] private RunMessageView view;
    [SerializeField] private RunMessageData[] messages;

    private void Awake()
    {
        Instance = this;
    }

    private IEnumerator Start()
    {
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

        if (runState != null && runState.CurrentLevel > 1)
            yield break;

        while (Time.timeScale <= 0f)
            yield return null;

        bool exploration = runState != null &&
            runState.CurrentSector != null &&
            RunRoute.IsExplorationSector(
                runState.CurrentSector.SectorNumber
            );

        ShowCustom(
            string.Empty,
            exploration
                ? "WASD — ДВИЖЕНИЕ\n" +
                  "ИССЛЕДУЙТЕ ANOMALY SITES ИЛИ СРАЗУ ИДИТЕ К EXIT\n" +
                  "E — ВЗАИМОДЕЙСТВИЕ С EVENT"
                : "WASD — ДВИЖЕНИЕ\n" +
                  "ОРУЖИЕ СТРЕЛЯЕТ АВТОМАТИЧЕСКИ\n" +
                  "ПОБЕДИТЕ БОССА",
            FirstRunHintDuration,
            true
        );

        float visibleTime = 0f;

        while (visibleTime < FirstRunHintDuration + 0.5f)
        {
            if (Time.timeScale <= 0f)
            {
                view?.HideInstant();
                yield break;
            }

            visibleTime += Time.unscaledDeltaTime;
            yield return null;
        }
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
