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
    [SerializeField] private NoDamageChallenge noDamageChallenge;
    [SerializeField, Min(0)] private int rewardAmount = 1;

    public int RewardAmount => rewardAmount;
    public int LastGrantedRewardAmount { get; private set; }
    public bool HasPendingChoice { get; private set; }
    public bool IsWaitingForChallenge { get; private set; }
    public DoubleOrLeaveState State { get; private set; }

    private void OnEnable()
    {
        ResolveReferences();

        if (worldEventSpawner != null)
            worldEventSpawner.EventCompleted += HandleWorldEventCompleted;

        if (noDamageChallenge != null)
        {
            noDamageChallenge.Completed += HandleChallengeCompleted;
            noDamageChallenge.Failed += HandleChallengeFailed;
        }
    }

    private void OnDisable()
    {
        if (worldEventSpawner != null)
            worldEventSpawner.EventCompleted -= HandleWorldEventCompleted;

        if (noDamageChallenge != null)
        {
            noDamageChallenge.Completed -= HandleChallengeCompleted;
            noDamageChallenge.Failed -= HandleChallengeFailed;
        }
    }

    public void TakeReward()
    {
        if (!HasPendingChoice)
            return;

        HasPendingChoice = false;
        LastGrantedRewardAmount = RewardAmount;
        State = DoubleOrLeaveState.RewardGranted;

        Debug.Log(
            $"[DoubleOrLeave] Reward taken: {LastGrantedRewardAmount}."
        );
    }

    public void DoubleReward()
    {
        if (!HasPendingChoice)
            return;

        HasPendingChoice = false;
        IsWaitingForChallenge = true;
        LastGrantedRewardAmount = 0;
        State = DoubleOrLeaveState.WaitingForChallenge;
    }

    public void ResetState()
    {
        HasPendingChoice = false;
        IsWaitingForChallenge = false;
        LastGrantedRewardAmount = 0;
        State = DoubleOrLeaveState.Inactive;
    }

    private void HandleWorldEventCompleted(WorldEvent worldEvent)
    {
        if (HasPendingChoice || IsWaitingForChallenge)
            return;

        HasPendingChoice = true;
        LastGrantedRewardAmount = 0;
        State = DoubleOrLeaveState.WaitingForChoice;
    }

    private void HandleChallengeCompleted()
    {
        if (!IsWaitingForChallenge)
            return;

        IsWaitingForChallenge = false;
        LastGrantedRewardAmount = RewardAmount * 2;
        State = DoubleOrLeaveState.RewardGranted;

        Debug.Log(
            $"[DoubleOrLeave] Doubled reward granted: " +
            $"{LastGrantedRewardAmount}."
        );
    }

    private void HandleChallengeFailed()
    {
        if (!IsWaitingForChallenge)
            return;

        IsWaitingForChallenge = false;
        LastGrantedRewardAmount = 0;
        State = DoubleOrLeaveState.Failed;

        Debug.Log("[DoubleOrLeave] Challenge failed. Reward lost.");
    }

    private void ResolveReferences()
    {
        if (worldEventSpawner == null)
            worldEventSpawner = FindFirstObjectByType<WorldEventSpawner>();

        if (noDamageChallenge == null)
            noDamageChallenge = FindFirstObjectByType<NoDamageChallenge>();
    }
}
