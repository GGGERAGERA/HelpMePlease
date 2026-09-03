using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Death-only content using the same StationWindow shell as BunkerSelectionWindow.</summary>
public sealed class DeathResultPresentation : MonoBehaviour
{
    [SerializeField] private GameObject stationWindowPrefab;
    [SerializeField] private TextMeshProUGUI detailTextTemplate;

    private readonly Dictionary<GameObject, bool> legacyVisibility = new();
    private RectTransform window;
    private Image backdrop;
    private Color originalBackdrop;
    private TextMeshProUGUI sector;
    private TextMeshProUGUI time;
    private TextMeshProUGUI kills;
    private TextMeshProUGUI level;
    private TextMeshProUGUI gold;
    private TextMeshProUGUI rings;
    private TextMeshProUGUI modules;
    private TextMeshProUGUI core;
    private TextMeshProUGUI comment;
    private Material textMaterial;
    private Canvas modalCanvas;
    private GraphicRaycaster modalRaycaster;

    public void Show(RunSummary summary, string commentText)
    {
        if (window == null)
            Build();
        modalCanvas.enabled = true;
        modalRaycaster.enabled = true;
        int topOrder = 0;
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (canvas != modalCanvas && canvas.isActiveAndEnabled)
                topOrder = Mathf.Max(topOrder, canvas.sortingOrder);
        // Reserve the top UI order for the software cursor.
        modalCanvas.sortingOrder = Mathf.Min(short.MaxValue - 1, topOrder + 1);
        foreach (GameObject child in legacyVisibility.Keys)
            if (child != null) child.SetActive(false);
        window.gameObject.SetActive(true);
        window.SetAsLastSibling();
        if (backdrop != null)
        {
            Color color = window.GetComponent<Image>().color;
            color.a = 0.96f;
            backdrop.color = color;
        }
        sector.text = $"СЕКТОР {Mathf.Clamp(summary.SectorNumber, 1, 4)} / 4";
        int seconds = Mathf.Max(0, Mathf.FloorToInt(summary.RunTime));
        time.text = $"{seconds / 60:00}:{seconds % 60:00}";
        kills.text = summary.Kills.ToString();
        level.text = summary.PlayerLevel.ToString();
        gold.text = summary.GoldEarned.ToString();
        rings.text = summary.OrbitalRingCount.ToString();
        modules.text = summary.OrbitalModuleCount.ToString();
        core.text = summary.OrbitalCoreLevel.ToString();
        comment.text = string.IsNullOrWhiteSpace(commentText) || commentText.Contains("\uFFFD")
            ? "Данные эксперимента сохранены. Подготовьте следующего субъекта."
            : commentText;
        FitToCanvas();
    }

    public void RestoreLegacyView()
    {
        if (window == null) return;
        window.gameObject.SetActive(false);
        modalCanvas.enabled = false;
        modalRaycaster.enabled = false;
        foreach (var child in legacyVisibility)
            if (child.Key != null) child.Key.SetActive(child.Value);
        if (backdrop != null) backdrop.color = originalBackdrop;
    }

