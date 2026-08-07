using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerStationUpgradePanel : MonoBehaviour
{
    private static readonly Color PanelColor = new(0.025f, 0.04f, 0.055f, 0.96f);
    private static readonly Color Cyan = new(0.1f, 0.82f, 0.86f, 1f);

    private GameObject canvasRoot;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI levelText;
    private TextMeshProUGUI costText;
    private TextMeshProUGUI unlocksText;
    private TextMeshProUGUI goldText;
    private Button upgradeButton;
    private TextMeshProUGUI upgradeButtonText;
    private BunkerStationId currentStationId;
    private Coroutine unlockFeedbackRoutine;

    public bool IsVisible => canvasRoot != null && canvasRoot.activeSelf;

    private void OnEnable() => BindEvents();
    private void OnDisable() => UnbindEvents();
    private void OnDestroy() => UnbindEvents();

    public void Show(BunkerStationId stationId)
    {
        EnsureUi();
        currentStationId = stationId;
        canvasRoot.SetActive(true);
        BindEvents();
        Refresh();
    }

    public void Hide()
    {
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
        unlocksText.text = isMax ? "" : unlocks.Length == 0
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

    private void EnsureUi()
    {
        if (canvasRoot != null)
            return;

        canvasRoot = new GameObject("StationProgressionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot.transform.SetParent(transform, false);
        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = CreateUiObject("Panel", canvasRoot.transform, typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = Vector2.one;
        panelRect.anchoredPosition = new Vector2(-28f, -28f);
        panelRect.sizeDelta = new Vector2(360f, 380f);
        panel.GetComponent<Image>().color = PanelColor;

        titleText = CreateText(panel.transform, "Title", new Vector2(24f, -22f), new Vector2(250f, 36f), 25f, Cyan);
        levelText = CreateText(panel.transform, "Level", new Vector2(24f, -68f), new Vector2(312f, 32f), 21f, Color.white);
        costText = CreateText(panel.transform, "Cost", new Vector2(24f, -110f), new Vector2(312f, 32f), 18f, Color.white);
        unlocksText = CreateText(panel.transform, "Unlocks", new Vector2(24f, -154f), new Vector2(312f, 94f), 17f, new Color(0.82f, 0.88f, 0.9f));
        goldText = CreateText(panel.transform, "Gold", new Vector2(24f, -258f), new Vector2(180f, 28f), 17f, Color.white);

        upgradeButton = CreateButton(panel.transform, "UpgradeButton", new Vector2(24f, -300f), new Vector2(200f, 52f), Cyan, out upgradeButtonText);
        upgradeButton.onClick.AddListener(Upgrade);

        Button closeButton = CreateButton(panel.transform, "CloseButton", new Vector2(286f, -12f), new Vector2(50f, 40f), new Color(0.18f, 0.23f, 0.26f), out TextMeshProUGUI closeText);
        closeText.text = "×";
        closeButton.onClick.AddListener(Hide);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Button addGoldButton = CreateButton(panel.transform, "DebugGold", new Vector2(232f, -300f), new Vector2(104f, 24f), new Color(0.15f, 0.18f, 0.2f), out TextMeshProUGUI addGoldText);
        addGoldText.fontSize = 11f;
        addGoldText.text = "+1000 GOLD";
        addGoldButton.onClick.AddListener(() => BunkerStationProgressionService.Instance?.DebugAddGold());
        Button resetButton = CreateButton(panel.transform, "DebugReset", new Vector2(232f, -328f), new Vector2(104f, 24f), new Color(0.15f, 0.18f, 0.2f), out TextMeshProUGUI resetText);
        resetText.fontSize = 10f;
        resetText.text = "RESET LEVELS";
        resetButton.onClick.AddListener(() => BunkerStationProgressionService.Instance?.DebugResetStationLevels());
#endif
        canvasRoot.SetActive(false);
    }

    private static GameObject CreateUiObject(string name, Transform parent, params System.Type[] components)
    {
        GameObject result = new(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        foreach (System.Type component in components)
            if (component != typeof(RectTransform))
                result.AddComponent(component);
        return result;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        GameObject go = CreateUiObject(name, parent, typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }

    private static Button CreateButton(Transform parent, string name, Vector2 position, Vector2 size, Color color, out TextMeshProUGUI label)
    {
        GameObject go = CreateUiObject(name, parent, typeof(Image), typeof(Button));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        label = CreateText(go.transform, "Label", Vector2.zero, size, 16f, Color.white);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = Vector2.zero;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return button;
    }
}
