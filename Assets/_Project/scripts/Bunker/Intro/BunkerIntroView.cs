using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerIntroView : MonoBehaviour
{
    private const string PixelFontResource =
        "Fonts & Materials/PressStart2P-vaV7 SDF";
    private const string ArchiveDamageText = "АРХИВ ПОВРЕЖДЁН";

    [Header("Root")]
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private Image blackOverlay;
    [SerializeField] private Image emergencyFlash;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI mainText;
    [SerializeField] private TextMeshProUGUI systemText;
    [SerializeField] private TextMeshProUGUI skipHint;

    private TMP_FontAsset pixelFont;
    private Image cursorBlock;
    private RectTransform cursorRect;
    private string cachedMainText;
    private string cachedSecondaryText;
    private BunkerIntroTextStyle cachedStyle;
    private bool hasCachedText;

    [Header("Colors")]
    [SerializeField] private Color recordingColor =
        new(0.78f, 0.82f, 0.84f, 1f);
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
        ConfigureTerminalLayout();
        rootGroup.alpha = 1f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = true;
        blackOverlay.color = Color.black;
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
        float textAlpha,
        bool showCursor = false,
        bool cursorOnMain = false)
    {
        main ??= string.Empty;
        secondary ??= string.Empty;

        if (!hasCachedText ||
            !string.Equals(cachedMainText, main, System.StringComparison.Ordinal) ||
            !string.Equals(cachedSecondaryText, secondary, System.StringComparison.Ordinal) ||
            cachedStyle != style)
        {
            SetFullText(main, secondary, style);
        }

        bool damagedRecording = style == BunkerIntroTextStyle.HumanRecording;
        Color mainColor = style == BunkerIntroTextStyle.Error
            ? errorColor
            : damagedRecording ? recordingColor : systemColor;
        Color secondaryColor = damagedRecording ? recordingColor : mainColor;

        mainText.color = WithAlpha(mainColor, textAlpha);
        systemText.color = WithAlpha(
            secondaryColor,
            textAlpha * (damagedRecording ? 0.48f : 0.88f));
        mainText.maxVisibleCharacters = Mathf.Clamp(
            visibleMainCharacters,
            0,
            main.Length);
        systemText.maxVisibleCharacters = Mathf.Clamp(
            visibleSecondaryCharacters,
            0,
            secondary.Length);
        UpdateCursor(
            showCursor,
            cursorOnMain,
            visibleMainCharacters,
            visibleSecondaryCharacters);
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
        hasCachedText = false;
        cachedMainText = null;
        cachedSecondaryText = null;

        if (cursorBlock != null)
            cursorBlock.gameObject.SetActive(false);

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

    private void ConfigureTerminalLayout()
    {
        if (pixelFont == null)
            pixelFont = Resources.Load<TMP_FontAsset>(PixelFontResource);

        if (pixelFont != null)
        {
            mainText.font = pixelFont;
            systemText.font = pixelFont;
        }

        RectTransform title = mainText.rectTransform;
        title.anchorMin = new Vector2(0.11f, 0.57f);
        title.anchorMax = new Vector2(0.89f, 0.82f);
        title.offsetMin = Vector2.zero;
        title.offsetMax = Vector2.zero;
        mainText.fontSize = 32f;
        mainText.fontStyle = FontStyles.Normal;
        mainText.characterSpacing = 1f;
        mainText.enableVertexGradient = false;
        mainText.alignment = TextAlignmentOptions.Center;
        mainText.textWrappingMode = TextWrappingModes.Normal;

        RectTransform body = systemText.rectTransform;
        body.anchorMin = new Vector2(0.12f, 0.23f);
        body.anchorMax = new Vector2(0.88f, 0.61f);
        body.offsetMin = Vector2.zero;
        body.offsetMax = Vector2.zero;
        systemText.fontSize = 22f;
        systemText.fontStyle = FontStyles.Normal;
        systemText.characterSpacing = 1f;
        systemText.enableVertexGradient = false;
        systemText.alignment = TextAlignmentOptions.Center;
        systemText.textWrappingMode = TextWrappingModes.Normal;
    }

    private void SetFullText(
        string main,
        string secondary,
        BunkerIntroTextStyle style)
    {
        bool damagedRecording = style == BunkerIntroTextStyle.HumanRecording;
        mainText.fontSize = damagedRecording
            ? 28f
            : main.Contains("\n") ? 20f : 28f;
        systemText.fontSize = damagedRecording ? 14f : 20f;

        mainText.text = ApplyArchiveAccent(main);
        systemText.text = secondary;
        mainText.maxVisibleCharacters = 0;
        systemText.maxVisibleCharacters = 0;
        mainText.ForceMeshUpdate();
        systemText.ForceMeshUpdate();

        cachedMainText = main;
        cachedSecondaryText = secondary;
        cachedStyle = style;
        hasCachedText = true;
    }

    private void UpdateCursor(
        bool visible,
        bool onMain,
        int visibleMainCharacters,
        int visibleSecondaryCharacters)
    {
        if (!visible)
        {
            if (cursorBlock != null)
                cursorBlock.gameObject.SetActive(false);

            return;
        }

        EnsureCursorBlock();
        TextMeshProUGUI target = onMain ? mainText : systemText;
        int visibleCharacters = onMain
            ? visibleMainCharacters
            : visibleSecondaryCharacters;

        if (cursorRect.parent != target.rectTransform)
            cursorRect.SetParent(target.rectTransform, false);

        TMP_TextInfo textInfo = target.textInfo;
        float x = 0f;
        float baseline = 0f;

        if (textInfo.characterCount > 0)
        {
            int index = Mathf.Clamp(
                visibleCharacters <= 0 ? 0 : visibleCharacters - 1,
                0,
                textInfo.characterCount - 1);
            TMP_CharacterInfo character = textInfo.characterInfo[index];

            if (visibleCharacters <= 0)
            {
                x = character.origin;
            }
            else if (character.character == '\n' &&
                     index + 1 < textInfo.characterCount)
            {
                character = textInfo.characterInfo[index + 1];
                x = character.origin;
            }
            else
            {
                x = character.xAdvance;
            }

            baseline = character.baseLine;
        }

        float width = Mathf.Max(3f, Mathf.Round(target.fontSize * 0.2f));
        float height = Mathf.Max(4f, Mathf.Round(target.fontSize * 0.42f));
        cursorRect.sizeDelta = new Vector2(width, height);
        cursorRect.localPosition = new Vector3(
            Mathf.Round(x + width * 0.5f),
            Mathf.Round(baseline + height * 0.5f),
            0f);
        cursorBlock.color = target.color;
        cursorBlock.gameObject.SetActive(true);
    }

    private void EnsureCursorBlock()
    {
        if (cursorBlock != null)
            return;

        GameObject cursorObject = new(
            "Typewriter Cursor",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        cursorRect = cursorObject.GetComponent<RectTransform>();
        cursorBlock = cursorObject.GetComponent<Image>();
        cursorBlock.raycastTarget = false;
        cursorRect.localScale = Vector3.one;
    }

    private string ApplyArchiveAccent(string sourceText)
    {
        if (string.IsNullOrEmpty(sourceText))
            return sourceText;

        int accentStart = sourceText.IndexOf(
            ArchiveDamageText,
            System.StringComparison.Ordinal);
        if (accentStart < 0)
            return sourceText;

        string openTag = $"<color=#{ColorUtility.ToHtmlStringRGB(errorColor)}>";
        return sourceText.Insert(accentStart, openTag)
            .Insert(
                accentStart + openTag.Length + ArchiveDamageText.Length,
                "</color>");
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
