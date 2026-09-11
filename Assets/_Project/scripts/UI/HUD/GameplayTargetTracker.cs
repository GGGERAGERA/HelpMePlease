using TMPro;
using UnityEngine;

/// <summary>Read-only presentation of the nearest available production map target.</summary>
public sealed class GameplayTargetTracker : MonoBehaviour
{
    // Order also resolves exact distance ties; it does not override proximity.
    private enum TargetKind { Exit, SpecialAnomaly, Event, Container, Anomaly }

    [SerializeField] private HUDManager hud;
    [SerializeField] private WorldEventSpawner eventSpawner;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private GameObject panel;
    [SerializeField] private RectTransform arrow;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private TextMeshProUGUI distanceText;
    [SerializeField] private GameObject[] typeIcons;

    private Transform player;
    private Transform selectedTarget;
    private TargetKind selectedKind;
    private float selectedDistanceSquared;
    private int displayedDistance = -1;
    private TargetKind? displayedKind;
    private GameLanguage displayedLanguage;

    public void BindPlayer(Transform target) => player = target;

    private void Awake()
    {
        PixelEventArrow.Apply(arrow);
        panel.SetActive(false);
    }

    private void LateUpdate()
    {
        if (player == null || !hud.IsInformationVisible || hud.IsBossForegroundVisible)
        {
            panel.SetActive(false);
            selectedTarget = null;
            return;
        }

        selectedTarget = null;
        selectedDistanceSquared = float.PositiveInfinity;
        foreach (var exit in ProductionSectorExit.ActiveExits)
            if (exit.IsAvailable) Consider(exit, TargetKind.Exit);
        foreach (var site in ProductionAnomalySite.ActiveSites)
            if (site.IsMapVisible)
                Consider(site, site.IsSpecial ? TargetKind.SpecialAnomaly : TargetKind.Anomaly);
        foreach (var worldEvent in eventSpawner.SpawnedEvents)
            if (worldEvent != null && !worldEvent.IsCompleted && !worldEvent.IsFailed)
                Consider(worldEvent, TargetKind.Event);
        foreach (var container in WorldBreakable.ActiveInstances)
            if (!container.IsBroken) Consider(container, TargetKind.Container);

        panel.SetActive(selectedTarget != null);
        if (selectedTarget == null) return;

        Vector3 from = worldCamera.WorldToScreenPoint(player.position);
        Vector3 to = worldCamera.WorldToScreenPoint(selectedTarget.position);
        Vector2 direction = to - from;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Round(angle / 45f) * 45f);

        int distance = Mathf.RoundToInt(Mathf.Sqrt(selectedDistanceSquared));
        if (distance != displayedDistance)
        {
            displayedDistance = distance;
            distanceText.SetText("{0}", distance);
        }
        var language = LocalizationService.Instance.CurrentLanguage;
        if (displayedKind == selectedKind && displayedLanguage == language) return;
        displayedKind = selectedKind;
        displayedLanguage = language;
        for (int i = 0; i < typeIcons.Length; i++)
            typeIcons[i].SetActive(i == (int)selectedKind);
        string key = selectedKind switch
        {
            TargetKind.Exit => "hud.target.exit",
            TargetKind.SpecialAnomaly => "hud.target.special",
            TargetKind.Event => "hud.target.event",
            TargetKind.Container => "hud.target.container",
            _ => "hud.target.anomaly"
        };
        label.text = LocalizationService.Instance.Get(key);
    }

    private void Consider(Behaviour candidate, TargetKind kind)
    {
        if (!candidate.isActiveAndEnabled || candidate.gameObject.scene != gameObject.scene) return;
        float distance = ((Vector2)candidate.transform.position - (Vector2)player.position).sqrMagnitude;
        if (distance > selectedDistanceSquared ||
            (distance == selectedDistanceSquared && selectedTarget != null && kind >= selectedKind)) return;
        selectedTarget = candidate.transform;
        selectedKind = kind;
        selectedDistanceSquared = distance;
    }
}
