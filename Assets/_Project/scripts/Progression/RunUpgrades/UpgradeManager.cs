using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(UpgradeApplier))]
public sealed class UpgradeManager : MonoBehaviour
{
    private readonly struct UpgradeChoiceRequest
    {
        public readonly int PlayerLevel;
        public readonly bool PlayLevelUpSound;
        public readonly int ChoiceCount;
        public readonly bool GuaranteeBehavior;
        public readonly bool NumericOnly;
        public readonly bool IsChestReward;
        public readonly System.Action OnClosed;

        public UpgradeChoiceRequest(
            int playerLevel,
            bool playLevelUpSound,
            int choiceCount,
            bool guaranteeBehavior,
            bool isChestReward,
            System.Action onClosed = null,
            bool numericOnly = false
        )
        {
            PlayerLevel = playerLevel;
            PlayLevelUpSound = playLevelUpSound;
            ChoiceCount = choiceCount;
            GuaranteeBehavior = guaranteeBehavior;
            NumericOnly = numericOnly;
            IsChestReward = isChestReward;
            OnClosed = onClosed;
        }
    }

    public static UpgradeManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private UpgradePanelView upgradePanelView;

    [Header("Logic")]
    [SerializeField] private UpgradeApplier upgradeApplier;
    [SerializeField] private UpgradeData[] allUpgrades;
    [SerializeField] private int choicesCount = 3;

    private readonly Queue<UpgradeChoiceRequest> pendingChoices = new();
    private UpgradeRoller upgradeRoller;
    private bool isChoosingUpgrade;
    private bool isChoosingWorldEventMode;
    private float previousTimeScale = 1f;
    private System.Action currentOnClosed;
    private System.Action worldEventStandardSelected;
    private System.Action worldEventRiskSelected;
    private bool currentRequestIsChestReward;

    public bool IsChoosingUpgrade => isChoosingUpgrade;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public IReadOnlyList<UpgradeData> AllUpgrades => allUpgrades;

    public void ConfigureDebugUpgradePool(
        UpgradeData[] upgrades,
        UpgradeApplier applier)
    {
        allUpgrades = upgrades ?? System.Array.Empty<UpgradeData>();
        upgradeApplier = applier != null ? applier : GetComponent<UpgradeApplier>();
        upgradeRoller = new UpgradeRoller(allUpgrades);
    }

    public bool TryApplyDebugUpgrade(
        UpgradeData upgrade,
        out ItemGrantResult grantResult)
    {
        return TryGrantUpgrade(upgrade, out grantResult);
    }
#endif

    public bool ShowWorldEventModeChoices(
        string eventDisplayName,
        string eventDescription,
        System.Action onStandardSelected,
        System.Action onRiskSelected
    )
    {
        if (isChoosingUpgrade || upgradePanelView == null)
            return false;

        isChoosingUpgrade = true;
        isChoosingWorldEventMode = true;
        worldEventStandardSelected = onStandardSelected;
        worldEventRiskSelected = onRiskSelected;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        upgradePanelView.ShowWorldEventModeChoices(
            eventDisplayName,
            eventDescription,
            () => CompleteWorldEventModeChoice(false),
            () => CompleteWorldEventModeChoice(true)
        );
        return true;
    }

    public void CancelWorldEventModeChoice()
    {
        if (!isChoosingWorldEventMode)
            return;

        isChoosingWorldEventMode = false;
        isChoosingUpgrade = false;
        worldEventStandardSelected = null;
        worldEventRiskSelected = null;
        upgradePanelView?.ClearWorldEventModeChoices();
        Time.timeScale = previousTimeScale;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (upgradeApplier == null)
            upgradeApplier = GetComponent<UpgradeApplier>();

        upgradeRoller = new UpgradeRoller(allUpgrades);

        if (upgradePanelView != null)
            upgradePanelView.Hide();
    }

    private void OnDisable()
    {
        CancelWorldEventModeChoice();

        if (!isChoosingUpgrade || !currentRequestIsChestReward)
            return;

        isChoosingUpgrade = false;
        currentRequestIsChestReward = false;
        upgradePanelView?.Hide();

        System.Action onClosed = currentOnClosed;
        currentOnClosed = null;
        onClosed?.Invoke();

        while (pendingChoices.Count > 0)
            pendingChoices.Dequeue().OnClosed?.Invoke();

        Time.timeScale = previousTimeScale;
    }

    public void ShowUpgradeChoices()
    {
        int playerLevel = ExperienceManager.Instance != null
            ? ExperienceManager.Instance.currentLevel
            : 1;

        RequestUpgradeChoices(
            new UpgradeChoiceRequest(
                playerLevel,
                playLevelUpSound: false,
                choicesCount,
                guaranteeBehavior: false,
                isChestReward: false
            )
        );
    }

    public void ShowLevelUpChoices(int playerLevel)
    {
        RequestUpgradeChoices(
            new UpgradeChoiceRequest(
                playerLevel,
                playLevelUpSound: true,
                choicesCount,
                guaranteeBehavior: false,
                isChestReward: false
            )
        );
    }

    public void ShowChestRewardChoices(
        int choiceCount,
        bool guaranteeBehavior,
        System.Action onClosed
    )
    {
        int playerLevel = ExperienceManager.Instance != null
            ? ExperienceManager.Instance.currentLevel
            : 1;

        RequestUpgradeChoices(
            new UpgradeChoiceRequest(
                playerLevel,
                playLevelUpSound: false,
                choiceCount,
                guaranteeBehavior,
                isChestReward: true,
                onClosed
            )
        );
    }

