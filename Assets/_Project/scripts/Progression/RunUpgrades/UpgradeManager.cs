#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Random = BotRunSeed.RewardRandom;
#endif
using System.Collections.Generic;
using UnityEngine;
using Subject42.Combat.OrbitalStation;

[RequireComponent(typeof(UpgradeApplier))]
public sealed class UpgradeManager : MonoBehaviour
{
    private sealed class UpgradeChoiceRequest
    {
        public bool RingOfferEvaluated;
        public bool OfferNewRing;
        public readonly int PlayerLevel;
        public readonly bool IsLevelUp;
        public readonly int ChoiceCount;
        public readonly bool GuaranteeBehavior;
        public readonly bool NumericOnly;
        public readonly bool IsChestReward;
        public readonly System.Action OnClosed;

        public UpgradeChoiceRequest(
            int playerLevel,
            bool isLevelUp,
            int choiceCount,
            bool guaranteeBehavior,
            bool isChestReward,
            System.Action onClosed = null,
            bool numericOnly = false
        )
        {
            PlayerLevel = playerLevel;
            IsLevelUp = isLevelUp;
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
    private readonly Queue<System.Action> idleCallbacks = new();
    private OrbitalRewardProvider orbitalRewardProvider;
    private OrbitalRewardFlowController orbitalRewardFlow;
    private OrbitalStationRuntime orbitalStation;
    private List<UpgradeData> currentChoices;
    private bool shuttingDown;
    private bool isChoosingUpgrade;
    private float previousTimeScale = 1f;
    private System.Action currentOnClosed;
    private UpgradeChoiceRequest currentRequest;
    private bool hasCurrentRequest;

    public float TimeScaleAfterRewards => previousTimeScale;

    public void BindOrbitalStation(OrbitalStationRuntime station) => orbitalStation = station;

    public bool IsRewardQueueIdle => !isChoosingUpgrade && !hasCurrentRequest &&
        pendingChoices.Count == 0;

    public bool IsChoosingUpgrade => isChoosingUpgrade;

    public void RunWhenRewardQueueIsIdle(System.Action callback)
    {
        if (callback == null)
            return;
        if (isChoosingUpgrade || pendingChoices.Count > 0)
        {
            idleCallbacks.Enqueue(callback);
            return;
        }
        callback();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public event System.Action<UpgradeData> DebugRewardCommitted;
    public object DebugCurrentRewardRequest => hasCurrentRequest ? currentRequest : null;
    public void DebugSetRewardResumeScale(float value) => previousTimeScale = value;
    public IReadOnlyList<UpgradeData> AllUpgrades => allUpgrades;

    public void ConfigureDebugUpgradePool(
        UpgradeData[] upgrades,
        UpgradeApplier applier)
    {
        allUpgrades = upgrades ?? System.Array.Empty<UpgradeData>();
        upgradeApplier = applier != null ? applier : GetComponent<UpgradeApplier>();
        orbitalRewardProvider?.Dispose();
        orbitalRewardProvider = new OrbitalRewardProvider(allUpgrades);
    }

    public bool TryApplyDebugUpgrade(
        UpgradeData upgrade,
        out ItemGrantResult grantResult)
    {
        return TryGrantUpgrade(upgrade, out grantResult);
    }

    public int GetEligibleProductionUpgradeCount(int playerLevel)
    {
        return orbitalRewardProvider?.GetEligibleKinds().Count ?? 0;
    }

    public int GetStationAvailableProductionUpgradeCount()
    {
        if (allUpgrades == null)
            return 0;

        int count = 0;
        for (int i = 0; i < allUpgrades.Length; i++)
        {
            UpgradeData upgrade = allUpgrades[i];
            if (upgrade != null &&
                UnlockProgressService.IsUnlockedNow(upgrade.unlockData))
            {
                count++;
            }
        }

        return count;
    }

    public string GetOrbitalEligibilitySummary() =>
        orbitalRewardProvider?.GetEligibilitySummary() ?? "provider unavailable";

    public bool DebugForceOrbitalReward(OrbitalRewardKind kind)
    {
        if (shuttingDown || isChoosingUpgrade || orbitalRewardProvider == null ||
            !orbitalRewardProvider.IsEligible(kind))
            return false;
        OrbitalRewardData reward = orbitalRewardProvider.GetDefinition(kind);
        if (reward == null)
            return false;
        int level = ExperienceManager.Instance?.CurrentLevel ?? 1;
        UpgradeChoiceRequest request = new(level, false, 1, false, false);
        BeginChoiceRequest(request, new List<UpgradeData> { reward });
        return true;
    }

    public IReadOnlyList<UpgradeData> DebugCurrentChoices => currentChoices;

    public bool DebugSelectCurrentChoice(int index)
    {
        if (!isChoosingUpgrade || currentChoices == null || index < 0 ||
            index >= currentChoices.Count)
            return false;
        SelectUpgrade(currentChoices[index]);
        return true;
    }
#endif

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

        orbitalRewardProvider = new OrbitalRewardProvider(allUpgrades);

        if (upgradePanelView != null)
            upgradePanelView.Hide();
    }

    private void OnEnable() => shuttingDown = false;
    private void OnDisable() => CancelPendingRewards();

    // Terminal cancellation happens before run replacement/UI destruction, not
    // in whichever scene component happens to receive OnDisable first.
    public void CancelPendingRewards()
    {
        shuttingDown = true;
        if (!IsChoosingUpgrade)
            return;

        if (orbitalRewardFlow != null)
            orbitalRewardFlow.CancelForSceneTransition();
        isChoosingUpgrade = false;
        hasCurrentRequest = false;
        currentChoices = null;
        if (upgradePanelView != null)
            upgradePanelView.Hide();

        System.Action onClosed = currentOnClosed;
        currentOnClosed = null;
        onClosed?.Invoke();

        while (pendingChoices.Count > 0)
            pendingChoices.Dequeue().OnClosed?.Invoke();

        idleCallbacks.Clear();

        RestoreRewardTimeScale();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        orbitalRewardProvider?.Dispose();
    }

    public void ShowUpgradeChoices()
    {
        int playerLevel = ExperienceManager.Instance != null
            ? ExperienceManager.Instance.currentLevel
            : 1;

        RequestUpgradeChoices(
            new UpgradeChoiceRequest(
                playerLevel,
                isLevelUp: false,
                choicesCount,
                guaranteeBehavior: false,
                isChestReward: false
            )
        );
    }

    public void GrantSpecialAnomalyRing()
    {
        if (shuttingDown) return;
        // Use the existing reward barrier: do not mutate targets during a flight,
        // and finish this grant before allowing a queued sector transition.
        RunWhenRewardQueueIsIdle(() =>
        {
            OrbitalStationRuntime station = orbitalStation;
            if (station != null && station.IsInitialized && station.AddRing() != null)
                RunMessageService.Instance?.ShowCustom("УСИЛЕННАЯ АНОМАЛИЯ",
                    "НОВАЯ ОРБИТА · +1 КОЛЬЦО", 2f);
            else
                ShowUpgradeChoices();
        });
    }

    public void ShowLevelUpChoices(int playerLevel)
    {
        FindFirstObjectByType<OrbitalInteractionController>()?.PrepareForExternalPause();
        UpgradeChoiceRequest request = new(
                playerLevel,
                isLevelUp: true,
                choicesCount,
                guaranteeBehavior: false,
                isChestReward: false
            );
        RequestUpgradeChoices(request);
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
                isLevelUp: false,
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
                isLevelUp: false,
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
        if (shuttingDown)
        {
            request.OnClosed?.Invoke();
            return;
        }
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

        BeginChoiceRequest(request, choices);
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

        if (request.IsLevelUp && !request.RingOfferEvaluated)
        {
            request.RingOfferEvaluated = true;
            request.OfferNewRing = RunStateManager.Instance.OrbitalStationState
                .BeginLevelUpOpportunity(request.PlayerLevel, Random.value);
        }
        choices = orbitalRewardProvider?.BuildChoices(request.ChoiceCount, request.OfferNewRing) ??
            new List<UpgradeData>();

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
        if (request.IsLevelUp)
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

        if (upgrade is OrbitalRewardData orbitalReward)
        {
            SelectOrbitalReward(orbitalReward);
            return;
        }

        bool applied = TryGrantUpgrade(upgrade, out ItemGrantResult grantResult);

        if (!applied)
        {
            Debug.LogWarning(
                $"[UpgradeManager] Could not grant " +
                $"'{upgrade?.upgradeName ?? "NULL"}' ({grantResult}). " +
                "Refreshing eligible choices."
            );
            RefreshChoicesAfterGrantFailure();
            return;
        }

        CompleteGrantedReward(upgrade);
    }

    private void SelectOrbitalReward(OrbitalRewardData reward)
    {
        // Targets may have changed since the hand was shown.
        if (!orbitalRewardProvider.IsEligible(reward.RewardKind))
        {
            RefreshChoicesAfterGrantFailure();
            return;
        }

        if (reward.BodyUpgrade != null)
        {
            if (TryGrantUpgrade(reward.BodyUpgrade,
                    out ItemGrantResult bodyResult))
                CompleteGrantedReward(reward);
            else
            {
                Debug.LogWarning($"[OrbitalRewards] Subject reward failed: {bodyResult}.");
                RefreshChoicesAfterGrantFailure();
            }
            return;
        }

        OrbitalStationRuntime station = orbitalStation;
        if (station == null || station.RewardFlow == null)
        {
            RefreshChoicesAfterGrantFailure();
            return;
        }
        upgradePanelView.Hide();
        orbitalRewardFlow = station.RewardFlow;
        bool started = orbitalRewardFlow.Begin(reward,
            () => CompleteGrantedReward(reward), ReturnToCurrentChoices);
        if (!started && isChoosingUpgrade)
            RefreshChoicesAfterGrantFailure();
    }

    private void ReturnToCurrentChoices()
    {
        orbitalRewardFlow = null;
        if (shuttingDown || !isChoosingUpgrade)
            return;
        currentChoices?.RemoveAll(choice => choice is OrbitalRewardData orbital &&
            !orbitalRewardProvider.IsEligible(orbital.RewardKind));
        if (currentChoices != null && currentChoices.Count > 0)
            ShowChoiceRequest(currentRequest, currentChoices);
        else
            RefreshChoicesAfterGrantFailure();
    }

    private void RefreshChoicesAfterGrantFailure()
    {
        if (hasCurrentRequest &&
            TryBuildChoices(currentRequest, out List<UpgradeData> choices))
        {
            currentChoices = choices;
            ShowChoiceRequest(currentRequest, choices);
            return;
        }

        Debug.LogWarning(
            "[UpgradeManager] No eligible upgrades remain after grant failure. " +
            "Closing the selection without leaving the game paused."
        );
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

        if (!UpgradeEligibilityRules.IsWeaponCompatible(
                upgrade,
                WeaponUpgradeCapabilityResolver.GetCurrentCapabilities()))
        {
            grantResult = ItemGrantResult.IncompatibleWeapon;
            return false;
        }

        if (UpgradeEligibilityRules.HasExclusiveConflict(
                upgrade,
                runState.ItemSlots))
        {
            grantResult = ItemGrantResult.ExclusiveConflict;
            return false;
        }

        if (!runState.ItemSlots.CanAccept(upgrade))
        {
            grantResult = runState.ItemSlots.GetLevel(upgrade) >=
                RunItemSlots.MaxItemLevel
                ? ItemGrantResult.MaxLevel
                : ItemGrantResult.RequiresReplacement;
            return false;
        }

        int nextLevel = runState.ItemSlots.GetLevel(upgrade) + 1;
        if (!upgradeApplier.Apply(upgrade, nextLevel))
            return false;

        grantResult = runState.ItemSlots.TryAdd(upgrade);

        if (grantResult != ItemGrantResult.Added &&
            grantResult != ItemGrantResult.LeveledUp)
        {
            Debug.LogError(
                $"[UpgradeManager] Slot state changed while granting " +
                $"'{upgrade.upgradeName}' ({grantResult})."
            );
            return false;
        }

        runState.RegisterUpgrade(upgrade);
        return true;
    }

    private void CompleteGrantedReward(UpgradeData upgrade)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugRewardCommitted?.Invoke(upgrade);
#endif
        CloseUpgradeSelection();
    }

