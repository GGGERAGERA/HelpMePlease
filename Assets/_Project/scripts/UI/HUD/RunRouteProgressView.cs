using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Projects route state into authored gameplay and selection prefabs.</summary>
public sealed class RunRouteProgressView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI sectorText;
    [SerializeField] private TextMeshProUGUI objectiveText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private GameObject specialIcon;
    [SerializeField] private Image[] points;
    [SerializeField] private Color currentColor;
    [SerializeField] private Color completedColor;
    [SerializeField] private Color futureColor;
    private int displayedSector, displayedTotal;
    private string displayedObjectiveKey;
    private bool displayedSpecial;
    private void OnEnable() => LocalizationService.EnsureExists().LanguageChanged += RefreshLanguage;
    private void OnDisable()
    {
        if (LocalizationService.Instance != null) LocalizationService.Instance.LanguageChanged -= RefreshLanguage;
    }
    private void RefreshLanguage(GameLanguage _) => Refresh();
    public void ShowCurrent(int sector, int total) => Show(sector, total);
    public void ShowNext(int sector, int total) => Show(sector, total);
    public void Hide() => canvasGroup.alpha = 0f;
    private void Show(int sector, int total)
    {
        displayedSector = Mathf.Clamp(sector, 1, Mathf.Max(1, total));
        displayedTotal = Mathf.Max(1, total);
        canvasGroup.alpha = 1f;
        Refresh();
    }
    public void ShowObjective(int sector, int total, string objectiveKey, bool specialAvailable)
    {
        canvasGroup.alpha = objectiveKey == null ? 0f : 1f;
        if (displayedSector == sector && displayedTotal == total &&
            displayedObjectiveKey == objectiveKey && displayedSpecial == specialAvailable) return;
        displayedSector = sector;
        displayedTotal = total;
        displayedObjectiveKey = objectiveKey;
        displayedSpecial = specialAvailable;
        Refresh();
    }
    private void Refresh()
    {
        if (sectorText != null) sectorText.SetText("{0}/{1}", displayedSector, displayedTotal);
        if (objectiveText != null) objectiveText.text = displayedObjectiveKey == null
            ? string.Empty : LocalizationService.Instance.Get(displayedObjectiveKey);
        if (specialIcon != null) specialIcon.SetActive(displayedSpecial);
        for (int i = 0; i < points.Length; i++)
        {
            points[i].gameObject.SetActive(i < displayedTotal);
            points[i].color = i < displayedSector - 1 ? completedColor :
                i == displayedSector - 1 ? currentColor : futureColor;
        }
    }
}
