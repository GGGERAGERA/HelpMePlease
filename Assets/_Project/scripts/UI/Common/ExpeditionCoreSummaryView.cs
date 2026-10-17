using TMPro;
using UnityEngine;

/// <summary>Only renders receipt-backed amounts supplied by the run snapshot.</summary>
public sealed class ExpeditionCoreSummaryView : MonoBehaviour
{
    [SerializeField] private TMP_Text breakdown;
    private ExpeditionCoreRewards rewards;
    public void Show(ExpeditionCoreRewards value)
    {
        rewards = value;
        gameObject.SetActive(value != null);
        if (value == null) return;
        string task = string.IsNullOrEmpty(value.TaskTitle) ? SurfaceMapBriefing.Text("expedition.none") : SurfaceMapBriefing.Text(value.TaskTitle);
        breakdown.text = SurfaceMapBriefing.Format("expedition.summary", task, value.TaskCores, value.SectorCores, value.FirstClearCores, value.Total);
    }
    private void OnEnable()
    { if (LocalizationService.Instance != null) LocalizationService.Instance.LanguageChanged += RefreshLanguage; }
    private void OnDisable()
    { if (LocalizationService.Instance != null) LocalizationService.Instance.LanguageChanged -= RefreshLanguage; }
    private void RefreshLanguage(GameLanguage language) => Show(rewards);
}
