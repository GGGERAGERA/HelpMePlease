using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Character Station presentation that lives inside the character selection frame.
/// Progression rules remain in BunkerStationProgressionService.
/// </summary>
public sealed class CharacterStationEmbeddedView : MonoBehaviour
{
    private const int SegmentCount = 10;
    private const float InvestmentRate = 180f;

    private static readonly Color Cyan = new(0.08f, 0.78f, 0.82f, 1f);
    private static readonly Color MutedCyan = new(0.23f, 0.55f, 0.58f, 1f);
    private static readonly Color EmptySegment = new(0.055f, 0.12f, 0.14f, 1f);

    private RectTransform panelRect;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI levelText;
    private GameObject progressRoot;
    private readonly Image[] progressSegments = new Image[SegmentCount];
    private readonly Image[] progressFills = new Image[SegmentCount];
    private TextMeshProUGUI goldProgressText;
    private TextMeshProUGUI availableGoldText;
    private TextMeshProUGUI unlockText;
    private Button upgradeButton;
    private TextMeshProUGUI upgradeButtonText;
    private TMP_FontAsset inheritedFont;
    private Coroutine feedbackRoutine;
    private BunkerStationProgressionService boundService;
    private CurrencyManager boundCurrency;
    private bool isInvesting;
    private bool investedDuringCurrentPress;
    private float investmentAccumulator;

    public void Configure(TMP_FontAsset font, RectTransform hostPanel)
    {
        inheritedFont = font != null ? font : TMP_Settings.defaultFontAsset;
        EnsureUi(hostPanel);
        Refresh();
    }

    private void OnEnable()
    {
        BindEvents();
        if (panelRect != null)
            Refresh();
    }

