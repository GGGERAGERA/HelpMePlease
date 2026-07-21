using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DoubleOrLeavePanel : MonoBehaviour
{
    [Header("View")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Button takeRewardButton;
    [SerializeField] private Button doubleRewardButton;

    [Header("Mechanics")]
    [SerializeField] private DoubleOrLeave doubleOrLeave;
    [SerializeField] private NoDamageChallenge noDamageChallenge;
    [SerializeField] private RunFlowController runFlow;

    private PlayerHealth playerHealth;
    private DoubleOrLeaveState observedState;
    private float previousTimeScale = 1f;
    private bool isOpen;
    private bool resetHandled;

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        takeRewardButton?.onClick.AddListener(TakeReward);
        doubleRewardButton?.onClick.AddListener(DoubleReward);

        if (doubleOrLeave != null)
            observedState = doubleOrLeave.State;
    }

    private void OnDisable()
    {
        takeRewardButton?.onClick.RemoveListener(TakeReward);
        doubleRewardButton?.onClick.RemoveListener(DoubleReward);

        ClosePanel();
        noDamageChallenge?.CancelChallenge();
        doubleOrLeave?.ResetState();
    }

    private void Update()
    {
        if (ShouldResetForRunState())
        {
            if (!resetHandled)
            {
                resetHandled = true;
                ClosePanel();
                noDamageChallenge?.CancelChallenge();
                doubleOrLeave?.ResetState();
                observedState = DoubleOrLeaveState.Inactive;
            }

            return;
        }

        resetHandled = false;
        ShowChallengeResultIfChanged();

        if (!isOpen &&
            Time.timeScale > 0f &&
            doubleOrLeave != null &&
            doubleOrLeave.HasPendingChoice)
        {
            OpenPanel();
        }
    }

    public void TakeReward()
    {
        if (!isOpen || doubleOrLeave == null)
            return;

        doubleOrLeave.TakeReward();
        observedState = doubleOrLeave.State;
        ClosePanel();
    }

    public void DoubleReward()
    {
        if (!isOpen || doubleOrLeave == null || noDamageChallenge == null)
            return;

        doubleOrLeave.DoubleReward();
        observedState = doubleOrLeave.State;
        ClosePanel();
        noDamageChallenge.StartChallenge();
    }

    private void OpenPanel()
    {
        if (isOpen || panelRoot == null)
            return;

        isOpen = true;
        previousTimeScale = Time.timeScale;

        if (rewardText != null)
        {
            rewardText.text =
                "DOUBLE OR LEAVE\n" +
                $"\u041d\u0430\u0433\u0440\u0430\u0434\u0430: {doubleOrLeave.RewardAmount} / " +
                $"\u0443\u0434\u0432\u043e\u0435\u043d\u0438\u0435: {doubleOrLeave.RewardAmount * 2}";
        }

        panelRoot.SetActive(true);
        Time.timeScale = 0f;
    }

    private void ClosePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (!isOpen)
            return;

        isOpen = false;
        Time.timeScale = previousTimeScale;
    }

    private void ShowChallengeResultIfChanged()
    {
        if (doubleOrLeave == null || doubleOrLeave.State == observedState)
            return;

        DoubleOrLeaveState previousState = observedState;
        observedState = doubleOrLeave.State;

        if (previousState != DoubleOrLeaveState.WaitingForChallenge)
            return;

        if (observedState == DoubleOrLeaveState.RewardGranted)
        {
            RunMessageService.Instance?.ShowCustom(
                "\u041d\u0410\u0413\u0420\u0410\u0414\u0410 \u0423\u0414\u0412\u041e\u0415\u041d\u0410",
                doubleOrLeave.LastGrantedRewardAmount.ToString()
            );
        }
        else if (observedState == DoubleOrLeaveState.Failed)
        {
            RunMessageService.Instance?.ShowCustom(
                "\u041d\u0410\u0413\u0420\u0410\u0414\u0410 \u041f\u041e\u0422\u0415\u0420\u042f\u041d\u0410",
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
