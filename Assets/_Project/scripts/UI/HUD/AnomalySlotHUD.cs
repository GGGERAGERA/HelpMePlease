using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AnomalySlotHUD : MonoBehaviour
{
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI valueText;
    private AnomalyInventory inventory;
    private RunStateManager runState;

    public static void EnsureExists()
    {
        if (FindFirstObjectByType<AnomalySlotHUD>() != null)
            return;

        GameObject root = new("Anomaly Slot HUD");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 55;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<AnomalySlotHUD>();
    }

    private void Awake()
    {
        BuildVisual();
    }

    private void OnEnable()
    {
        runState = RunStateManager.Instance != null
            ? RunStateManager.Instance
            : RunStateManager.EnsureExists();
        inventory = runState.AnomalyInventory;
        inventory.Changed += Refresh;
        runState.EvolutionChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.Changed -= Refresh;

        if (runState != null)
            runState.EvolutionChanged -= Refresh;

        inventory = null;
        runState = null;
    }

    private void BuildVisual()
    {
        RectTransform panel = CreateRect("Anomaly Panel", transform);
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.zero;
        panel.pivot = Vector2.zero;
        panel.anchoredPosition = new Vector2(18f, 82f);
        panel.sizeDelta = new Vector2(300f, 62f);

        Image background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0.035f, 0.045f, 0.06f, 0.86f);

        titleText = CreateText("Title", panel);
        ConfigureText(titleText, 13f, FontStyles.Bold, new Color(0.42f, 0.82f, 1f));
        titleText.text = "ANOMALY";
        SetRect(titleText.rectTransform, new Vector2(10f, 34f), new Vector2(280f, 20f));

        valueText = CreateText("Value", panel);
        ConfigureText(valueText, 18f, FontStyles.Bold, Color.white);
        SetRect(valueText.rectTransform, new Vector2(10f, 7f), new Vector2(280f, 25f));
    }

    private void Refresh()
    {
        if (valueText == null)
            return;

        if (inventory == null || inventory.IsEmpty)
        {
            titleText.text = "ANOMALY";
            valueText.text = "[ EMPTY ]";
            return;
        }

        if (runState != null && runState.HasEvolution)
        {
            string mode = inventory.Level >= 3
                ? "OVERDRIVE"
                : "HYBRID";
            titleText.text = "ANOMALY";
            valueText.text = $"[ {inventory.CurrentItem.DisplayName} " +
                $"{ToRoman(inventory.Level)} • {mode} ]";
            return;
        }

        titleText.text = "ANOMALY";
        valueText.text = $"[ {inventory.CurrentItem.DisplayName} " +
            $"{ToRoman(inventory.Level)} ]";
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent)
    {
        RectTransform rect = CreateRect(name, parent);
        return rect.gameObject.AddComponent<TextMeshProUGUI>();
    }

    private static void ConfigureText(
        TextMeshProUGUI text,
        float size,
        FontStyles style,
        Color color)
    {
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static string ToRoman(int level) => level switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        _ => "—"
    };
}
