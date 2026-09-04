using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays the run snapshot in an authored death window; flow remains with GameOverManager.</summary>
public sealed class DeathResultPresentation : MonoBehaviour
{

    private readonly Dictionary<GameObject, bool> legacyVisibility = new();
    [SerializeField] private RectTransform window;
    [SerializeField] private Image backdrop;
    private Color originalBackdrop;
    [SerializeField] private TextMeshProUGUI sector;
    [SerializeField] private TextMeshProUGUI time;
    [SerializeField] private TextMeshProUGUI kills;
    [SerializeField] private TextMeshProUGUI level;
    [SerializeField] private TextMeshProUGUI gold;
    [SerializeField] private TextMeshProUGUI rings;
    [SerializeField] private TextMeshProUGUI modules;
    [SerializeField] private TextMeshProUGUI core;
    [SerializeField] private TextMeshProUGUI comment;
    [SerializeField] private GameObject[] legacyObjects;
    [SerializeField] private bool[] legacyActive;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button bunkerButton;
    [SerializeField] private Image windowImage;
    [SerializeField] private RectTransform rootCanvasRect;
    [SerializeField] private Canvas modalCanvas;
    [SerializeField] private GraphicRaycaster modalRaycaster;

    private bool viewValid;

    private void Awake()
    {
        viewValid = window != null && backdrop != null && windowImage != null &&
            rootCanvasRect != null && modalCanvas != null && modalRaycaster != null &&
            sector != null && time != null && kills != null && level != null && gold != null &&
            rings != null && modules != null && core != null && comment != null &&
            restartButton != null && bunkerButton != null && legacyObjects != null &&
            legacyActive != null && legacyObjects.Length == legacyActive.Length;
        if (!viewValid)
        {
            Debug.LogError("[DeathResultPresentation] Authored result references are missing.", this);
            enabled = false;
            return;
        }
        originalBackdrop = backdrop.color;
        for (int i = 0; i < legacyObjects.Length; i++)
        {
            if (legacyObjects[i] == null)
            {
                Debug.LogError("[DeathResultPresentation] Authored victory region is missing.", this);
                viewValid = false;
                enabled = false;
                return;
            }
            legacyVisibility.Add(legacyObjects[i], legacyActive[i]);
        }
        restartButton.onClick.AddListener(Restart);
        bunkerButton.onClick.AddListener(ReturnToBunker);
    }

    public void Show(RunSummary summary, string commentText)
    {
        if (!viewValid || summary == null) return;
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
            Color color = windowImage.color;
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
        if (!viewValid) return;
        window.gameObject.SetActive(false);
        modalCanvas.enabled = false;
        modalRaycaster.enabled = false;
        foreach (var child in legacyVisibility)
            if (child.Key != null) child.Key.SetActive(child.Value);
        if (backdrop != null) backdrop.color = originalBackdrop;
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
        if (viewValid && window.gameObject.activeSelf) FitToCanvas();
    }

    private void FitToCanvas()
    {
        Vector2 available = rootCanvasRect.rect.size;
        Place(window, 0f, 0f, 1000f, 840f);
        window.localScale = Vector3.one * Mathf.Min(1f,
            Mathf.Min((available.x - 48f) / 1000f, (available.y - 48f) / 840f));
    }

    // Delegate unchanged to the production flow, including its duplicate-click guards.
    private static void Restart() => GameOverManager.Instance?.RestartGame();
    private static void ReturnToBunker() => GameOverManager.Instance?.MainMenu();

    private void OnDestroy()
    {
        if (restartButton != null) restartButton.onClick.RemoveListener(Restart);
        if (bunkerButton != null) bunkerButton.onClick.RemoveListener(ReturnToBunker);
    }
}
