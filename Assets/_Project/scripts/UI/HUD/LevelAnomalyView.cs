using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public sealed class LevelAnomalyView : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;

    private CanvasGroup rootGroup;
    private CanvasGroup overlayGroup;
    private Image overlayBackground;
    private Image flashImage;
    private CanvasGroup sectorGroup;
    private RectTransform sectorRect;
    private TextMeshProUGUI sectorText;
    private CanvasGroup alertGroup;
    private TextMeshProUGUI alertText;
    private CanvasGroup revealCardGroup;
    private RectTransform revealCardRect;
    private TextMeshProUGUI revealNameText;
    private TextMeshProUGUI revealDescriptionText;
    private CanvasGroup pinnedGroup;
    private TextMeshProUGUI pinnedNameText;
    private TextMeshProUGUI pinnedDescriptionText;
    private Coroutine localCardRoutine;
    private bool built;

    private static readonly Color DarkPanel =
        new(0.015f, 0.025f, 0.04f, 0.96f);
    private static readonly Color Cyan =
        new(0.12f, 0.78f, 0.9f, 1f);
    private static readonly Color DangerRed =
        new(0.92f, 0.14f, 0.18f, 1f);

    private void Awake()
    {
        rootGroup = GetComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;
    }

    public void Prepare()
    {
        if (built)
            return;

        built = true;
        if (rootGroup == null)
            rootGroup = GetComponent<CanvasGroup>();

        rootGroup.alpha = 0f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;

        RectTransform rootRect = (RectTransform)transform;
        Stretch(rootRect);

        BuildOverlay();
        BuildPinnedCard();
        overlayGroup.gameObject.SetActive(false);
    }

    public IEnumerator PlayIntro(
        LevelNodeData sector,
        LevelAnomalyData anomaly)
    {
        Prepare();
        StopLocalCardRoutine();
        SetData(sector, anomaly);

        rootGroup.alpha = 1f;
        rootGroup.blocksRaycasts = true;
        overlayBackground.enabled = true;
        overlayGroup.gameObject.SetActive(true);
        pinnedGroup.gameObject.SetActive(false);

        overlayGroup.alpha = 0f;
        sectorGroup.alpha = 0f;
        alertGroup.alpha = 0f;
        revealCardGroup.alpha = 0f;
        flashImage.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0f);

        sectorRect.anchoredPosition = new Vector2(-70f, 130f);
        revealCardRect.anchoredPosition = Vector2.zero;
        revealCardRect.localScale = Vector3.one * 0.7f;

        yield return Fade(overlayGroup, 0f, 1f, 0.18f);
        yield return AnimateSectorIn(0.25f);
        yield return WaitRealtime(0.22f);
        yield return Fade(sectorGroup, 1f, 0f, 0.15f);
        yield return GlitchFlash();
        yield return Fade(alertGroup, 0f, 1f, 0.15f);
        yield return ScaleAndFadeCard(0.25f);
        yield return ScaleCard(Vector3.one * 1.08f, Vector3.one, 0.08f);
        yield return WaitRealtime(0.45f);
        yield return DockCard(0.28f);

        revealCardGroup.alpha = 0f;
        pinnedGroup.gameObject.SetActive(true);
        pinnedGroup.alpha = 1f;
        yield return Fade(overlayGroup, 1f, 0f, 0.2f);

        overlayGroup.gameObject.SetActive(false);
        rootGroup.blocksRaycasts = false;
    }

    public void ShowLocalAnomaly(LevelAnomalyData anomaly)
    {
        if (anomaly == null)
            return;

        Prepare();
        StopLocalCardRoutine();
        localCardRoutine = StartCoroutine(
            ShowLocalAnomalyRoutine(anomaly)
        );
    }

    public void HideLocalAnomaly()
    {
        if (!built)
            return;

        StopLocalCardRoutine();
        revealCardGroup.alpha = 0f;
        pinnedGroup.alpha = 0f;
        pinnedGroup.gameObject.SetActive(false);
        overlayGroup.gameObject.SetActive(false);
        overlayBackground.enabled = true;
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
    }

    private IEnumerator ShowLocalAnomalyRoutine(
        LevelAnomalyData anomaly)
    {
        SetData(null, anomaly);

        rootGroup.alpha = 1f;
        rootGroup.blocksRaycasts = false;
        overlayBackground.enabled = false;
        overlayGroup.gameObject.SetActive(true);
        overlayGroup.alpha = 1f;
        sectorGroup.alpha = 0f;
        alertGroup.alpha = 0f;
        flashImage.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0f);
        pinnedGroup.alpha = 0f;
        pinnedGroup.gameObject.SetActive(false);
        revealCardGroup.alpha = 0f;
        revealCardRect.anchoredPosition = Vector2.zero;
        revealCardRect.localScale = Vector3.one * 0.7f;

        yield return ScaleAndFadeCard(0.25f);
        yield return ScaleCard(
            Vector3.one * 1.08f,
            Vector3.one,
            0.08f
        );
        yield return WaitRealtime(0.45f);
        yield return DockCard(0.28f);

        revealCardGroup.alpha = 0f;
        pinnedGroup.gameObject.SetActive(true);
        pinnedGroup.alpha = 1f;
        overlayGroup.gameObject.SetActive(false);
        overlayBackground.enabled = true;
        localCardRoutine = null;
    }

    private void StopLocalCardRoutine()
    {
        if (localCardRoutine == null)
            return;

        StopCoroutine(localCardRoutine);
        localCardRoutine = null;
    }

    private void BuildOverlay()
    {
        GameObject overlay = CreateUiObject("AnomalyIntroOverlay", transform);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        Stretch(overlayRect);
        overlayBackground = overlay.AddComponent<Image>();
        overlayBackground.color =
            new Color(0.005f, 0.008f, 0.015f, 0.9f);
        overlayBackground.raycastTarget = true;
        overlayGroup = overlay.AddComponent<CanvasGroup>();

        GameObject flash = CreateUiObject("GlitchFlash", overlay.transform);
        RectTransform flashRect = flash.GetComponent<RectTransform>();
        Stretch(flashRect);
        flashImage = flash.AddComponent<Image>();
        flashImage.raycastTarget = false;

        GameObject sector = CreateUiObject("SectorTitle", overlay.transform);
        sectorRect = sector.GetComponent<RectTransform>();
        SetCenteredRect(sectorRect, new Vector2(1280f, 150f), new Vector2(0f, 130f));
        sectorGroup = sector.AddComponent<CanvasGroup>();
        sectorText = AddText(
            sector,
            58f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Color.white
        );

        GameObject alert = CreateUiObject("AnomalyAlert", overlay.transform);
        RectTransform alertRect = alert.GetComponent<RectTransform>();
        SetCenteredRect(alertRect, new Vector2(1000f, 90f), new Vector2(0f, 210f));
        alertGroup = alert.AddComponent<CanvasGroup>();
        alertText = AddText(
            alert,
            38f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            DangerRed
        );
        alertText.text = "ОБНАРУЖЕНА АНОМАЛИЯ";

        GameObject card = CreateUiObject("AnomalyRevealCard", overlay.transform);
        revealCardRect = card.GetComponent<RectTransform>();
        SetCenteredRect(revealCardRect, new Vector2(650f, 280f), Vector2.zero);
        Image cardImage = card.AddComponent<Image>();
        cardImage.color = DarkPanel;
        cardImage.raycastTarget = false;
        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = Cyan;
        outline.effectDistance = new Vector2(2f, -2f);
        revealCardGroup = card.AddComponent<CanvasGroup>();

        GameObject accent = CreateUiObject("DangerAccent", card.transform);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(7f, 0f);
        Image accentImage = accent.AddComponent<Image>();
        accentImage.color = DangerRed;
        accentImage.raycastTarget = false;

        revealNameText = CreateTextChild(
            card.transform,
            "AnomalyName",
            new Vector2(0f, 48f),
            new Vector2(570f, 80f),
            34f,
            FontStyles.Bold,
            Color.white
        );
        revealDescriptionText = CreateTextChild(
            card.transform,
            "AnomalyDescription",
            new Vector2(0f, -45f),
            new Vector2(570f, 110f),
            24f,
            FontStyles.Normal,
            new Color(0.82f, 0.9f, 0.95f, 1f)
        );
    }

    private void BuildPinnedCard()
    {
        GameObject pinned = CreateUiObject("ActiveAnomalyCard", transform);
        RectTransform pinnedRect = pinned.GetComponent<RectTransform>();
        pinnedRect.anchorMin = new Vector2(1f, 1f);
        pinnedRect.anchorMax = new Vector2(1f, 1f);
        pinnedRect.pivot = new Vector2(1f, 1f);
        pinnedRect.anchoredPosition = new Vector2(-20f, -165f);
        pinnedRect.sizeDelta = new Vector2(390f, 155f);

        Image background = pinned.AddComponent<Image>();
        background.color = new Color(0.015f, 0.025f, 0.04f, 0.92f);
        background.raycastTarget = false;
        Outline outline = pinned.AddComponent<Outline>();
        outline.effectColor = Cyan;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        pinnedGroup = pinned.AddComponent<CanvasGroup>();
        pinnedGroup.interactable = false;
        pinnedGroup.blocksRaycasts = false;

        GameObject accent = CreateUiObject("DangerAccent", pinned.transform);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(5f, 0f);
        Image accentImage = accent.AddComponent<Image>();
        accentImage.color = DangerRed;
        accentImage.raycastTarget = false;

        TextMeshProUGUI header = CreateTextChild(
            pinned.transform,
            "Header",
            new Vector2(0f, 55f),
            new Vector2(340f, 30f),
            17f,
            FontStyles.Bold,
            Cyan
        );
        header.text = "АНОМАЛИЯ";

        pinnedNameText = CreateTextChild(
            pinned.transform,
            "Name",
            new Vector2(0f, 16f),
            new Vector2(340f, 42f),
            23f,
            FontStyles.Bold,
            Color.white
        );
        pinnedDescriptionText = CreateTextChild(
            pinned.transform,
            "Description",
            new Vector2(0f, -37f),
            new Vector2(340f, 58f),
            18f,
            FontStyles.Normal,
            new Color(0.82f, 0.9f, 0.95f, 1f)
        );

        pinned.SetActive(false);
    }

    private void SetData(LevelNodeData sector, LevelAnomalyData anomaly)
    {
        sectorText.text = sector != null
            ? sector.nodeName
            : "СЕКТОР";
        revealNameText.text = anomaly.DisplayName;
        revealDescriptionText.text = anomaly.Description;
        pinnedNameText.text = anomaly.DisplayName;
        pinnedDescriptionText.text = anomaly.PinnedDescription;
    }

    private IEnumerator AnimateSectorIn(float duration)
    {
        Vector2 start = new(-70f, 130f);
        Vector2 target = new(0f, 130f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOut(elapsed / duration);
            sectorRect.anchoredPosition = Vector2.LerpUnclamped(start, target, t);
            sectorGroup.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        sectorRect.anchoredPosition = target;
        sectorGroup.alpha = 1f;
    }

    private IEnumerator GlitchFlash()
    {
        flashImage.color = new Color(DangerRed.r, DangerRed.g, DangerRed.b, 0.5f);
        yield return WaitRealtime(0.04f);
        flashImage.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f);
        yield return WaitRealtime(0.04f);
        flashImage.color = new Color(DangerRed.r, DangerRed.g, DangerRed.b, 0f);
    }

    private IEnumerator ScaleAndFadeCard(float duration)
    {
        float elapsed = 0f;
        Vector3 start = Vector3.one * 0.7f;
        Vector3 target = Vector3.one * 1.08f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOut(elapsed / duration);
            revealCardRect.localScale = Vector3.LerpUnclamped(start, target, t);
            revealCardGroup.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        revealCardRect.localScale = target;
        revealCardGroup.alpha = 1f;
    }

    private IEnumerator ScaleCard(Vector3 start, Vector3 target, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOut(elapsed / duration);
            revealCardRect.localScale = Vector3.LerpUnclamped(start, target, t);
            yield return null;
        }

        revealCardRect.localScale = target;
    }

    private IEnumerator DockCard(float duration)
    {
        Vector2 startPosition = revealCardRect.anchoredPosition;
        Vector2 targetPosition = new(735f, 380f);
        Vector3 startScale = revealCardRect.localScale;
        Vector3 targetScale = Vector3.one * 0.52f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOut(elapsed / duration);
            revealCardRect.anchoredPosition =
                Vector2.LerpUnclamped(startPosition, targetPosition, t);
            revealCardRect.localScale =
                Vector3.LerpUnclamped(startScale, targetScale, t);
            yield return null;
        }
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
            group.alpha = Mathf.Lerp(start, target, EaseOut(elapsed / duration));
            yield return null;
        }

        group.alpha = target;
    }

    private static IEnumerator WaitRealtime(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static float EaseOut(float value)
    {
        float t = Mathf.Clamp01(value);
        return 1f - (1f - t) * (1f - t);
    }

    private TextMeshProUGUI CreateTextChild(
        Transform parent,
        string objectName,
        Vector2 position,
        Vector2 size,
        float fontSize,
        FontStyles style,
        Color color)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        SetCenteredRect(rect, size, position);
        return AddText(
            textObject,
            fontSize,
            style,
            TextAlignmentOptions.Center,
            color
        );
    }

    private TextMeshProUGUI AddText(
        GameObject target,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color)
    {
        TextMeshProUGUI text = target.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
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
