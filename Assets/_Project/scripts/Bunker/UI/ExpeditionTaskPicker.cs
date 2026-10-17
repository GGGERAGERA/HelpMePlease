using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ExpeditionTaskPicker : MonoBehaviour
{
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text rewards;
    [SerializeField] private TMP_Text hint;
    [SerializeField] private RectTransform list;
    [SerializeField] private ExpeditionTaskChoice template;
    [SerializeField] private Button noneButton;
    [SerializeField] private Button launchButton;
    [SerializeField] private Button backButton;
    private readonly List<(ExpeditionTaskDefinition task, ExpeditionTaskChoice view)> rows = new();
    private SurfaceMapService map;
    private Action launch;
    private LocalizationService localization;
    public void Show(SurfaceMapService service, Action start)
    {
        map = service; launch = start;
        foreach (var row in rows) Destroy(row.view.gameObject);
        rows.Clear();
        var sector = map.Definition.Find(map.SelectedSectorId);
        foreach (var task in sector.ExpeditionTasks)
        {
            if (task == null || !task.IsAvailable(sector.BuildRunConfig(map.Definition.Id))) continue;
            var row = Instantiate(template, list);
            row.gameObject.SetActive(true);
            row.Button.onClick.AddListener(() => { map.TrySelectTask(task.Id); Refresh(); });
            rows.Add((task, row));
        }
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        noneButton.onClick.RemoveAllListeners(); noneButton.onClick.AddListener(() => { map.TrySelectTask(null); Refresh(); });
        backButton.onClick.RemoveAllListeners(); backButton.onClick.AddListener(() => gameObject.SetActive(false));
        launchButton.onClick.RemoveAllListeners(); launchButton.onClick.AddListener(Launch);
        Refresh();
    }
    private void OnEnable()
    {
        localization = LocalizationService.Instance;
        if (localization != null) localization.LanguageChanged += LanguageChanged;
    }
    private void OnDisable()
    {
        if (localization != null) localization.LanguageChanged -= LanguageChanged;
    }
    private void LanguageChanged(GameLanguage language) => Refresh();
    private void Launch()
    {
        if (SceneTransitionOverlay.IsTransitioning) return;
        launch?.Invoke();
        gameObject.SetActive(false);
    }
    private void Refresh()
    {
        if (map == null) return;
        var sector = map.Definition.Find(map.SelectedSectorId);
        title.text = SurfaceMapBriefing.Text(sector.DisplayName);
        rewards.text = SurfaceMapBriefing.Format("expedition.rewards", sector.VictoryCores,
            map.GetStatus(sector.Id) == SurfaceSectorStatus.Completed ? 0 : sector.FirstClearCores);
        hint.text = SurfaceMapBriefing.Text("expedition.choose_one");
        foreach (var row in rows) row.view.Present(row.task, map.SelectedTask == row.task);
        noneButton.GetComponentInChildren<TMP_Text>().text = (map.SelectedTask == null ? "[V] " : "") + SurfaceMapBriefing.Text("expedition.none");
        launchButton.GetComponentInChildren<TMP_Text>().text = SurfaceMapBriefing.Text("expedition.launch");
        backButton.GetComponentInChildren<TMP_Text>().text = SurfaceMapBriefing.Text("expedition.back");
    }
}
