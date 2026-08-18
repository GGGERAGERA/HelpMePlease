using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dynamic Character Station data presenter. All visual objects are serialized in
/// CharacterStationPanel.prefab; this component never constructs UI hierarchy.
/// </summary>
public sealed class CharacterStationEmbeddedView : MonoBehaviour
{
    private const int SegmentCount = 10;
    private const float InvestmentRate = 180f;

    [Header("Prefab References")]
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private GameObject progressRoot;
    [SerializeField] private Image[] progressSegments = new Image[SegmentCount];
    [SerializeField] private Image[] progressFills = new Image[SegmentCount];
    [SerializeField] private TextMeshProUGUI goldProgressText;
    [SerializeField] private TextMeshProUGUI availableGoldText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TextMeshProUGUI upgradeButtonText;

    private BunkerStationProgressionService boundService;
    private CurrencyManager boundCurrency;
    private HoldInvestmentInput holdInput;
    private bool isInvesting;
    private bool investedDuringCurrentPress;
    private float investmentAccumulator;

    private void Awake()
    {
        BindInput();
    }

    private void OnEnable()
    {
        BindInput();
        BindEvents();
        Refresh();
    }

    private void OnDisable()
    {
        StopInvesting();
        UnbindEvents();
    }

    private void OnDestroy()
    {
        StopInvesting();
        UnbindEvents();
        if (holdInput != null)
        {
            holdInput.PointerDown = null;
            holdInput.PointerUp = null;
        }
    }

    private void Update()
    {
        BindEvents();
        if (!isInvesting)
            return;

        if (Time.timeScale <= 0f)
        {
            StopInvesting();
            Refresh();
            return;
        }

        BunkerStationProgressionService service = BunkerStationProgressionService.Instance;
        if (service == null || !service.CanInvest(BunkerStationId.Character))
        {
            StopInvesting();
            Refresh();
            return;
        }

        investmentAccumulator += Time.deltaTime * InvestmentRate;
        int amount = Mathf.FloorToInt(investmentAccumulator);
        if (amount <= 0)
            return;

        investmentAccumulator -= amount;
        Invest(amount);
    }

    public void Refresh()
    {
        if (panelRect == null)
            return;

        BunkerStationProgressionService service = BunkerStationProgressionService.Instance;
        if (service == null ||
            !service.TryGetData(BunkerStationId.Character, out BunkerStationProgressionData data))
        {
            panelRect.gameObject.SetActive(false);
            return;
        }

        panelRect.gameObject.SetActive(true);
        int level = service.GetLevel(BunkerStationId.Character);
        bool isMax = level >= data.MaxLevel;
        int cost = service.GetUpgradeCost(BunkerStationId.Character);
        int invested = service.GetInvestedGold(BunkerStationId.Character);
        int gold = CurrencyManager.Instance != null ? CurrencyManager.Instance.TotalGold : 0;

        if (titleText != null)
            titleText.text = "СТАНЦИЯ ПЕРСОНАЖЕЙ";
        if (levelText != null)
            levelText.text = $"УРОВЕНЬ СТАНЦИИ {level} / {data.MaxLevel}";
        if (availableGoldText != null)
        {
            availableGoldText.gameObject.SetActive(!isMax);
            availableGoldText.text = $"GOLD: {gold}";
        }
        if (goldProgressText != null)
        {
            goldProgressText.text = isMax ? "МАКСИМАЛЬНЫЙ УРОВЕНЬ" : $"{invested} / {cost}";
            LayoutElement labelLayout = goldProgressText.GetComponent<LayoutElement>();
            if (labelLayout != null)
                labelLayout.preferredWidth = isMax ? 230f : 100f;
        }

        float fill = isMax
            ? 1f
            : cost > 0
                ? Mathf.Clamp01((float)invested / cost)
                : 0f;
        RefreshProgress(fill);

        bool canUpgrade = !isMax && service.CanInvest(BunkerStationId.Character);
        if (upgradeButton != null)
        {
            upgradeButton.gameObject.SetActive(!isMax);
            upgradeButton.interactable = canUpgrade;
        }

        if (upgradeButtonText != null)
        {
            int remaining = Mathf.Max(0, cost - invested);
            upgradeButtonText.text = isInvesting
                ? "УЛУЧШЕНИЕ..."
                : $"УЛУЧШИТЬ СТАНЦИЮ — {remaining}";
        }
    }