    private void OnDisable()
    {
        StopInvesting();
        UnbindEvents();
        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
        }
    }

    private void OnDestroy()
    {
        StopInvesting();
        UnbindEvents();
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

    private void BeginInvesting()
    {
        if (BunkerStationProgressionService.Instance == null ||
            !BunkerStationProgressionService.Instance.CanInvest(BunkerStationId.Character))
            return;

        isInvesting = true;
        investedDuringCurrentPress = false;
        investmentAccumulator = 0f;
        upgradeButtonText.text = "INVESTING...";
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
        if (service == null || !service.TryGetData(BunkerStationId.Character, out BunkerStationProgressionData data))
            return;

        int oldLevel = service.GetLevel(BunkerStationId.Character);
        string[] unlockedContent = data.GetUnlocksForLevel(oldLevel + 1);
        if (!service.TryInvestGold(BunkerStationId.Character, requestedAmount, out int actual) || actual <= 0)
        {
            StopInvesting();
            Refresh();
            return;
        }

        investedDuringCurrentPress = true;
        int newLevel = service.GetLevel(BunkerStationId.Character);
        if (newLevel != oldLevel)
            StopInvesting();
        Refresh();
        if (newLevel == oldLevel)
            return;

        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);
        if (unlockedContent.Length > 0)
            feedbackRoutine = StartCoroutine(ShowUnlockFeedback(unlockedContent));
    }

    public void Refresh()
    {
        if (panelRect == null)
            return;

        BunkerStationProgressionService service = BunkerStationProgressionService.Instance;
        if (service == null || !service.TryGetData(BunkerStationId.Character, out BunkerStationProgressionData data))
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

        titleText.text = data.DisplayName;
        levelText.text = $"LEVEL {level} / {data.MaxLevel}";
        panelRect.sizeDelta = new Vector2(-56f, isMax ? 156f : 340f);
        progressRoot.SetActive(!isMax);
        goldProgressText.text = isMax ? "MAX LEVEL" : $"{invested} / {cost} INVESTED";
        SetStretchTop(goldProgressText.rectTransform, isMax ? 102f : 136f, 28f);
        availableGoldText.gameObject.SetActive(!isMax);
        unlockText.gameObject.SetActive(!isMax);
        availableGoldText.text = $"GOLD: {gold}";

        if (isMax)
        {
            unlockText.text = string.Empty;
        }
        else
        {
            string[] unlocks = data.GetUnlocksForLevel(level + 1);
            unlockText.text = unlocks.Length == 0
                ? "NEXT UNLOCK\n-"
                : $"NEXT UNLOCK\n{string.Join("\n", unlocks)}";
        }

        float fill = cost > 0 ? Mathf.Clamp01((float)invested / cost) : 0f;
        for (int i = 0; i < progressSegments.Length; i++)
        {
            progressSegments[i].color = EmptySegment;
            float segmentFill = Mathf.Clamp01(fill * SegmentCount - i);
            Image segmentProgress = progressFills[i];
            segmentProgress.gameObject.SetActive(segmentFill > 0f);
            RectTransform fillRect = segmentProgress.rectTransform;
            fillRect.anchorMax = new Vector2(segmentFill, 1f);
        }

        bool canUpgrade = !isMax && service.CanInvest(BunkerStationId.Character);
        upgradeButton.gameObject.SetActive(!isMax);
        upgradeButton.interactable = canUpgrade;
        upgradeButtonText.text = isInvesting ? "INVESTING..." : "INVEST GOLD";
    }

    private IEnumerator ShowUnlockFeedback(string[] unlockedContent)
    {
        unlockText.color = Cyan;
        unlockText.text = "UNLOCKED: " +
            string.Join(", ", unlockedContent.Select(value => value.ToUpperInvariant()));
        yield return new WaitForSecondsRealtime(1.1f);
        unlockText.color = Color.white;
        feedbackRoutine = null;
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
            {
                boundCurrency.OnGoldUpdated += HandleGoldChanged;
                Refresh();
            }
        }

        if (boundService != BunkerStationProgressionService.Instance)
        {
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
                Refresh();
            }
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

    private void EnsureUi(RectTransform hostPanel)
    {
        if (panelRect != null)
        {
            if (hostPanel != null && panelRect.parent != hostPanel)
                panelRect.SetParent(hostPanel, false);
            return;
        }

        Transform parent = hostPanel != null ? hostPanel : transform;
        GameObject panel = CreateUiObject("CharacterStationSection", parent);
        panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -248f);
        panelRect.sizeDelta = new Vector2(-56f, 340f);

        GameObject divider = CreateUiObject("InfoStationDivider", panel.transform, typeof(Image));
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        SetStretchTop(dividerRect, 0f, 1f);
        Image dividerImage = divider.GetComponent<Image>();
        dividerImage.color = MutedCyan;
        dividerImage.raycastTarget = false;

        titleText = CreateStretchText(panel.transform, "StationTitle", 22f, 34f, 22f, Cyan);
        titleText.fontStyle = FontStyles.Bold;
        levelText = CreateStretchText(panel.transform, "StationLevel", 62f, 28f, 18f, Color.white);

        progressRoot = CreateUiObject("GoldProgress", panel.transform);
        RectTransform progressRect = progressRoot.GetComponent<RectTransform>();
        SetStretchTop(progressRect, 102f, 24f);

        for (int i = 0; i < SegmentCount; i++)
        {
            GameObject segment = CreateUiObject($"Segment_{i + 1:00}", progressRoot.transform, typeof(Image));
            RectTransform segmentRect = segment.GetComponent<RectTransform>();
            float left = (float)i / SegmentCount;
            float right = (float)(i + 1) / SegmentCount;
            segmentRect.anchorMin = new Vector2(left, 0f);
            segmentRect.anchorMax = new Vector2(right, 1f);
            segmentRect.offsetMin = new Vector2(2f, 1f);
            segmentRect.offsetMax = new Vector2(-2f, -1f);
            progressSegments[i] = segment.GetComponent<Image>();
            progressSegments[i].raycastTarget = false;
            progressSegments[i].color = EmptySegment;

            GameObject fill = CreateUiObject("Fill", segment.transform, typeof(Image));
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            progressFills[i] = fill.GetComponent<Image>();
            progressFills[i].color = Cyan;
            progressFills[i].raycastTarget = false;
        }

        goldProgressText = CreateStretchText(panel.transform, "GoldProgressText", 136f, 28f, 17f, Color.white);
        availableGoldText = CreateStretchText(panel.transform, "AvailableGoldText", 166f, 28f, 17f, Color.white);
        unlockText = CreateStretchText(panel.transform, "NextUnlock", 201f, 60f, 17f, Color.white);
        unlockText.lineSpacing = 5f;

        upgradeButton = CreateButton(panel.transform, "UpgradeStationButton", 284f, 50f, out upgradeButtonText);
        HoldInvestmentInput holdInput = upgradeButton.gameObject.AddComponent<HoldInvestmentInput>();
        holdInput.PointerDown = BeginInvesting;
        holdInput.PointerUp = EndInvesting;
    }

    private Button CreateButton(Transform parent, string name, float top, float height, out TextMeshProUGUI label)
    {
        GameObject go = CreateUiObject(name, parent, typeof(Image), typeof(Outline), typeof(Button));
        RectTransform rect = go.GetComponent<RectTransform>();
        SetStretchTop(rect, top, height);

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.035f, 0.18f, 0.2f, 1f);
        Outline outline = go.GetComponent<Outline>();
        outline.effectColor = Cyan;
        outline.effectDistance = new Vector2(1f, -1f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.disabledColor = new Color(0.35f, 0.4f, 0.42f, 0.6f);
        button.colors = colors;

        label = CreateStretchText(go.transform, "Label", 0f, height, 16f, Color.white);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = Vector2.zero;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        return button;
    }

    private TextMeshProUGUI CreateStretchText(
        Transform parent,
        string name,
        float top,
        float height,
        float fontSize,
        Color color)
    {
        GameObject go = CreateUiObject(name, parent, typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        SetStretchTop(rect, top, height);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = inheritedFont != null ? inheritedFont : TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static void SetStretchTop(RectTransform rect, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(0f, height);
    }

    private static GameObject CreateUiObject(string name, Transform parent, params System.Type[] components)
    {
        GameObject result = new(name, typeof(RectTransform));
        result.layer = parent.gameObject.layer;
        result.transform.SetParent(parent, false);
        foreach (System.Type component in components)
        {
            if (component != typeof(RectTransform))
                result.AddComponent(component);
        }

        return result;
    }
}

internal sealed class HoldInvestmentInput : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public System.Action PointerDown { private get; set; }
    public System.Action PointerUp { private get; set; }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            PointerDown?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            PointerUp?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (eventData.pointerPress == gameObject)
            PointerUp?.Invoke();
    }
}
