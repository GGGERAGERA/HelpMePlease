using System;
using UnityEngine;

public enum DoubleOrLeaveState
{
    Inactive,
    WaitingForChoice,
    WaitingForChallenge,
    RewardGranted,
    Failed
}

public sealed class DoubleOrLeave : MonoBehaviour
{
    [SerializeField] private WorldEventSpawner worldEventSpawner;

    public bool HasPendingChoice { get; private set; }
    public bool IsWaitingForChallenge { get; private set; }
    public DoubleOrLeaveState State { get; private set; }

    private Action takeRewardAction;
    private Action riskRewardAction;
    private WorldEvent riskyEvent;

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        ResetState();
    }

    public bool BeginRewardChoice(Action takeReward, Action riskReward)
    {
        if (HasPendingChoice || IsWaitingForChallenge)
            return false;

        takeRewardAction = takeReward;
        riskRewardAction = riskReward;
        HasPendingChoice = true;
        State = DoubleOrLeaveState.WaitingForChoice;
        return true;
    }

    public void TakeReward()
    {
        if (!HasPendingChoice)
            return;

        HasPendingChoice = false;
        State = DoubleOrLeaveState.RewardGranted;

        Action action = takeRewardAction;
        ClearChoiceActions();
        action?.Invoke();
    }

    public void RiskReward()
    {
        if (!HasPendingChoice)
            return;

        HasPendingChoice = false;
        IsWaitingForChallenge = true;
        State = DoubleOrLeaveState.WaitingForChallenge;

        Action action = riskRewardAction;
        ClearChoiceActions();
        action?.Invoke();
    }

    public bool TryBeginRiskyEvent(WorldEvent worldEvent)
    {
        if (!IsWaitingForChallenge || riskyEvent != null || worldEvent == null)
            return false;

        riskyEvent = worldEvent;
        return true;
    }

    public bool ResolveCompletedEvent(WorldEvent worldEvent)
    {
        if (worldEvent == null || worldEvent != riskyEvent)
            return false;

        riskyEvent = null;
        IsWaitingForChallenge = false;
        State = DoubleOrLeaveState.RewardGranted;
        return true;
    }

    public bool ResolveFailedEvent(WorldEvent worldEvent)
    {
        if (worldEvent == null || worldEvent != riskyEvent)
            return false;

        riskyEvent = null;
        IsWaitingForChallenge = false;
        State = DoubleOrLeaveState.Failed;
        return true;
    }

    public void ResetState()
    {
        HasPendingChoice = false;
        IsWaitingForChallenge = false;
        riskyEvent = null;
        ClearChoiceActions();
        State = DoubleOrLeaveState.Inactive;
    }

    private void ClearChoiceActions()
    {
        takeRewardAction = null;
        riskRewardAction = null;
    }

    private void ResolveReferences()
    {
        if (worldEventSpawner == null)
            worldEventSpawner = FindFirstObjectByType<WorldEventSpawner>();
    }
}
