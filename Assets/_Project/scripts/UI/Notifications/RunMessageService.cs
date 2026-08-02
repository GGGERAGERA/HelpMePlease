using System.Collections;
using System;
using UnityEngine;

public sealed class RunMessageService : MonoBehaviour
{
    private const float InitialLevelMessageDuration = 4.5f;
    private const float FirstRunHintDuration = 4.5f;
    private const float WorldEventStartDuration = 0.4f;

    public static RunMessageService Instance { get; private set; }

    [SerializeField] private RunMessageView view;
    [SerializeField] private RunMessageData[] messages;

    private WorldEvent worldEventPresentationOwner;

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

        ShowCustom(
            string.Empty,
            "WASD — ДВИЖЕНИЕ\n" +
            "ОРУЖИЕ СТРЕЛЯЕТ АВТОМАТИЧЕСКИ\n" +
            "ПЕРЕЖИВИТЕ ВОЛНУ И ПОБЕДИТЕ БОССА",
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
        if (worldEventPresentationOwner != null)
            return;

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
        if (view == null || worldEventPresentationOwner != null)
            return;

        view.Show(title, description, duration, useTypewriter);
    }

    public void ShowWorldEventFeedback(
        string title,
        string description,
        Color accentColor,
        float duration = 0.45f)
    {
        if (view == null || worldEventPresentationOwner != null)
            return;

        view.ShowWorldEventFeedback(
            title,
            description,
            accentColor,
            duration
        );
    }

    public bool ShowWorldEventStart(
        WorldEvent worldEvent,
        string displayName,
        Color accentColor,
        Action onComplete)
    {
        if (view == null || worldEvent == null ||
            worldEventPresentationOwner != null)
        {
            return false;
        }

        worldEventPresentationOwner = worldEvent;
        view.ShowWorldEventStart(
            worldEvent,
            displayName,
            accentColor,
            WorldEventStartDuration,
            () => FinishWorldEventStart(worldEvent, onComplete)
        );
        return true;
    }

    public void CancelWorldEventStart(WorldEvent worldEvent)
    {
        if (worldEvent == null ||
            worldEventPresentationOwner != worldEvent)
        {
            return;
        }

        view?.CancelWorldEventStart(worldEvent);
        worldEventPresentationOwner = null;
    }

    private void FinishWorldEventStart(
        WorldEvent worldEvent,
        Action onComplete)
    {
        if (worldEventPresentationOwner != worldEvent)
            return;

        worldEventPresentationOwner = null;
        onComplete?.Invoke();
    }

    private void OnDisable()
    {
        if (worldEventPresentationOwner != null)
            CancelWorldEventStart(worldEventPresentationOwner);

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
