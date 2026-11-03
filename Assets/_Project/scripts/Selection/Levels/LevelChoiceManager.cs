using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LevelChoiceManager : MonoBehaviour
{
    [SerializeField] private LevelChoicePanelView panelView;
    [SerializeField] private WorldRuleData[] availableWorldRules;
    [SerializeField] private RouteSectorType[] nodeTypes;
    [SerializeField] private LocalAnomalyData defaultLocalAnomaly;
    [SerializeField] private StageProfileData[] stageProfiles;
    [SerializeField] private string gameplaySceneName = "MVP";

    private CharacterSpawner characters;
    private IReadOnlyList<RouteNode> choices;
    private Action startFirstSector;
    public bool IsChoosing => choices != null;

    internal void BindScene(CharacterSpawner playerOwner) => characters = playerOwner;

    public bool CanLeaveTutorial => SceneTransitionOverlay.CanLoad(gameplaySceneName) &&
        SceneTransitionOverlay.Instance != null;

    public void LeaveTutorial()
    {
        var run = RunStateManager.Instance;
        if (run != null && TutorialController.IsTutorialSector)
            TransitionToSector(run.CreateFirstNormalSector());
    }

    internal bool TryShowInitialChoice(Action onSelected)
    {
        startFirstSector = onSelected;
        return Show(RunRoute.FirstSector);
    }

    public bool TryShowChoices()
    {
        var run = RunStateManager.Instance;
        var sector = run?.CurrentSector;
        return sector != null && RunRoute.HasNextSector(sector.SectorNumber) &&
            Show(sector.SectorNumber + 1);
    }

    public void ShowChoices() => TryShowChoices();

    internal bool CanChoose => !SceneTransitionOverlay.IsTransitioning &&
        characters.Station is { IsInitialized: true } station && station.InputOwner.CanTransition;

    private bool Show(int nextSector)
    {
        var run = RunStateManager.Instance;
        if (IsChoosing || run.IsRunEnded || !CanChoose || !CanLeaveTutorial) return false;
        if (panelView == null || defaultLocalAnomaly == null || stageProfiles.Length != StageProfileData.ProductionTierCount)
        {
            Debug.LogError("[LevelChoiceManager] Authored route presentation or sector profiles are missing.", this);
            return false;
        }

        int level = RunRoute.LevelOf(nextSector);
        if (run.RouteGraph?.Level != level)
        {
            WorldRuleData initial = nextSector == RunRoute.FirstSector ? run.CurrentSector.WorldRule : null;
            run.RouteGraph = RunRouteGraph.Generate(level, availableWorldRules, nodeTypes, initial,
                nextSector == RunRoute.FirstSector && run.CurrentConfig.WorldRule != null);
        }

        var current = RunRoute.ColumnOf(nextSector) == 0 ? null : run.CurrentSector.RouteNode;
        choices = current == null ? run.RouteGraph.Entries : current.Next;
        characters.PrepareForSceneTransition();
        Time.timeScale = 0f;
        panelView.Show(run.RouteGraph, current, choices, SelectNode);
        return true;
    }

    private bool SelectNode(RouteNode node)
    {
        if (!IsChoosing || !CanChoose || !ContainsChoice(node)) return false;
        var run = RunStateManager.Instance;
        // Keep the existing first three presets, then reuse the last one.
        int profileIndex = Mathf.Min(node.SectorNumber, stageProfiles.Length) - 1;
        var sector = new RunSector(node.SectorNumber, stageProfiles[profileIndex], node.WorldRule,
            startFirstSector != null ? run.CurrentSector.LocalAnomaly : defaultLocalAnomaly, node);
        if (startFirstSector == null) return TransitionToSector(sector);

        Action begin = startFirstSector;
        startFirstSector = null;
        choices = null;
        run.SetCurrentSector(sector);
        panelView.Hide();
        begin();
        Time.timeScale = 1f;
        return true;
    }

    private bool ContainsChoice(RouteNode node)
    {
        foreach (var choice in choices)
            if (choice == node) return true;
        return false;
    }

    private bool TransitionToSector(RunSector sector)
    {
        var runState = RunStateManager.Instance;
        RunSector current = runState?.CurrentSector;
        if (current == null || !RunRoute.HasNextSector(current.SectorNumber) ||
            sector.SectorNumber != current.SectorNumber + 1 || !CanChoose) return false;

        return SceneTransitionOverlay.Load(gameplaySceneName, () =>
        {
            choices = null;
            runState.CommitCurrentSceneStats();
            runState.SaveExperienceState();
            runState.SavePlayerState(characters.SpawnedPlayer);
            runState.ReleaseCurrentSector(runState.RunId);
            runState.SetCurrentSector(sector);
            panelView.Hide();
        });
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool DebugSelectFirstRule() => IsChoosing && panelView.DebugConfirmFirstNode();
#endif
}