    private void Build()
    {
        foreach (Transform child in transform)
            legacyVisibility.Add(child.gameObject, child.gameObject.activeSelf);
        backdrop = GetComponent<Image>();
        if (backdrop != null) originalBackdrop = backdrop.color;
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        modalCanvas = gameObject.AddComponent<Canvas>();
        modalCanvas.overrideSorting = true;
        if (parentCanvas != null) modalCanvas.sortingLayerID = parentCanvas.sortingLayerID;
        modalRaycaster = gameObject.AddComponent<GraphicRaycaster>();

        // Instantiate the actual reusable shell, retaining its images, cyan
        // outlines, button transitions and font materials. No separate theme.
        window = Instantiate(stationWindowPrefab, transform).GetComponent<RectTransform>();
        window.name = "DeathResultWindow";
        window.GetComponent<VerticalLayoutGroup>().enabled = false;
        window.GetComponent<StationUIShell>().enabled = false;
        window.Find("StationProgressPanel").gameObject.SetActive(false);
        RectTransform header = (RectTransform)window.Find("Header");
        RectTransform body = (RectTransform)window.Find("Body");
        RectTransform footer = (RectTransform)window.Find("Footer");
        body.GetComponent<HorizontalLayoutGroup>().enabled = false;
        Place(header, 0f, 286f, 904f, 204f);
        Place(body, 0f, -12f, 904f, 360f);
        Place(footer, 0f, -344f, 904f, 80f);

        TextMeshProUGUI title = header.Find("Title").GetComponent<TextMeshProUGUI>();
        textMaterial = new Material(title.fontSharedMaterial);
        textMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        textMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
        TextMeshProUGUI status = Text(title, header, "ЭКСПЕРИМЕНТ ПРЕРВАН", 18f);
        status.color = new Color(0.95f, 0.25f, 0.28f);
        Place(status.rectTransform, 0f, 64f, 840f, 28f);
        ConfigureText(title, "СУБЪЕКТ УТРАЧЕН", 46f);
        Place(title.rectTransform, 0f, 7f, 840f, 68f);
        sector = Text(detailTextTemplate, header, string.Empty, 22f);
        Place(sector.rectTransform, 0f, -62f, 840f, 34f);

        RectTransform stats = (RectTransform)body.Find("MainContent");
        RectTransform orbital = (RectTransform)body.Find("InfoPanel");
        Place(stats, 0f, 67f, 904f, 224f);
        Place(orbital, 0f, -135f, 904f, 144f);
        time = Stat(stats, title, "ВРЕМЯ", -220f, 52f);
        kills = Stat(stats, title, "УБИЙСТВА", 220f, 52f);
        level = Stat(stats, title, "УРОВЕНЬ", -220f, -52f);
        gold = Stat(stats, title, "ПОЛУЧЕННОЕ ЗОЛОТО", 220f, -52f);
        TextMeshProUGUI orbitalTitle = Text(title, orbital, "ОРБИТАЛЬНАЯ СТАНЦИЯ", 20f);
        // The station-name component is the shell's existing cyan text style.
        orbitalTitle.color = window.Find("StationProgressPanel/StationName")
            .GetComponent<TextMeshProUGUI>().color;
        Place(orbitalTitle.rectTransform, 0f, 45f, 840f, 30f);
        rings = Stat(orbital, title, "КОЛЬЦА", -290f, -15f, 260f);
        modules = Stat(orbital, title, "МОДУЛИ", 0f, -15f, 260f);
        core = Stat(orbital, title, "УРОВЕНЬ ЯДРА", 290f, -15f, 260f);

        comment = Text(detailTextTemplate, window, string.Empty, 17f);
        comment.alpha = 0.65f;
        comment.textWrappingMode = TextWrappingModes.Normal;
        Place(comment.rectTransform, 0f, -265f, 856f, 60f);

        Button primary = footer.Find("PrimaryActionButton").GetComponent<Button>();
        Button secondary = footer.Find("BackButton").GetComponent<Button>();
        ConfigureButton(primary, "ПОВТОРИТЬ", -222f);
        ConfigureButton(secondary, "ВЕРНУТЬСЯ В БУНКЕР", 222f);
        primary.GetComponentInChildren<TextMeshProUGUI>(true).color = orbitalTitle.color;
        primary.onClick.AddListener(Restart);
        secondary.onClick.AddListener(ReturnToBunker);
    }

    private TextMeshProUGUI Stat(Transform parent, TextMeshProUGUI valueTemplate,
        string label, float x, float y, float width = 400f)
    {
        TextMeshProUGUI caption = Text(detailTextTemplate, parent, label, 17f);
        caption.alpha = 0.75f;
        Place(caption.rectTransform, x, y + 17f, width, 26f);
        TextMeshProUGUI value = Text(valueTemplate, parent, "0", 30f);
        Place(value.rectTransform, x, y - 20f, width, 42f);
        return value;
    }

    private TextMeshProUGUI Text(TextMeshProUGUI template, Transform parent,
        string content, float size)
    {
        TextMeshProUGUI text = Instantiate(template, parent);
        text.name = string.IsNullOrEmpty(content) ? "Value" : content;
        text.gameObject.SetActive(true);
        ConfigureText(text, content, size);
        return text;
    }

    private void ConfigureText(TextMeshProUGUI text, string content, float size)
    {
        text.fontSharedMaterial = textMaterial;
        text.text = content;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * 0.8f;
        text.fontSizeMax = size;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        // Do not mutate the shared font material used by the other windows.
        foreach (Shadow effect in text.GetComponents<Shadow>()) effect.enabled = false;
    }

    private void ConfigureButton(Button button, string label, float x)
    {
        Place((RectTransform)button.transform, x, 0f, 420f, 56f);
        ConfigureText(button.GetComponentInChildren<TextMeshProUGUI>(true), label, 22f);
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    private void LateUpdate()
    {
        if (window != null && window.gameObject.activeSelf) FitToCanvas();
    }

    private void FitToCanvas()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        Vector2 available = canvasRect != null ? canvasRect.rect.size : new Vector2(1920f, 1080f);
        Place(window, 0f, 0f, 1000f, 840f);
        window.localScale = Vector3.one * Mathf.Min(1f,
            Mathf.Min((available.x - 48f) / 1000f, (available.y - 48f) / 840f));
    }

    // Delegate unchanged to the production flow, including its duplicate-click guards.
    private static void Restart() => GameOverManager.Instance?.RestartGame();
    private static void ReturnToBunker() => GameOverManager.Instance?.MainMenu();

    private void OnDestroy()
    {
        if (textMaterial != null) Destroy(textMaterial);
    }
}
