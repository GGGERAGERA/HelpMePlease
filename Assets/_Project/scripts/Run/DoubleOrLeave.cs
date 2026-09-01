using UnityEngine;

public enum DoubleOrLeaveState
{
    Inactive,
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

    public void TrackStartedEvent(
        WorldEvent worldEvent,
        WorldEventDifficulty difficulty
    )
    {
        if (worldEvent == null)
            return;

        selectedEvent = worldEvent;
        selectedRisk = difficulty == WorldEventDifficulty.Risk;
        State = selectedRisk
            ? DoubleOrLeaveState.WaitingForChallenge
            : DoubleOrLeaveState.Inactive;
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
        selectedEvent = null;
        selectedRisk = false;
        State = DoubleOrLeaveState.Inactive;
    }
}
