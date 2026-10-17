using TMPro;
using UnityEngine;

/// <summary>Compact addition to the existing HUD; observes the run-owned objective.</summary>
public sealed class ExpeditionTaskHud : MonoBehaviour
{
    [SerializeField] private CanvasGroup content;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text progress;
    [SerializeField] private HUDManager hud;
    private ExpeditionTaskRun expedition;
    private LocalizationService localization;
    public bool HasTask => expedition?.Task != null;
    private void OnEnable()
    {
        expedition = RunStateManager.Instance?.Expedition;
        if (expedition != null) { expedition.Changed += Refresh; expedition.Completed += NotifyCompleted; }
        localization = LocalizationService.Instance;
        if (localization != null) localization.LanguageChanged += LanguageChanged;
        Refresh();
    }
    private void OnDisable()
    {
        if (expedition != null) { expedition.Changed -= Refresh; expedition.Completed -= NotifyCompleted; }
        if (localization != null) localization.LanguageChanged -= LanguageChanged;
    }
    private void LateUpdate() => content.alpha = expedition?.Task != null && hud.IsInformationVisible ? 1f : 0f;
    private void LanguageChanged(GameLanguage language) => Refresh();
    private void Refresh()
    {
        content.alpha = 0f;
        if (expedition?.Task == null) return;
        var task = expedition.Task;
        title.text = (expedition.IsCompleted ? "[V] " : "") + SurfaceMapBriefing.Text(task.Title);
        progress.text = expedition.IsCompleted ? SurfaceMapBriefing.Format("expedition.task_paid", expedition.Snapshot.TaskCores) :
            SurfaceMapBriefing.Format(task.ProgressLabel, expedition.Progress, task.Target);
    }
    private void NotifyCompleted() => RunMessageService.Instance?.ShowCustom(
        SurfaceMapBriefing.Text("expedition.completed"),
        SurfaceMapBriefing.Format("expedition.task_paid", expedition.Snapshot.TaskCores), 3f);
}
