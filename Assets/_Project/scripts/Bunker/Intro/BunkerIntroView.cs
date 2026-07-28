using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerIntroView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private Image blackOverlay;
    [SerializeField] private Image emergencyFlash;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI mainText;
    [SerializeField] private TextMeshProUGUI systemText;
    [SerializeField] private TextMeshProUGUI skipHint;

    [Header("Colors")]
    [SerializeField] private Color recordingColor =
        new(1f, 0.94f, 0.86f, 1f);
    [SerializeField] private Color systemColor =
        new(0.72f, 0.94f, 1f, 1f);
    [SerializeField] private Color errorColor =
        new(1f, 0.22f, 0.18f, 1f);

    public bool IsConfigured =>
        rootGroup != null &&
        blackOverlay != null &&
        emergencyFlash != null &&
        mainText != null &&
        systemText != null &&
        skipHint != null;

    public void Prepare()
    {
        gameObject.SetActive(true);
        rootGroup.alpha = 1f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = true;
        SetOverlayAlpha(1f);
        SetFlash(Color.red, 0f);
        ClearText();
        SetSkipHint(false, 0f);
    }

    public void SetText(
        string main,
        string secondary,
        BunkerIntroTextStyle style,
        int visibleMainCharacters,
        int visibleSecondaryCharacters,
        float textAlpha)
    {
        Color mainColor = GetColor(style);
        Color secondaryColor = style == BunkerIntroTextStyle.HumanRecording
            ? systemColor
            : mainColor;

        mainText.text = main ?? string.Empty;
        systemText.text = secondary ?? string.Empty;
        mainText.color = WithAlpha(mainColor, textAlpha);
        systemText.color = WithAlpha(
            secondaryColor,
            textAlpha * 0.72f);
        mainText.maxVisibleCharacters = visibleMainCharacters;
        systemText.maxVisibleCharacters = visibleSecondaryCharacters;
    }

    public void SetOverlayAlpha(float alpha)
    {
        blackOverlay.color = WithAlpha(
            blackOverlay.color,
            Mathf.Clamp01(alpha));
    }

    public void SetFlash(Color color, float alpha)
    {
        emergencyFlash.color = WithAlpha(color, alpha);
    }

    public void SetTextOffset(float horizontalOffset)
    {
        Vector2 mainPosition = mainText.rectTransform.anchoredPosition;
        Vector2 systemPosition = systemText.rectTransform.anchoredPosition;
        mainPosition.x = horizontalOffset;
        systemPosition.x = horizontalOffset;
        mainText.rectTransform.anchoredPosition = mainPosition;
        systemText.rectTransform.anchoredPosition = systemPosition;
    }

    public void SetSkipHint(bool visible, float progress)
    {
        skipHint.gameObject.SetActive(visible);

        if (!visible)
            return;

        float alpha = Mathf.Lerp(0.42f, 1f, Mathf.Clamp01(progress));
        skipHint.alpha = alpha;
        skipHint.text = progress > 0f
            ? $"УДЕРЖИВАЙТЕ, ЧТОБЫ ПРОПУСТИТЬ  {Mathf.RoundToInt(progress * 100f)}%"
            : "УДЕРЖИВАЙТЕ, ЧТОБЫ ПРОПУСТИТЬ";
    }

    public void SetRootAlpha(float alpha)
    {
        rootGroup.alpha = Mathf.Clamp01(alpha);
    }

    public void ClearText()
    {
        SetTextOffset(0f);

        if (mainText != null)
        {
            mainText.text = string.Empty;
            mainText.maxVisibleCharacters = 0;
        }

        if (systemText != null)
        {
            systemText.text = string.Empty;
            systemText.maxVisibleCharacters = 0;
        }
    }

    public void HideImmediate()
    {
        ClearText();
        SetSkipHint(false, 0f);

        if (emergencyFlash != null)
            SetFlash(Color.red, 0f);

        if (blackOverlay != null)
            SetOverlayAlpha(0f);

        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }

        gameObject.SetActive(false);
    }

    private Color GetColor(BunkerIntroTextStyle style)
    {
        return style switch
        {
            BunkerIntroTextStyle.HumanRecording => recordingColor,
            BunkerIntroTextStyle.Error => errorColor,
            _ => systemColor
        };
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
