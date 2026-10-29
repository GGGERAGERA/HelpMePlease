#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

public sealed partial class UpgradeManager
{
    private bool debugCatalogueReward;
    public IReadOnlyList<OrbitalRewardData> DebugRewardCatalogue => orbitalRewardProvider?.GetActiveCatalogue(RunStateManager.Instance?.OrbitalStationState) ?? System.Array.Empty<OrbitalRewardData>();
    public UpgradeCardView DebugCardTemplate => upgradePanelView != null ? upgradePanelView.CardViews.FirstOrDefault(v => v != null) : null;
    public string DebugRewardUnavailableReason(OrbitalRewardKind kind)
    {
        if (!CanAcceptWorldEventReward) return "Системы рана ещё не готовы";
        if (SceneTransitionOverlay.IsTransitioning) return "Идёт переход между сценами";
        if (orbitalStation.Owner == null || orbitalStation.Owner.IsDead) return "Игрок не готов или погиб";
        if (!IsRewardQueueIdle) return "Сначала завершите текущую награду или установку";
        return orbitalRewardProvider.GetUnavailableReason(kind, RunStateManager.Instance?.OrbitalStationState, RunStateManager.Instance?.ItemSlots);
    }
    public string DebugRewardLevel(OrbitalRewardData data) => orbitalRewardProvider.GetLevelSummary(data, RunStateManager.Instance?.OrbitalStationState, RunStateManager.Instance?.ItemSlots);

    public bool DebugOfferCatalogueReward(OrbitalRewardKind kind, bool installImmediately = false)
    {
        if (DebugRewardUnavailableReason(kind) != null) return false;
        var data = orbitalRewardProvider.GetDefinition(kind);
        debugCatalogueReward = true;
        RunStateManager.Instance?.DebugMarkDevelopmentRun();
        if (installImmediately)
        {
            if (TryBeginDirectNormalReward(data, null)) return true;
            debugCatalogueReward = false; return false;
        }
        BeginChoiceRequest(new UpgradeChoiceRequest(ExperienceManager.Instance?.CurrentLevel ?? 1, false, 1, false, false), new List<UpgradeData> { data });
        return true;
    }
    public bool DebugCancelCatalogueReward()
    {
        if (!debugCatalogueReward || !isChoosingUpgrade) return false;
        if (orbitalRewardFlow != null) orbitalRewardFlow.CancelForSceneTransition();
        else CloseUpgradeSelection();
        return true;
    }
    private void Update()
    {
        if (debugCatalogueReward && Input.GetKeyDown(KeyCode.Escape) && orbitalRewardFlow == null &&
            (boundOrbitalInput == null || !boundOrbitalInput.IsGameplayInputBlocked))
        {
            boundOrbitalInput?.DebugConsumeExternalUiEscape();
            DebugCancelCatalogueReward();
        }
    }
}
#endif
