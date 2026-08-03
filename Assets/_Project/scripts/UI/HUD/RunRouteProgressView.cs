using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RunRouteProgressView : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Sprite pointSprite;
    [SerializeField] private bool showPoints = true;

    private readonly List<Image> points = new();
    private TextMeshProUGUI sectorText;
    private RectTransform pointsRoot;
    private TextMeshProUGUI finalLabel;
    private bool built;

    private static readonly Color Cyan =
        new(0.12f, 0.78f, 0.9f, 1f);
    private static readonly Color Completed =
        new(0.12f, 0.78f, 0.9f, 0.48f);
    private static readonly Color Future =
        new(0.12f, 0.3f, 0.36f, 0.34f);

    public void ShowCurrent(int currentSector, int totalSectors)
    {
        Show(currentSector, totalSectors);
    }

    public void ShowNext(int nextSector, int totalSectors)
    {
        Show(nextSector, totalSectors);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Show(int sectorNumber, int totalSectors)
    {
        Build();

        int safeTotal = Mathf.Max(1, totalSectors);
        int safeSector = Mathf.Clamp(sectorNumber, 1, safeTotal);

        gameObject.SetActive(true);
        sectorText.text = $"СЕКТОР {safeSector} / {safeTotal}";

        if (!showPoints)
            return;

        EnsurePoints(safeTotal);

        for (int i = 0; i < points.Count; i++)
        {
            Image point = points[i];
            bool visible = i < safeTotal;
            point.gameObject.SetActive(visible);

            if (!visible)
                continue;

            point.color = i < safeSector - 1
                ? Completed
                : i == safeSector - 1
                    ? Cyan
                    : Future;

            RectTransform pointRect = (RectTransform)point.transform;
            float size = i == safeTotal - 1 ? 20f : 16f;
            pointRect.sizeDelta = new Vector2(size, size);
        }

        finalLabel.text = "ФИНАЛ";
        finalLabel.gameObject.SetActive(true);
    }

    private void Build()
    {
        if (built)
            return;

        built = true;
        sectorText = CreateText("SectorText", transform);
        RectTransform textRect = sectorText.rectTransform;
        textRect.anchorMin = new Vector2(0f, showPoints ? 0.58f : 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(12f, 0f);
        textRect.offsetMax = new Vector2(-12f, -2f);
        sectorText.fontSize = showPoints ? 24f : 20f;
        sectorText.fontStyle = FontStyles.Bold;

        if (!showPoints)
            return;

        GameObject pointsObject = CreateUiObject("Points", transform);
        pointsRoot = (RectTransform)pointsObject.transform;
        pointsRoot.anchorMin = new Vector2(0.5f, 0.5f);
        pointsRoot.anchorMax = new Vector2(0.5f, 0.5f);
        pointsRoot.pivot = new Vector2(0.5f, 0.5f);
        pointsRoot.anchoredPosition = new Vector2(0f, -12f);
        pointsRoot.sizeDelta = new Vector2(560f, 24f);

        HorizontalLayoutGroup layout = pointsObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 34f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

    }

    private void EnsurePoints(int totalSectors)
    {
        while (points.Count < totalSectors)
        {
            GameObject pointObject = CreateUiObject(
                $"SectorPoint_{points.Count + 1:00}",
                pointsRoot
            );
            RectTransform rect = (RectTransform)pointObject.transform;
            rect.sizeDelta = new Vector2(16f, 16f);

            Image image = pointObject.AddComponent<Image>();
            image.sprite = pointSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            points.Add(image);
        }

        Image finalPoint = points[totalSectors - 1];
        Outline outline = finalPoint.GetComponent<Outline>();

        if (outline == null)
            outline = finalPoint.gameObject.AddComponent<Outline>();

        outline.effectColor = Cyan;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;

        if (finalLabel == null)
        {
            finalLabel = CreateText("FinalLabel", finalPoint.transform);
            RectTransform finalRect = finalLabel.rectTransform;
            finalRect.anchorMin = new Vector2(0.5f, 0.5f);
            finalRect.anchorMax = new Vector2(0.5f, 0.5f);
            finalRect.pivot = new Vector2(0.5f, 0.5f);
            finalRect.anchoredPosition = new Vector2(0f, -27f);
            finalRect.sizeDelta = new Vector2(92f, 22f);
            finalLabel.fontSize = 15f;
            finalLabel.fontStyle = FontStyles.Bold;
            finalLabel.color = Cyan;
        }
        else if (finalLabel.transform.parent != finalPoint.transform)
        {
            finalLabel.transform.SetParent(finalPoint.transform, false);
        }
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateUiObject(
        string objectName,
        Transform parent)
    {
        GameObject result = new(objectName, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }
}
