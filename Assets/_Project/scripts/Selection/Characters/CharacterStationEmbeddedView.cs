using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Character Station presentation that lives inside the character selection frame.
/// Progression rules remain in BunkerStationProgressionService.
/// </summary>
public sealed class CharacterStationEmbeddedView : MonoBehaviour
{
    private const int SegmentCount = 10;

    private static readonly Color Cyan = new(0.08f, 0.78f, 0.82f, 1f);
    private static readonly Color MutedCyan = new(0.23f, 0.55f, 0.58f, 1f);
    private static readonly Color EmptySegment = new(0.055f, 0.12f, 0.14f, 1f);

    private RectTransform panelRect;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI levelText;
    private GameObject progressRoot;
    private readonly Image[] progressSegments = new Image[SegmentCount];
    private TextMeshProUGUI goldProgressText;
    private TextMeshProUGUI unlockText;
    private Button upgradeButton;
    private TextMeshProUGUI upgradeButtonText;
    private TMP_FontAsset inheritedFont;
    private Coroutine feedbackRoutine;
    private bool eventsBound;

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
        UnbindEvents();
        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
        }
    }

    private void OnDestroy() => UnbindEvents();

    private void Upgrade()
    {
        BunkerStationProgressionService service = BunkerStationProgressionService.Instance;
        if (service == null || !service.TryGetData(BunkerStationId.Character, out BunkerStationProgressionData data))
            return;

        int nextLevel = service.GetLevel(BunkerStationId.Character) + 1;
        string[] unlockedContent = data.GetUnlocksForLevel(nextLevel);
        if (!service.TryUpgrade(BunkerStationId.Character))
        {
            Refresh();
            return;
        }

        Refresh();
        if (unlockedContent.Length == 0)
            return;

        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);
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
        int gold = CurrencyManager.Instance != null ? CurrencyManager.Instance.TotalGold : 0;

        titleText.text = data.DisplayName;
        levelText.text = $"LEVEL {level} / {data.MaxLevel}";
        progressRoot.SetActive(!isMax);
        goldProgressText.text = isMax ? "MAX LEVEL" : $"{gold} / {cost} GOLD";

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

        float fill = cost > 0 ? Mathf.Clamp01((float)gold / cost) : 0f;
        int filledSegments = Mathf.FloorToInt(fill * SegmentCount + 0.0001f);
        for (int i = 0; i < progressSegments.Length; i++)
            progressSegments[i].color = i < filledSegments ? Cyan : EmptySegment;

        bool canUpgrade = !isMax && service.CanUpgrade(BunkerStationId.Character);
        upgradeButton.gameObject.SetActive(!isMax);
        upgradeButton.interactable = canUpgrade;
        upgradeButtonText.text = "UPGRADE STATION";
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
        if (eventsBound)
            return;

        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGoldUpdated += HandleGoldChanged;
        if (BunkerStationProgressionService.Instance != null)
            BunkerStationProgressionService.Instance.StationLevelChanged += HandleStationLevelChanged;
        eventsBound = true;
    }

    private void UnbindEvents()
    {
        if (!eventsBound)
            return;

        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGoldUpdated -= HandleGoldChanged;
        if (BunkerStationProgressionService.Instance != null)
            BunkerStationProgressionService.Instance.StationLevelChanged -= HandleStationLevelChanged;
        eventsBound = false;
    }

    private void HandleGoldChanged(int value) => Refresh();

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
        panelRect.anchoredPosition = new Vector2(0f, -394f);
        panelRect.sizeDelta = new Vector2(-56f, 354f);

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
        }

        goldProgressText = CreateStretchText(panel.transform, "GoldProgressText", 136f, 28f, 17f, Color.white);
        unlockText = CreateStretchText(panel.transform, "NextUnlock", 181f, 70f, 17f, Color.white);
        unlockText.lineSpacing = 5f;

        upgradeButton = CreateButton(panel.transform, "UpgradeStationButton", 284f, 50f, out upgradeButtonText);
        upgradeButton.onClick.AddListener(Upgrade);
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
