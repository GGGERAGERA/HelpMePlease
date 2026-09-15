using UnityEngine;

public sealed class DoubleOrLeavePanel : MonoBehaviour
{
    [Header("Mechanics")]
    [SerializeField] private DoubleOrLeave doubleOrLeave;
    [SerializeField] private RunFlowController runFlow;

    private PlayerHealth playerHealth;
    private DoubleOrLeaveState observedState;
    private bool resetHandled;

    private void OnEnable()
    {
        if (doubleOrLeave != null)
            observedState = doubleOrLeave.State;
    }

    private void OnDisable()
    {
        doubleOrLeave?.ResetState();
    }

    private void Update()
    {
        if (ShouldResetForRunState())
        {
            if (!resetHandled)
            {
                resetHandled = true;
                doubleOrLeave?.ResetState();
                observedState = DoubleOrLeaveState.Inactive;
            }

            return;
        }

        resetHandled = false;
        ShowChallengeResultIfChanged();
    }

    private void ShowChallengeResultIfChanged()
    {
        if (doubleOrLeave == null || doubleOrLeave.State == observedState)
            return;

        DoubleOrLeaveState previousState = observedState;
        observedState = doubleOrLeave.State;

        if (previousState != DoubleOrLeaveState.WaitingForChallenge)
            return;

        if (observedState == DoubleOrLeaveState.Failed)
        {
            RunMessageService.Instance?.ShowCustom(
                "mechanic.lost",
                string.Empty
            );
        }
    }

    private bool ShouldResetForRunState()
    {
        bool playerDead = IsPlayerDead();
        bool runEnded = IsRunEnded();
        bool levelCompleted = runFlow != null && runFlow.IsLevelCompleted;

        return playerDead || runEnded || levelCompleted;
    }

    private bool IsPlayerDead()
    {
        if (playerHealth == null)
            playerHealth = FindFirstObjectByType<PlayerHealth>();

        return playerHealth != null && playerHealth.IsDead;
    }

    private bool IsRunEnded()
    {
        return RunStateManager.Instance != null &&
            RunStateManager.Instance.IsRunEnded;
    }
}