    private void CloseUpgradeSelection()
    {
        if (upgradePanelView != null)
            upgradePanelView.Hide();

        isChoosingUpgrade = false;
        orbitalRewardFlow = null;
        hasCurrentRequest = false;
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
            currentRequest = nextRequest;
            hasCurrentRequest = true;
            currentOnClosed = nextRequest.OnClosed;
            currentChoices = choices;
            ShowChoiceRequest(nextRequest, choices);
            return;
        }

        RestoreRewardTimeScale();
        InvokeIdleCallbacksIfReady();
    }

    private void RestoreRewardTimeScale()
    {
        PauseMenuUI pause = FindFirstObjectByType<PauseMenuUI>();
        if (pause == null || !pause.IsPaused) Time.timeScale = previousTimeScale;
    }

    private void BeginChoiceRequest(UpgradeChoiceRequest request,
        List<UpgradeData> choices)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PhysicalCombatFeedbackRuntime.CancelHitStopForExternalTimeControl();
#endif
        FindFirstObjectByType<OrbitalInteractionController>()?.PrepareForExternalPause();
        isChoosingUpgrade = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        currentRequest = request;
        hasCurrentRequest = true;
        currentOnClosed = request.OnClosed;
        currentChoices = choices;
        ShowChoiceRequest(request, choices);
    }

    private void InvokeIdleCallbacksIfReady()
    {
        if (isChoosingUpgrade || pendingChoices.Count > 0)
            return;

        while (idleCallbacks.Count > 0 && !isChoosingUpgrade && pendingChoices.Count == 0)
            idleCallbacks.Dequeue()?.Invoke();
    }

}