    private void RefreshProgress(float fill)
    {
        if (progressRoot != null)
            progressRoot.SetActive(true);

        int count = Mathf.Min(progressSegments.Length, progressFills.Length);
        for (int i = 0; i < count; i++)
        {
            if (progressSegments[i] != null)
                progressSegments[i].color = StationPixelVisuals.Window;

            Image segmentFill = progressFills[i];
            if (segmentFill == null)
                continue;

            float amount = Mathf.Clamp01(fill * count - i);
            segmentFill.gameObject.SetActive(amount > 0f);
            segmentFill.rectTransform.anchorMax = new Vector2(amount, 1f);
        }
    }

    private void BindInput()
    {
        if (upgradeButton == null)
            return;

        holdInput = upgradeButton.GetComponent<HoldInvestmentInput>();
        if (holdInput == null)
        {
            Debug.LogError("[CharacterStationEmbeddedView] UpgradeStationButton is missing HoldInvestmentInput.");
            return;
        }

        holdInput.PointerDown = BeginInvesting;
        holdInput.PointerUp = EndInvesting;
    }

    private void BeginInvesting()
    {
        if (BunkerStationProgressionService.Instance == null ||
            !BunkerStationProgressionService.Instance.CanInvest(BunkerStationId.Character))
            return;

        isInvesting = true;
        investedDuringCurrentPress = false;
        investmentAccumulator = 0f;
        if (upgradeButtonText != null)
            upgradeButtonText.text = "УЛУЧШЕНИЕ...";
    }

    private void EndInvesting()
    {
        if (isInvesting && !investedDuringCurrentPress)
            Invest(1);
        StopInvesting();
        Refresh();
    }

    private void StopInvesting()
    {
        isInvesting = false;
        investmentAccumulator = 0f;
    }

    private void Invest(int requestedAmount)
    {
        BunkerStationProgressionService service = BunkerStationProgressionService.Instance;
        if (service == null)
            return;

        int oldLevel = service.GetLevel(BunkerStationId.Character);
        if (!service.TryInvestGold(BunkerStationId.Character, requestedAmount, out int actual) || actual <= 0)
        {
            StopInvesting();
            Refresh();
            return;
        }

        investedDuringCurrentPress = true;
        if (service.GetLevel(BunkerStationId.Character) != oldLevel)
            StopInvesting();
        Refresh();
    }

    private void BindEvents()
    {
        if (boundCurrency != CurrencyManager.Instance)
        {
            if (boundCurrency != null)
                boundCurrency.OnGoldUpdated -= HandleGoldChanged;
            boundCurrency = CurrencyManager.Instance;
            if (boundCurrency != null)
                boundCurrency.OnGoldUpdated += HandleGoldChanged;
        }

        if (boundService == BunkerStationProgressionService.Instance)
            return;

        if (boundService != null)
        {
            boundService.StationLevelChanged -= HandleStationLevelChanged;
            boundService.StationInvestmentChanged -= HandleInvestmentChanged;
        }

        boundService = BunkerStationProgressionService.Instance;
        if (boundService != null)
        {
            boundService.StationLevelChanged += HandleStationLevelChanged;
            boundService.StationInvestmentChanged += HandleInvestmentChanged;
        }
    }

    private void UnbindEvents()
    {
        if (boundCurrency != null)
            boundCurrency.OnGoldUpdated -= HandleGoldChanged;
        if (boundService != null)
        {
            boundService.StationLevelChanged -= HandleStationLevelChanged;
            boundService.StationInvestmentChanged -= HandleInvestmentChanged;
        }
        boundCurrency = null;
        boundService = null;
    }

    private void HandleGoldChanged(int value) => Refresh();

    private void HandleInvestmentChanged(BunkerStationId stationId, int invested)
    {
        if (stationId == BunkerStationId.Character)
            Refresh();
    }

    private void HandleStationLevelChanged(BunkerStationId stationId, int level)
    {
        if (stationId == BunkerStationId.Character)
            Refresh();
    }
}
