using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public sealed class LevelAnomalyView : MonoBehaviour
{
    private static readonly Vector2 CardPosition = new(40f, -275f);
    private static readonly Vector2 CardSize = new(350f, 140f);

    private static readonly Color PanelColor =
        new(0.01f, 0.022f, 0.032f, 0.86f);
    private static readonly Color Cyan =
        new(0.12f, 0.78f, 0.9f, 0.95f);
    private static readonly Color DescriptionColor =
        new(0.78f, 0.84f, 0.88f, 1f);

    [SerializeField] private TMP_FontAsset font;

    private CanvasGroup rootGroup;
    private CanvasGroup cardGroup;
    private RectTransform cardRect;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI descriptionText;
    private Coroutine cardRoutine;
    private bool built;

    private void Awake()
    {
        rootGroup = GetComponent<CanvasGroup>();
        ConfigureRootGroup();
    }

    public void Prepare()
    {
        if (built)
            return;

        built = true;
        rootGroup ??= GetComponent<CanvasGroup>();
        ConfigureRootGroup();
        Stretch((RectTransform)transform);
        BuildCard();
    }

    public void ShowLocalAnomaly(
        LevelMechanicPresentationData presentation)
    {
        Prepare();
        StopCardRoutine();
        cardRoutine = StartCoroutine(ShowCardRoutine(presentation));
    }

    public void HideLocalAnomaly()
    {
        if (!built)
            return;

        StopCardRoutine();
        cardGroup.alpha = 0f;
        cardGroup.gameObject.SetActive(false);
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
    }

    private IEnumerator ShowCardRoutine(
        LevelMechanicPresentationData presentation)
    {
        SetData(presentation);
        rootGroup.alpha = 1f;
        rootGroup.blocksRaycasts = false;
        cardRect.anchoredPosition = CardPosition;
        cardGroup.gameObject.SetActive(true);
        yield return Fade(cardGroup, 0f, 1f, 0.15f);
        cardRoutine = null;
    }

    private void BuildCard()
    {
        GameObject card = CreateUiObject("ActiveAnomalyCard", transform);
        cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0f, 1f);
        cardRect.anchorMax = new Vector2(0f, 1f);
        cardRect.pivot = new Vector2(0f, 1f);
        cardRect.anchoredPosition = CardPosition;
        cardRect.sizeDelta = CardSize;

        Image background = card.AddComponent<Image>();
        background.color = PanelColor;
        background.raycastTarget = false;

        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = Cyan;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        cardGroup = card.AddComponent<CanvasGroup>();
        cardGroup.interactable = false;
        cardGroup.blocksRaycasts = false;

        TextMeshProUGUI header = CreateTextChild(
            card.transform,
            "Header",
            new Vector2(0f, 49f),
            new Vector2(310f, 20f),
            15f,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft,
            Cyan
        );
        header.text = "АНОМАЛИЯ";

        nameText = CreateTextChild(
            card.transform,
            "Name",
            new Vector2(0f, 19f),
            new Vector2(310f, 32f),
            23f,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft,
            Color.white
        );

        descriptionText = CreateTextChild(
            card.transform,
            "Description",
            new Vector2(0f, -31f),
            new Vector2(310f, 50f),
            17f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft,
            DescriptionColor
        );
        descriptionText.maxVisibleLines = 2;

        card.SetActive(false);
    }

    private void SetData(LevelMechanicPresentationData presentation)
    {
        nameText.text = GetCompactName(presentation.Title);
        descriptionText.text = string.IsNullOrWhiteSpace(
                presentation.PinnedDescription)
            ? presentation.Description
            : presentation.PinnedDescription;
    }

    private static string GetCompactName(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        int separatorIndex = title.IndexOf(':');
        return separatorIndex >= 0 && separatorIndex < title.Length - 1
            ? title.Substring(separatorIndex + 1).Trim()
            : title.Trim();
    }

    private void ConfigureRootGroup()
    {
        rootGroup.alpha = 0f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;
    }

    private void StopCardRoutine()
    {
        if (cardRoutine == null)
            return;

        StopCoroutine(cardRoutine);
        cardRoutine = null;
    }

    private TextMeshProUGUI CreateTextChild(
        Transform parent,
        string objectName,
        Vector2 position,
        Vector2 size,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        SetCenteredRect(rect, size, position);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static IEnumerator Fade(
        CanvasGroup group,
        float start,
        float target,
        float duration)
    {
        group.alpha = start;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            group.alpha = Mathf.Lerp(start, target, 1f - (1f - t) * (1f - t));
            yield return null;
        }

        group.alpha = target;
    }

    private static GameObject CreateUiObject(
        string objectName,
        Transform parent)
    {
        GameObject result = new(objectName, typeof(RectTransform));
        result.layer = 5;
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetCenteredRect(
        RectTransform rect,
        Vector2 size,
        Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void OnDisable()
    {
        HideLocalAnomaly();
    }
}
