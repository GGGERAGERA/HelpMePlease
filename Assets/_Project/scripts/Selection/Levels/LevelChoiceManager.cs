using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LevelChoiceManager : MonoBehaviour
{
    [SerializeField] private LevelChoicePanelView panelView;
    [SerializeField] private WorldRuleData[] availableWorldRules;
    [SerializeField] private RouteLocationWeight[] locationWeights;
    [SerializeField, Range(0f, 1f)] private float surpriseChance = .2f;
    [SerializeField] private LocalAnomalyData defaultLocalAnomaly;
    [SerializeField] private StageProfileData[] stageProfiles;
    [SerializeField] private string gameplaySceneName = "MVP";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal IReadOnlyList<StageProfileData> DebugStageProfiles => stageProfiles ?? Array.Empty<StageProfileData>();
#endif

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
        return sector != null && RunRoute.IsExplorationSector(sector.SectorNumber) && RunRoute.HasNextSector(sector.SectorNumber) &&
            Show(sector.SectorNumber + 1);
    }

    public void ShowChoices() => TryShowChoices();

    internal bool CanChoose => !SceneTransitionOverlay.IsTransitioning &&
        characters.Station is { IsInitialized: true } station && station.InputOwner.CanTransition;

    internal RunRouteGraph CreateGraph(WorldRuleData initialRule, bool fixedEntryRule = false,
        IReadOnlyList<SurfaceSectorDefinition> expeditions = null) =>
        RunRouteGraph.Generate(GameplayRandom.IsActive ? GameplayRandom.Seed : GameplayRandom.NextAuto(),
            availableWorldRules, locationWeights, initialRule, fixedEntryRule, expeditions, surpriseChance);

    private bool Show(int nextSector)
    {
        var run = RunStateManager.Instance;
        if (IsChoosing || run.IsRunEnded || !CanChoose || !CanLeaveTutorial) return false;
        if (panelView == null || defaultLocalAnomaly == null || stageProfiles.Length != StageProfileData.ProductionTierCount)
        {
            Debug.LogError("[LevelChoiceManager] Authored route presentation or sector profiles are missing.", this);
            return false;
        }

        bool opening = nextSector == RunRoute.FirstSector;
        run.RouteGraph ??= CreateGraph(run.CurrentSector.WorldRule, run.CurrentConfig.WorldRule != null);
        var current = opening ? null : run.CurrentSector.RouteNode;
        choices = current == null ? run.RouteGraph.NodesAt(nextSector) : current.Next;
        if (choices.Count == 0) { choices = null; return false; }
        characters.PrepareForSceneTransition();
        Time.timeScale = 0f;
        panelView.Show(run.RouteGraph, run.RoutePath, choices, SelectNode);
        return true;
    }

    private bool SelectNode(RouteNode node)
    {
        if (!IsChoosing || !CanChoose || !ContainsChoice(node)) return false;
        var run = RunStateManager.Instance;
        var current = run.CurrentSector;
        bool sameWasteland = current != null && RunRoute.LevelOf(current.SectorNumber) == node.Level;
        var sector = new RunSector(node.SectorNumber, ProfileFor(node.SectorNumber), node.WorldRule,
            sameWasteland ? current.LocalAnomaly : defaultLocalAnomaly, node);
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

    private StageProfileData ProfileFor(int sector) => stageProfiles[Mathf.Min(sector, stageProfiles.Length) - 1];

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
