using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerStationUpgradePanel : MonoBehaviour
{
    private static readonly Color Cyan = new(0.1f, 0.82f, 0.86f, 1f);

    [Header("Prefab View")]
    [SerializeField] private GameObject canvasRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI unlocksText;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TextMeshProUGUI upgradeButtonText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button debugAddGoldButton;
    [SerializeField] private Button debugResetButton;
    private BunkerStationId currentStationId;
    private Coroutine unlockFeedbackRoutine;

    public bool IsVisible => canvasRoot != null && canvasRoot.activeSelf;

    private void Awake()
    {
        upgradeButton?.onClick.AddListener(Upgrade);
        closeButton?.onClick.AddListener(Hide);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugAddGoldButton != null)
        {
            debugAddGoldButton.gameObject.SetActive(true);
            debugAddGoldButton.onClick.AddListener(AddDebugGold);
        }

        if (debugResetButton != null)
        {
            debugResetButton.gameObject.SetActive(true);
            debugResetButton.onClick.AddListener(ResetDebugLevels);
        }
#else
        if (debugAddGoldButton != null)
            debugAddGoldButton.gameObject.SetActive(false);
        if (debugResetButton != null)
            debugResetButton.gameObject.SetActive(false);
#endif
    }

    private void OnDisable() => UnbindEvents();
    private void OnDestroy()
    {
        UnbindEvents();
        upgradeButton?.onClick.RemoveListener(Upgrade);
        closeButton?.onClick.RemoveListener(Hide);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        debugAddGoldButton?.onClick.RemoveListener(AddDebugGold);
        debugResetButton?.onClick.RemoveListener(ResetDebugLevels);
#endif
    }

    public void Show(BunkerStationId stationId)
    {
        if (canvasRoot == null)
        {
            Debug.LogError(
                "[BunkerStationUpgradePanel] Prefab view is not assigned.",
                this);
            return;
        }

        currentStationId = stationId;
        canvasRoot.SetActive(true);
        BindEvents();
        Refresh();
    }

    public void Hide()
    {
        UnbindEvents();

        if (unlockFeedbackRoutine != null)
        {
            StopCoroutine(unlockFeedbackRoutine);
            unlockFeedbackRoutine = null;
        }

        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    private void Upgrade()
    {
        BunkerStationProgressionService service = BunkerStationProgressionService.Instance;
        if (service == null || !service.TryGetData(currentStationId, out BunkerStationProgressionData data))
            return;

        int nextLevel = service.GetLevel(currentStationId) + 1;
        string[] unlockedContent = data.GetUnlocksForLevel(nextLevel);
        bool upgraded = service.TryUpgrade(currentStationId);
        Refresh();

        if (upgraded && unlockedContent.Length > 0)
        {
            if (unlockFeedbackRoutine != null)
                StopCoroutine(unlockFeedbackRoutine);
            unlockFeedbackRoutine = StartCoroutine(ShowUnlockFeedback(unlockedContent));
        }
    }

    private void Refresh()
    {
        BunkerStationProgressionService service = BunkerStationProgressionService.Instance;
        if (service == null || !service.TryGetData(currentStationId, out BunkerStationProgressionData data))
        {
            Hide();
            return;
        }

        int level = service.GetLevel(currentStationId);
        bool isMax = level >= data.MaxLevel;
        int cost = service.GetUpgradeCost(currentStationId);
        string[] unlocks = isMax ? System.Array.Empty<string>() : data.GetUnlocksForLevel(level + 1);

        titleText.text = data.DisplayName;
        levelText.text = $"LEVEL {level} / {data.MaxLevel}";
        costText.text = isMax ? "MAX LEVEL" : $"NEXT LEVEL: {cost} GOLD";
        string nextUnlocks = isMax ? "" : unlocks.Length == 0
            ? "NEXT: —"
            : $"NEXT: {string.Join(", ", unlocks)}";
        unlocksText.text = currentStationId == BunkerStationId.Upgrades
            ? "TIERS: LV1 CORE (6) • LV2 ADVANCED (+4) • LV3 BUILD (+2)" +
              (string.IsNullOrEmpty(nextUnlocks) ? "" : $"\n{nextUnlocks}")
            : isMax ? "" : unlocks.Length == 0
                ? "UNLOCKS:\n—"
                : $"UNLOCKS:\n{string.Join("\n", unlocks.Select(value => "• " + value))}";
        unlocksText.color = new Color(0.82f, 0.88f, 0.9f);

        int gold = CurrencyManager.Instance != null ? CurrencyManager.Instance.TotalGold : 0;
        goldText.text = $"GOLD: {gold}";
        upgradeButton.interactable = !isMax && service.CanUpgrade(currentStationId);
        upgradeButtonText.text = isMax ? "MAX LEVEL" : "UPGRADE";
    }

    private IEnumerator ShowUnlockFeedback(string[] unlockedContent)
    {
        unlocksText.color = Cyan;
        unlocksText.text = "ОТКРЫТО: " +
            string.Join(", ", unlockedContent.Select(value => value.ToUpperInvariant()));
        yield return new WaitForSecondsRealtime(1.1f);
        unlockFeedbackRoutine = null;
        Refresh();
    }

    private void BindEvents()
    {
        UnbindEvents();
        if (!IsVisible)
            return;

        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGoldUpdated += HandleGoldChanged;
        if (BunkerStationProgressionService.Instance != null)
            BunkerStationProgressionService.Instance.StationLevelChanged += HandleStationLevelChanged;
    }

    private void UnbindEvents()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGoldUpdated -= HandleGoldChanged;
        if (BunkerStationProgressionService.Instance != null)
            BunkerStationProgressionService.Instance.StationLevelChanged -= HandleStationLevelChanged;
    }

    private void HandleGoldChanged(int value) => Refresh();

    private void HandleStationLevelChanged(BunkerStationId stationId, int level)
    {
        if (stationId == currentStationId)
            Refresh();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static void AddDebugGold() =>
        BunkerStationProgressionService.Instance?.DebugAddGold();

    private static void ResetDebugLevels() =>
        BunkerStationProgressionService.Instance?.DebugResetStationLevels();
#endif
}
