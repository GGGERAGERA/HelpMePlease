using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The same continuous pressure display in combat and on the route map.</summary>
public sealed class RunPressureMeter : MonoBehaviour
{
    private RectTransform fill;
    private Image fillImage;
    private TMP_Text percentage;
    public float Value { get; private set; }

    public static RunPressureMeter Create(RectTransform parent, TMP_FontAsset font, Vector2 size,
        string percentageName)
    {
        var root = Rect("Pressure meter", parent);
        root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
        root.sizeDelta = size;
        var meter = root.gameObject.AddComponent<RunPressureMeter>();
        var track = Rect("Pressure track", root);
        track.anchorMin = new Vector2(0f, .5f);
        track.anchorMax = new Vector2(1f, .5f);
        track.offsetMin = new Vector2(0f, -4f);
        track.offsetMax = new Vector2(-53f, 4f);
        var background = track.gameObject.AddComponent<Image>();
        background.color = Subject42UITheme.MeterTrack;
        background.raycastTarget = false;

        meter.fill = Rect("Pressure fill", track);
        meter.fill.anchorMin = Vector2.zero;
        meter.fill.anchorMax = Vector2.one;
        meter.fill.offsetMin = meter.fill.offsetMax = Vector2.zero;
        meter.fillImage = meter.fill.gameObject.AddComponent<Image>();
        meter.fillImage.raycastTarget = false;

        var text = Rect(percentageName, root);
        text.anchorMin = new Vector2(1f, 0f);
        text.anchorMax = Vector2.one;
        text.offsetMin = new Vector2(-49f, 0f);
        text.offsetMax = Vector2.zero;
        meter.percentage = text.gameObject.AddComponent<TextMeshProUGUI>();
        meter.percentage.font = font;
        meter.percentage.fontSize = 14f;
        meter.percentage.alignment = TextAlignmentOptions.MidlineRight;
        meter.percentage.textWrappingMode = TextWrappingModes.NoWrap;
        meter.percentage.raycastTarget = false;
        meter.SetValue(0f);
        return meter;
    }

    public void SetValue(float value)
    {
        Value = Mathf.Clamp(value, 0f, 100f);
        fill.anchorMax = new Vector2(Value / 100f, 1f);
        fillImage.color = StageColor(ThreatTierPresentation.FromPressure(Value));
        percentage.color = fillImage.color;
        // Truncate instead of rounding: 24.99 must never claim the 25% stage.
        float displayed = Mathf.Floor(Value * 10f) / 10f;
        percentage.text = displayed.ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }

    public static Color StageColor(ThreatTier tier) => tier switch
    {
        ThreatTier.Tier2 => Subject42UITheme.Amber,
        ThreatTier.Tier3 => Subject42UITheme.Coral,
        ThreatTier.Tier4 => new Color32(0xEF, 0x4B, 0x5E, 255),
        _ => Subject42UITheme.Cyan
    };

    private static RectTransform Rect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }
}
