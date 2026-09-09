using System.Linq;
using System.Text;
using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Read-only presentation of the current run in an authored, scrollable shell.</summary>
public sealed class PauseBuildOverview : MonoBehaviour
{
    [SerializeField] private RectTransform window;
    [SerializeField] private TMP_Text runText;
    [SerializeField] private TMP_Text ringsTitle;
    [SerializeField] private TMP_Text ringsText;
    [SerializeField] private TMP_Text modulesText;
    [SerializeField] private TMP_Text coreText;
    [SerializeField] private TMP_Text playerText;
    [SerializeField] private GameObject coreSection;
    [SerializeField] private GameObject playerSection;
    [SerializeField] private ScrollRect ringsScroll;
    [SerializeField] private ScrollRect summaryScroll;
    [SerializeField] private GameObject confirmation;
    [SerializeField] private TMP_Text confirmationText;
    [SerializeField] private CanvasGroup content;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    private System.Action pendingAction;

    public bool IsConfirming => confirmation.activeSelf;

    private void OnEnable() => FitWindow();
    private void OnRectTransformDimensionsChange() => FitWindow();
    private void FitWindow()
    {
        if (window == null) return; // Serialized references are assigned after AddComponent in the Editor.
        var bounds = ((RectTransform)transform).rect.size;
        float scale = Mathf.Min(1f, (bounds.x - 48f) / window.sizeDelta.x, (bounds.y - 48f) / window.sizeDelta.y);
        window.localScale = Vector3.one * Mathf.Max(.01f, scale);
    }

    private void Awake()
    {
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(CancelConfirmation);
    }

    public void Refresh(RunStateManager run)
    {
        var localization = LocalizationService.EnsureExists();
        float time = run != null ? run.GetCurrentRunTime() : RunStatsManager.Instance?.RunTime ?? 0f;
        int kills = run != null ? run.GetCurrentRunKills() : RunStatsManager.Instance?.Kills ?? 0;
        runText.text = $"{localization.Get("stats.time")}  <color=#FFFFFF>{(int)time / 60:00}:{(int)time % 60:00}</color>     " +
            $"{localization.Get("stats.kills")}  <color=#FFFFFF>{kills}</color>     " +
            $"{localization.Get("stats.level")}  <color=#FFFFFF>{ExperienceManager.Instance?.CurrentLevel ?? 1}</color>     " +
            $"{localization.Get("pause.sector")}  <color=#FFFFFF>{run?.CurrentSector?.SectorNumber ?? 1} / {RunRoute.TotalSectors}</color>";
        RefreshBuild(run?.OrbitalStationState, run?.ItemSlots);
    }

    public void RefreshBuild(OrbitalRunState state, RunItemSlots items)
    {
        var localization = LocalizationService.EnsureExists();
        ringsTitle.text = $"ORBITAL STATION  /  {localization.Get("pause.rings")}: {state?.Rings.Count ?? 0}";
        var text = new StringBuilder();
        if (state != null)
        {
            foreach (var ring in state.Rings.OrderBy(r => r.Order))
            {
                string color = ColorUtility.ToHtmlStringRGB(OrbitalPresentationConfig.Active.GetRingTier(ring.VisualTier).BaseColor);
                string tier = ring.VisualTier switch { 1 => "WHITE", 2 => "CYAN", 3 => "VIOLET", _ => "GOLD" };
                int occupied = state.Modules.Count(m => m.StableRingId == ring.StableRingId);
                text.AppendLine($"<color=#{color}>■</color>  <b>{localization.Get("pause.ring")} {ring.Order + 1}</b>   <color=#{color}>{tier}</color>");
                float speed = Mathf.Pow(1f + OrbitalProgressionConfig.Default.SpeedIncrement, ring.SpeedUpgradeLevel);
                text.AppendLine($"<size=19>POWER ×{ring.PowerMultiplier:0.##}    SPEED ×{speed:0.##}    CAPACITY {ring.MountCapacity}</size>");
                text.AppendLine($"<size=19><color=#9AB3BE>{localization.Get("pause.mounts")}  {occupied} / {ring.MountCapacity}</color></size>\n");
            }
        }
        ringsText.text = text.ToString().TrimEnd();
        text.Clear();
        if (state != null)
            foreach (var group in state.Modules.GroupBy(m => m.ModuleType).OrderBy(g => g.Key))
                text.AppendLine($"{ModuleName(group.Key)} <color=#14D1DB>×{group.Count()}</color>");
        modulesText.text = text.Length == 0 ? localization.Get("pause.noModules") : text.ToString().TrimEnd();
        text.Clear();
        if (state != null)
        {
            if (state.CoreState.Level > 0)
                text.AppendLine("CORE " + (state.CoreState.Level switch { 1 => "I — 1 волна импульса", 2 => "II — 2 волны импульса", _ => "III — 3 волны импульса" }));
            AppendUpgrade(text, "Link Matrix", state.CoreState.LinkMatrixUpgradeLevel);
        }
        coreText.text = text.ToString().TrimEnd();
        coreSection.SetActive(text.Length > 0);
        text.Clear();
        if (items != null)
            foreach (var slot in items.Slots)
                if (slot.Item != null && slot.Level > 0)
                    AppendUpgrade(text, slot.Item.upgradeName, slot.Level);
        playerText.text = text.ToString().TrimEnd();
        playerSection.SetActive(text.Length > 0);
        Canvas.ForceUpdateCanvases();
        ringsScroll.StopMovement();
        summaryScroll.StopMovement();
        ringsScroll.verticalNormalizedPosition = summaryScroll.verticalNormalizedPosition = 1f;
    }

    private static void AppendUpgrade(StringBuilder text, string name, int level)
    {
        if (level > 0) text.AppendLine($"{name}  <color=#14D1DB>+{level}</color>");
    }

    private static string ModuleName(OrbitalModuleKind kind) => kind switch
    {
        OrbitalModuleKind.LaserSword => "Laser Sword",
        OrbitalModuleKind.ImpulseGun => "Impulse Gun",
        OrbitalModuleKind.ArcEmitter => "Arc Emitter",
        OrbitalModuleKind.LinkNode => "Link Node",
        _ => kind.ToString()
    };

    public void AskConfirmation(string key, System.Action action)
    {
        pendingAction = action;
        confirmationText.text = LocalizationService.EnsureExists().Get(key);
        content.interactable = false;
        content.blocksRaycasts = false;
        confirmation.SetActive(true);
        cancelButton.Select();
    }

    public void CancelConfirmation()
    {
        pendingAction = null;
        confirmation.SetActive(false);
        content.interactable = content.blocksRaycasts = true;
    }

    private void Confirm()
    {
        var action = pendingAction;
        CancelConfirmation();
        action?.Invoke();
    }
}
