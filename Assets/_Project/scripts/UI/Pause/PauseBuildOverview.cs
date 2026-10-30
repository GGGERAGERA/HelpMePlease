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

    private RunStateManager displayedRun;
    private OrbitalRunState displayedState;
    private RunItemSlots displayedItems;
    private string confirmationKey;
    private bool hasBuild;
    private void OnEnable()
    {
        FitWindow();
        LocalizationService.EnsureExists().LanguageChanged += RefreshLanguage;
        RefreshLanguage(LocalizationService.Instance.CurrentLanguage);
    }
    private void OnDisable()
    {
        if (LocalizationService.Instance != null) LocalizationService.Instance.LanguageChanged -= RefreshLanguage;
    }
    private void RefreshLanguage(GameLanguage language)
    {
        if (displayedRun != null) Refresh(displayedRun);
        else if (hasBuild) RefreshBuild(displayedState, displayedItems);
        if (pendingAction != null) confirmationText.text = LocalizationService.EnsureExists().Get(confirmationKey);
    }
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
        displayedRun = run;
        var localization = LocalizationService.EnsureExists();
        float time = run != null ? run.GetCurrentRunTime() : RunStatsManager.Instance?.RunTime ?? 0f;
        int kills = run != null ? run.GetCurrentRunKills() : RunStatsManager.Instance?.Kills ?? 0;
        string sectorLabel = run?.CurrentSector?.SectorNumber == RunRoute.TutorialSector
            ? localization.Get("run.tutorialSectorLabel") : $"{run?.CurrentSector?.SectorNumber ?? 1} / {RunRoute.TotalSectors}";
        runText.text = $"{localization.Get("stats.time")}  <color=#FFFFFF>{(int)time / 60:00}:{(int)time % 60:00}</color>     " +
            $"{localization.Get("stats.kills")}  <color=#FFFFFF>{kills}</color>     " +
            $"{localization.Get("stats.level")}  <color=#FFFFFF>{ExperienceManager.Instance?.CurrentLevel ?? 1}</color>     " +
            $"{localization.Get("pause.sector")}  <color=#FFFFFF>{sectorLabel}</color>";
        RefreshBuild(run?.OrbitalStationState, run?.ItemSlots);
    }

    public void RefreshBuild(OrbitalRunState state, RunItemSlots items)
    {
        displayedState = state;
        displayedItems = items;
        hasBuild = true;
        var localization = LocalizationService.EnsureExists();
        ringsTitle.text = $"{localization.Get("pause.station")}  /  {localization.Get("pause.rings")}: {(state == null ? 0 : state.UsesSegmentedOrbit ? state.UnlockedSegmentedLevels : state.Rings.Count)}";
        var text = new StringBuilder();
        if (state != null)
        {
            foreach (var ring in state.Rings.Where(state.IsRingActive).OrderBy(r => r.Order))
            {
                string color = ColorUtility.ToHtmlStringRGB(OrbitalPresentationConfig.Active.GetRingTier(ring.VisualTier).BaseColor);
                int occupied = state.Modules.Count(m => m.StableRingId == ring.StableRingId);
                if (state.UsesSegmentedOrbit)
                {
                    int weaponCapacity = Enumerable.Range(0, ring.MountCount).Count(position => OrbitalRunState.IsSegmentedWeaponSlot(ring.Order, position));
                    int moduleCapacity = ring.MountCount - weaponCapacity;
                    string occupancy = $"O: {occupied}/{weaponCapacity}";
                    if (moduleCapacity > 0)
                    {
                        int installedModules = state.SegmentedLinks.Count(m => m.RingId == ring.StableRingId);
                        occupancy += $"    M: {installedModules}/{moduleCapacity}";
                    }
                    text.AppendLine($"<color=#{color}>■</color>  <b>{localization.Get("pause.ring")} {ring.Order + 1}</b>");
                    text.AppendLine($"<size=19>{localization.Get("pause.damage")} ×{ring.PowerMultiplier:0.##}    {localization.Get("pause.modules")} {occupancy}</size>\n");
                    continue;
                }
                string tier = ring.VisualTier switch { 1 => LocalizationService.EnsureExists().Get("pause.tier1"), 2 => LocalizationService.EnsureExists().Get("pause.tier2"), 3 => LocalizationService.EnsureExists().Get("pause.tier3"), _ => LocalizationService.EnsureExists().Get("pause.tier4") };
                text.AppendLine($"<color=#{color}>■</color>  <b>{localization.Get("pause.ring")} {ring.Order + 1}</b>   <color=#{color}>{tier}</color>");
                float speed = Mathf.Pow(1f + OrbitalProgressionConfig.Default.SpeedIncrement, ring.SpeedUpgradeLevel);
                text.AppendLine($"<size=19>{localization.Get("pause.damage")} ×{ring.PowerMultiplier:0.##}    {localization.Get("pause.speed")} ×{speed:0.##}    {localization.Get("pause.modules")} {occupied}</size>");
                text.AppendLine($"<size=19><color=#9AB3BE>{localization.Get("pause.mounts")}  {ring.MountCount} / {ring.MountCapacity}</color></size>\n");
            }
        }
        ringsText.text = text.ToString().TrimEnd();
        text.Clear();
        if (state != null)
            foreach (var group in state.Modules.GroupBy(m => m.ModuleType).OrderBy(g => g.Key))
                text.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGB(OrbitalRewardIconResolver.ModuleColor(group.Key))}>{ModuleName(group.Key, state)} ×{group.Count()}</color>");
        if (state?.UsesSegmentedOrbit == true)
            foreach (var group in state.SegmentedLinks.GroupBy(m => m.Kind).OrderBy(g => g.Key))
                text.AppendLine($"{SegmentedModuleName(group.Key)} ×{group.Count()}");
        modulesText.text = text.Length == 0 ? localization.Get("pause.noModules") : text.ToString().TrimEnd();
        if (state?.UsesSegmentedOrbit == true)
            modulesText.text += $"\n{localization.Get("pause.weaponLimit")}: {state.Modules.Count}/{state.SegmentedWeaponLimit}";
        text.Clear();
        if (state != null && !state.UsesSegmentedOrbit)
        {
            AppendUpgrade(text, LocalizationService.EnsureExists().Get("pause.matrix"), state.LinkMatrixUpgradeLevel);
        }
        coreText.text = text.ToString().TrimEnd();
        coreSection.SetActive(text.Length > 0);
        text.Clear();
        if (items != null)
            foreach (var slot in items.Slots)
            {
                if (slot.Item != null && slot.Level > 0)
                {
                    if (slot.Item.upgradeType == UpgradeType.MaxHealthFlat)
                        text.AppendLine($"{localization.Get("pause.maxHealth")}  {slot.Level}/3 · +{ProductionUpgradeProfiles.MaxHealthBonus(slot.Level):0}");
                    else if (slot.Item.upgradeType == UpgradeType.MoveSpeedPercent)
                        text.AppendLine($"{localization.Get("pause.moveSpeed")}  {slot.Level}/3 · ×{ProductionUpgradeProfiles.MoveSpeedMultiplier(slot.Level):0.00}");
                }
            }
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

    private static string SegmentedModuleName(SegmentedLinkKind kind)
    {
        string key = kind switch
        {
            SegmentedLinkKind.Damage => "constructor.damageLabel",
            SegmentedLinkKind.FireRate => "constructor.fireRateLabel",
            SegmentedLinkKind.ExtraShot => "constructor.extraShotLabel",
            SegmentedLinkKind.Ricochet => "constructor.ricochetLabel",
            _ => null
        };
        return key == null ? kind.ToString() : LocalizationService.EnsureExists().Get(key);
    }

    private static string ModuleName(OrbitalModuleKind kind, OrbitalRunState state)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (state?.UsesSegmentedOrbit == true)
        {
            if (kind == OrbitalModuleKind.Pistol && state.MissReturnPistolExperiment)
                return LocalizationService.EnsureExists().Get("reward.missReturnPistol");
            if (kind == OrbitalModuleKind.Saw && state.PulsedSawExperiment)
                return LocalizationService.EnsureExists().Get("reward.pulsedSaw");
        }
#endif
        return kind switch
        {
            OrbitalModuleKind.Saw => LocalizationService.EnsureExists().Get("reward.saw"),
            OrbitalModuleKind.Shotgun => LocalizationService.EnsureExists().Get("reward.shotgun"),
            OrbitalModuleKind.Sniper => LocalizationService.EnsureExists().Get("reward.sniper"),
            OrbitalModuleKind.ArcEmitter => LocalizationService.EnsureExists().Get("pause.arc"),
            OrbitalModuleKind.LinkNode => LocalizationService.EnsureExists().Get("pause.link"),
            _ => LocalizationService.EnsureExists().Get("pause.gun")
        };
    }

    public void AskConfirmation(string key, System.Action action)
    {
        pendingAction = action;
        confirmationKey = key;
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
