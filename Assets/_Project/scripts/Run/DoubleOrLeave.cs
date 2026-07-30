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
    public DoubleOrLeaveState State { get; private set; }

    private WorldEvent selectedEvent;
    private bool selectedRisk;

    private void OnDisable()
    {
        ResetState();
    }

    public bool BeginEventChoice(
        WorldEvent worldEvent,
        Action<bool> onSelected
    )
    {
        if (worldEvent == null || State == DoubleOrLeaveState.WaitingForChoice)
            return false;

        UpgradeManager upgradeManager = UpgradeManager.Instance;

        if (upgradeManager == null)
            return false;

        selectedEvent = worldEvent;
        State = DoubleOrLeaveState.WaitingForChoice;

        bool shown = upgradeManager.ShowWorldEventModeChoices(
            () => SelectEventMode(false, onSelected),
            () => SelectEventMode(true, onSelected)
        );

        if (shown)
            return true;

        selectedEvent = null;
        State = DoubleOrLeaveState.Inactive;
        return false;
    }

    private void SelectEventMode(bool risk, Action<bool> onSelected)
    {
        if (selectedEvent == null ||
            State != DoubleOrLeaveState.WaitingForChoice)
        {
            return;
        }

        selectedRisk = risk;
        State = risk
            ? DoubleOrLeaveState.WaitingForChallenge
            : DoubleOrLeaveState.Inactive;
        onSelected?.Invoke(risk);
    }

    public bool ResolveCompletedEvent(WorldEvent worldEvent)
    {
        if (worldEvent == null || worldEvent != selectedEvent)
            return false;

        bool wasRisk = selectedRisk;
        selectedEvent = null;
        selectedRisk = false;
        State = DoubleOrLeaveState.RewardGranted;
        return wasRisk;
    }

    public bool ResolveFailedEvent(WorldEvent worldEvent)
    {
        if (worldEvent == null || worldEvent != selectedEvent)
            return false;

        selectedEvent = null;
        selectedRisk = false;
        State = DoubleOrLeaveState.Failed;
        return true;
    }

    public void ResetState()
    {
        UpgradeManager.Instance?.CancelWorldEventModeChoice();
        selectedEvent = null;
        selectedRisk = false;
        State = DoubleOrLeaveState.Inactive;
    }
}