    public void ShowNumericChestRewardChoices(
        int choiceCount,
        System.Action onClosed)
    {
        int playerLevel = ExperienceManager.Instance != null
            ? ExperienceManager.Instance.currentLevel
            : 1;

        RequestUpgradeChoices(
            new UpgradeChoiceRequest(
                playerLevel,
                playLevelUpSound: false,
                choiceCount,
                guaranteeBehavior: false,
                isChestReward: true,
                onClosed: onClosed,
                numericOnly: true
            )
        );
    }

    private void RequestUpgradeChoices(UpgradeChoiceRequest request)
    {
        if (isChoosingUpgrade)
        {
            pendingChoices.Enqueue(request);
            return;
        }

        if (!TryBuildChoices(request, out List<UpgradeData> choices))
        {
            request.OnClosed?.Invoke();
            return;
        }

        isChoosingUpgrade = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        currentOnClosed = request.OnClosed;
        currentRequestIsChestReward = request.IsChestReward;

        ShowChoiceRequest(request, choices);
    }

    private bool TryBuildChoices(
        UpgradeChoiceRequest request,
        out List<UpgradeData> choices
    )
    {
        choices = null;

        if (upgradePanelView == null)
        {
            Debug.LogError("[UpgradeManager] UpgradePanelView is not assigned.");
            return false;
        }

        if (allUpgrades == null || allUpgrades.Length == 0)
        {
            Debug.LogWarning("[UpgradeManager] allUpgrades is empty.");
            return false;
        }

        choices = request.NumericOnly
            ? upgradeRoller.RollNumericChoices(
                request.PlayerLevel,
                request.ChoiceCount
            )
            : request.GuaranteeBehavior
                ? upgradeRoller.RollRewardChoices(
                    request.PlayerLevel,
                    request.ChoiceCount
                )
                : upgradeRoller.RollChoices(
                    request.PlayerLevel,
                    request.ChoiceCount
                );

        if (choices.Count > 0)
            return true;

        if (request.IsChestReward)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"[UpgradeManager] No chest rewards available for level " +
                $"{request.PlayerLevel}."
            );
#endif
        }
        else
        {
            Debug.LogWarning(
                $"[UpgradeManager] No upgrades available for level " +
                $"{request.PlayerLevel}."
            );
        }

        return false;
    }

    private void ShowChoiceRequest(
        UpgradeChoiceRequest request,
        IReadOnlyList<UpgradeData> choices
    )
    {
        if (request.PlayLevelUpSound)
            AudioService.Instance?.Play(AudioCueId.LevelUp);

        if (request.IsChestReward)
        {
            upgradePanelView.ShowWorldEventReward(
                "НАГРАДА",
                "Выберите предмет",
                choices,
                SelectUpgrade
            );
        }
        else
        {
            upgradePanelView.Show(
                request.PlayerLevel,
                choices,
                SelectUpgrade
            );
        }
    }

    private void SelectUpgrade(UpgradeData upgrade)
    {
        if (!isChoosingUpgrade)
            return;

        bool applied = TryGrantUpgrade(upgrade, out ItemGrantResult grantResult);

        if (!applied && grantResult == ItemGrantResult.RequiresReplacement)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"[UpgradeManager] No free item slot for {upgrade.upgradeName}. " +
                "Replacement selection is not implemented yet."
            );
#endif
        }
        else if (grantResult == ItemGrantResult.Invalid)
        {
            Debug.LogWarning("[UpgradeManager] Cannot grant an invalid upgrade.");
            return;
        }
        else if (!applied &&
            (grantResult == ItemGrantResult.Added ||
             grantResult == ItemGrantResult.LeveledUp))
        {
            return;
        }

        CloseUpgradeSelection();
    }

    private bool TryGrantUpgrade(
        UpgradeData upgrade,
        out ItemGrantResult grantResult)
    {
        grantResult = ItemGrantResult.Invalid;

        if (upgradeApplier == null)
        {
            Debug.LogError("[UpgradeManager] UpgradeApplier is not assigned.");
            return false;
        }

        RunStateManager runState = RunStateManager.EnsureExists();
        grantResult = runState.ItemSlots.TryAdd(upgrade);

        if (grantResult != ItemGrantResult.Added &&
            grantResult != ItemGrantResult.LeveledUp)
        {
            return false;
        }

        if (!upgradeApplier.Apply(upgrade))
            return false;

        runState.RegisterUpgrade(upgrade);
        return true;
    }

    private void CloseUpgradeSelection()
    {
        if (upgradePanelView != null)
            upgradePanelView.Hide();

        isChoosingUpgrade = false;
        currentRequestIsChestReward = false;
        System.Action onClosed = currentOnClosed;
        currentOnClosed = null;
        onClosed?.Invoke();

        while (pendingChoices.Count > 0)
        {
            UpgradeChoiceRequest nextRequest = pendingChoices.Dequeue();

            if (!TryBuildChoices(nextRequest, out List<UpgradeData> choices))
            {
                nextRequest.OnClosed?.Invoke();
                continue;
            }

            isChoosingUpgrade = true;
            currentOnClosed = nextRequest.OnClosed;
            currentRequestIsChestReward = nextRequest.IsChestReward;
            ShowChoiceRequest(nextRequest, choices);
            return;
        }

        Time.timeScale = previousTimeScale;
    }

    private void CompleteWorldEventModeChoice(bool risk)
    {
        if (!isChoosingUpgrade || !isChoosingWorldEventMode)
            return;

        System.Action selection = risk
            ? worldEventRiskSelected
            : worldEventStandardSelected;

        isChoosingWorldEventMode = false;
        isChoosingUpgrade = false;
        worldEventStandardSelected = null;
        worldEventRiskSelected = null;
        upgradePanelView.ClearWorldEventModeChoices();
        Time.timeScale = previousTimeScale;
        selection?.Invoke();
    }
}
