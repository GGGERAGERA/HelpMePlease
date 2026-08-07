using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerAnomalyStabilizerPanel : MonoBehaviour
{
    private sealed class CardView
    {
        public GameObject Root;
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Description;
        public Button SelectButton;
        public TextMeshProUGUI SelectLabel;
        public AnomalyStabilizerData Data;
    }

    private static readonly Color PanelColor = new(0.025f, 0.04f, 0.055f, 0.97f);
    private static readonly Color CardColor = new(0.055f, 0.08f, 0.1f, 0.98f);
    private static readonly Color Cyan = new(0.1f, 0.82f, 0.86f, 1f);

    private readonly List<AnomalyStabilizerData> available = new();
    private readonly List<AnomalyStabilizerData> choices = new();
    private readonly CardView[] cards = new CardView[3];

    private BunkerPanelManager panelManager;
    private GameObject canvasRoot;
    private TextMeshProUGUI levelText;
    private TextMeshProUGUI selectedText;

    public bool IsVisible => canvasRoot != null && canvasRoot.activeSelf;

    public void Configure(BunkerPanelManager manager)
    {
        panelManager = manager;
    }

    public void Show()
    {
        EnsureUi();
        canvasRoot.SetActive(true);
        BindEvents();
        RollChoices();
        Refresh();
    }

    public void Hide()
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        UnbindEvents();
    }

    private void OnDestroy()
    {
        UnbindEvents();
    }

    private void BindEvents()
    {
        UnbindEvents();

        if (BunkerStationProgressionService.Instance != null)
        {
            BunkerStationProgressionService.Instance.StationLevelChanged +=
                HandleStationLevelChanged;
        }
    }

    private void UnbindEvents()
    {
        if (BunkerStationProgressionService.Instance != null)
        {
            BunkerStationProgressionService.Instance.StationLevelChanged -=
                HandleStationLevelChanged;
        }
    }

    private void HandleStationLevelChanged(BunkerStationId stationId, int level)
    {
        if (stationId != BunkerStationId.Anomaly || !IsVisible)
            return;

        RollChoices();
        Refresh();
    }

    private void RollChoices()
    {
        available.Clear();
        choices.Clear();

        int stationLevel = BunkerStationProgressionService.Instance != null
            ? BunkerStationProgressionService.Instance.GetLevel(BunkerStationId.Anomaly)
            : BunkerStationProgressionService.GetStoredLevel(BunkerStationId.Anomaly);
        AnomalyStabilizerData[] all =
            Resources.LoadAll<AnomalyStabilizerData>("AnomalyStabilizers");

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].RequiredStationLevel <= stationLevel)
                available.Add(all[i]);
        }

        AnomalyStabilizerData selected = RunSelectionManager.Instance != null
            ? RunSelectionManager.Instance.SelectedAnomalyStabilizer
            : null;

        if (selected != null && selected.RequiredStationLevel > stationLevel)
        {
            RunSelectionManager.Instance?.SelectAnomalyStabilizer(null);
            selected = null;
        }

        if (selected != null && available.Remove(selected))
            choices.Add(selected);

        int choiceCount = stationLevel <= 1 ? 2 : 3;

        while (choices.Count < choiceCount && available.Count > 0)
        {
            int index = Random.Range(0, available.Count);
            choices.Add(available[index]);
            available.RemoveAt(index);
        }
    }

    private void Select(AnomalyStabilizerData data)
    {
        if (data == null || RunSelectionManager.Instance == null)
            return;

        RunSelectionManager.Instance.SelectAnomalyStabilizer(data);
        Refresh();
    }

    private void ClearSelection()
    {
        RunSelectionManager.Instance?.SelectAnomalyStabilizer(null);
        Refresh();
    }

    private void Refresh()
    {
        int stationLevel = BunkerStationProgressionService.Instance != null
            ? BunkerStationProgressionService.Instance.GetLevel(BunkerStationId.Anomaly)
            : BunkerStationProgressionService.GetStoredLevel(BunkerStationId.Anomaly);
        levelText.text = $"STATION LV.{stationLevel}";

        AnomalyStabilizerData selected = RunSelectionManager.Instance != null
            ? RunSelectionManager.Instance.SelectedAnomalyStabilizer
            : null;
        selectedText.text = selected != null
            ? $"SELECTED: {selected.DisplayName}"
            : "SELECTED: NONE";

        for (int i = 0; i < cards.Length; i++)
        {
            CardView card = cards[i];
            bool active = i < choices.Count;
            card.Root.SetActive(active);

            if (!active)
                continue;

            AnomalyStabilizerData data = choices[i];
            card.Data = data;
            card.Title.text = data.DisplayName;
            card.Description.text = data.Description;
            bool isSelected = data == selected;
            card.SelectLabel.text = isSelected ? "SELECTED" : "SELECT";
            card.SelectButton.interactable = !isSelected;
        }
    }

    private void EnsureUi()
    {
        if (canvasRoot != null)
            return;

        canvasRoot = new GameObject(
            "AnomalyStabilizerCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasRoot.transform.SetParent(transform, false);

        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 290;

        CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = CreateObject("Panel", canvasRoot.transform, typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(28f, -28f);
        panelRect.sizeDelta = new Vector2(1160f, 570f);
        panel.GetComponent<Image>().color = PanelColor;

        TextMeshProUGUI title = CreateText(
            panel.transform, "Title", new Vector2(28f, -22f),
            new Vector2(720f, 40f), 27f, Cyan);
        title.text = "СТАБИЛИЗАТОР АНОМАЛИЙ";
        levelText = CreateText(
            panel.transform, "Level", new Vector2(28f, -66f),
            new Vector2(300f, 30f), 19f, Color.white);
        selectedText = CreateText(
            panel.transform, "Selected", new Vector2(330f, -66f),
            new Vector2(650f, 30f), 17f, new Color(0.82f, 0.88f, 0.9f));

        for (int i = 0; i < cards.Length; i++)
            cards[i] = CreateCard(panel.transform, i, 28f + i * 370f);

        Button clearButton = CreateButton(
            panel.transform, "Clear", new Vector2(28f, -520f),
            new Vector2(170f, 34f), new Color(0.16f, 0.2f, 0.23f),
            out TextMeshProUGUI clearLabel);
        clearLabel.text = "CLEAR SELECTION";
        clearLabel.fontSize = 13f;
        clearButton.onClick.AddListener(ClearSelection);

        Button closeButton = CreateButton(
            panel.transform, "Close", new Vector2(1082f, -14f),
            new Vector2(50f, 40f), new Color(0.18f, 0.23f, 0.26f),
            out TextMeshProUGUI closeLabel);
        closeLabel.text = "×";
        closeButton.onClick.AddListener(() => panelManager?.CloseAll());

        canvasRoot.SetActive(false);
    }

    private CardView CreateCard(Transform parent, int index, float x)
    {
        GameObject root = CreateObject($"Card {index + 1}", parent, typeof(Image));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -112f);
        rect.sizeDelta = new Vector2(340f, 386f);
        root.GetComponent<Image>().color = CardColor;

        CardView card = new()
        {
            Root = root,
            Title = CreateText(root.transform, "Name", new Vector2(20f, -20f),
                new Vector2(300f, 62f), 21f, Cyan),
            Description = CreateText(root.transform, "Description", new Vector2(20f, -98f),
                new Vector2(300f, 196f), 17f, Color.white),
            SelectButton = CreateButton(root.transform, "Select", new Vector2(20f, -318f),
                new Vector2(300f, 48f), Cyan, out TextMeshProUGUI selectLabel),
            SelectLabel = selectLabel
        };
        card.Description.textWrappingMode = TextWrappingModes.Normal;
        card.SelectButton.onClick.AddListener(() => Select(card.Data));
        return card;
    }

    private static GameObject CreateObject(string name, Transform parent, params System.Type[] components)
    {
        GameObject result = new(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != typeof(RectTransform))
                result.AddComponent(components[i]);
        }

        return result;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent, string name, Vector2 position, Vector2 size,
        float fontSize, Color color)
    {
        GameObject go = CreateObject(name, parent, typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(
        Transform parent, string name, Vector2 position, Vector2 size,
        Color color, out TextMeshProUGUI label)
    {
        GameObject go = CreateObject(name, parent, typeof(Image), typeof(Button));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
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
        return button;
    }
}
