using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerIntroView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private Image blackOverlay;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI mainText;
    [SerializeField] private TextMeshProUGUI systemText;
    [SerializeField] private TextMeshProUGUI skipHint;

    [Header("Colors")]
    [SerializeField] private Color normalColor =
        new(0.78f, 0.95f, 1f, 1f);
    [SerializeField] private Color errorColor =
        new(1f, 0.24f, 0.2f, 1f);

    public bool IsConfigured =>
        rootGroup != null &&
        blackOverlay != null &&
        mainText != null &&
        systemText != null &&
        skipHint != null;

    public void Prepare()
    {
        gameObject.SetActive(true);
        rootGroup.alpha = 1f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = true;
        blackOverlay.color = WithAlpha(blackOverlay.color, 1f);
        ClearText();
        SetSkipHint(false, 0f);
    }

    public void SetStep(
        string main,
        string system,
        bool errorStyle,
        int visibleMainCharacters,
        int visibleSystemCharacters,
        float textAlpha,
        float overlayAlpha)
    {
        Color color = errorStyle ? errorColor : normalColor;

        mainText.text = main ?? string.Empty;
        systemText.text = system ?? string.Empty;
        mainText.color = WithAlpha(color, textAlpha);
        systemText.color = WithAlpha(color, textAlpha * 0.88f);
        mainText.maxVisibleCharacters = visibleMainCharacters;
        systemText.maxVisibleCharacters = visibleSystemCharacters;
        blackOverlay.color = WithAlpha(blackOverlay.color, overlayAlpha);
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

    public void HideImmediate()
    {
        SetTextOffset(0f);
        ClearText();
        SetSkipHint(false, 0f);

        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }

        gameObject.SetActive(false);
    }

    private void ClearText()
    {
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

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
