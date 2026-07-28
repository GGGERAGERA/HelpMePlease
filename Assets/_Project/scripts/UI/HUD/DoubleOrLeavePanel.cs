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
            doubleOrLeave.HasPendingChoice &&
            (UpgradeManager.Instance == null ||
             !UpgradeManager.Instance.IsChoosingUpgrade))
        {
            OpenPanel();
        }
    }

    public void TakeReward()
    {
        if (!isOpen || doubleOrLeave == null)
            return;

        ClosePanel();
        doubleOrLeave.TakeReward();
        observedState = doubleOrLeave.State;
    }

    public void DoubleReward()
    {
        if (!isOpen || doubleOrLeave == null)
            return;

        ClosePanel();
        doubleOrLeave.RiskReward();
        observedState = doubleOrLeave.State;
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
                "\u0417\u0410\u0411\u0420\u0410\u0422\u042c\n" +
                "\u041f\u043e\u043b\u0443\u0447\u0438\u0442\u044c \u043f\u0440\u0435\u0434\u043c\u0435\u0442\u043d\u0443\u044e \u043d\u0430\u0433\u0440\u0430\u0434\u0443 \u0441\u0435\u0439\u0447\u0430\u0441.\n\n" +
                "\u0420\u0418\u0421\u041a\u041d\u0423\u0422\u042c\n" +
                "\u0421\u043b\u0435\u0434\u0443\u044e\u0449\u0435\u0435 \u0438\u0441\u043f\u044b\u0442\u0430\u043d\u0438\u0435 \u0441\u0442\u0430\u043d\u0435\u0442 \u0441\u043b\u043e\u0436\u043d\u0435\u0435. " +
                "\u041f\u043e\u0431\u0435\u0434\u0430 \u0434\u0430\u0441\u0442 \u0443\u043b\u0443\u0447\u0448\u0435\u043d\u043d\u0443\u044e \u043d\u0430\u0433\u0440\u0430\u0434\u0443. " +
                "\u041f\u043e\u0440\u0430\u0436\u0435\u043d\u0438\u0435 \u0443\u043d\u0438\u0447\u0442\u043e\u0436\u0438\u0442 \u043d\u0430\u0433\u0440\u0430\u0434\u0443.";
        }

        SetButtonText(takeRewardButton, "\u0417\u0410\u0411\u0420\u0410\u0422\u042c");
        SetButtonText(doubleRewardButton, "\u0420\u0418\u0421\u041a\u041d\u0423\u0422\u042c");

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

        if (observedState == DoubleOrLeaveState.Failed)
        {
            RunMessageService.Instance?.ShowCustom(
                "\u041d\u0410\u0413\u0420\u0410\u0414\u0410 \u041f\u041e\u0422\u0415\u0420\u042f\u041d\u0410",
                string.Empty
            );
        }
    }

    private static void SetButtonText(Button button, string value)
    {
        TextMeshProUGUI text = button != null
            ? button.GetComponentInChildren<TextMeshProUGUI>(true)
            : null;

        if (text != null)
            text.text = value;
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
