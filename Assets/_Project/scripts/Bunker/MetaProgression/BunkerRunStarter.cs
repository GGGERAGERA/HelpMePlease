using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public sealed class BunkerRunStarter : MonoBehaviour
{
    [SerializeField] private DepthCatalog depths;
    public DepthCatalog Depths => depths;

    [Header("Starting Sector")]
    [SerializeField] private WorldRuleData startingWorldRule;
    [SerializeField] private LocalAnomalyData startingLocalAnomaly;
    [SerializeField] private LevelChoiceManager routeSource;

    [Header("Run Transition")]
    [SerializeField] private Camera transitionCamera;
    [SerializeField] private Transform cameraRig;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private Behaviour playerMovement;
    [SerializeField] private BunkerCursorInteractor bunkerCursor;
    [SerializeField] private BunkerGateVisual runGate;
    [SerializeField, Min(0.5f)] private float targetOrthographicSize = 3.5f;
    [SerializeField, Range(0f, 1f)] private float gateOpenNormalizedTime = 0.35f;

    private bool isTransitioning;
    private RunRouteGraph surfaceGraph;
    private RunRouteGraph escapeGraph;

    public bool IsTransitioning => isTransitioning;

    private BunkerNotificationManager Notifications =>
    BunkerContext.Instance != null ? BunkerContext.Instance.Notifications : null;

    public SurfaceMapService SurfaceMap => MetaProgressionManager.EnsureExists().SurfaceMap;

    internal SurfaceSectorDefinition EscapeRoute
    {
        get
        {
            var meta = MetaProgressionManager.EnsureExists();
            var sector = meta.EscapeProtocol?.FinalSector;
            return sector != null && sector.FinalChallengeReady && meta.SurfaceMap != null &&
                meta.SurfaceMap.GetStatus(sector.Id) != SurfaceSectorStatus.Locked ? sector : null;
        }
    }

    internal RunRouteGraph GetEntryGraph(bool escape)
    {
        var map = SurfaceMap;
        if (routeSource == null || map == null) return null;
        if (escape)
        {
            var expedition = EscapeRoute;
            return expedition == null ? null : escapeGraph ??= routeSource.CreateEntryGraph(new[] { expedition }, startingWorldRule);
        }

        var expeditions = new List<SurfaceSectorDefinition>();
        foreach (string startId in map.Definition.StartingSectors)
        {
            var expedition = map.Definition.Find(startId);
            if (expedition == null || map.GetStatus(startId) == SurfaceSectorStatus.Locked) continue;
            // An unlocked successor replaces the earlier preset in the same entrance lane.
            foreach (string successorId in expedition.Neighbours)
            {
                if (map.GetStatus(successorId) == SurfaceSectorStatus.Locked) continue;
                expedition = map.Definition.Find(successorId);
                break;
            }
            expeditions.Add(expedition);
            if (expeditions.Count == 3) break;
        }
        if (expeditions.Count == 0) return null;
        if (surfaceGraph == null || !HasSameEntries(surfaceGraph, expeditions))
            surfaceGraph = routeSource.CreateEntryGraph(expeditions, startingWorldRule);
        return surfaceGraph;
    }

    private static bool HasSameEntries(RunRouteGraph graph, List<SurfaceSectorDefinition> expeditions)
    {
        if (graph.Entries.Count != expeditions.Count) return false;
        for (int i = 0; i < expeditions.Count; i++)
            if (graph.Entries[i].Expedition != expeditions[i]) return false;
        return true;
    }

    internal void StartRoute(Transform target, RunRouteGraph graph, RouteNode entry)
    {
        if (graph == null || graph != surfaceGraph && graph != escapeGraph || !graph.ContainsEntry(entry) ||
            !SurfaceMap.TryBuildRunConfig(out var config) || config.SectorId != entry.Expedition.Id) return;
        StartRunInternal(target, DepthCatalog.SurfaceId, config, graph, entry);
    }

    public void StartSurfaceRun(Transform transitionTarget)
    {
        if (SurfaceMap == null || !SurfaceMap.TryBuildRunConfig(out RunConfig config)) return;
        StartRun(transitionTarget, DepthCatalog.SurfaceId, config);
    }

    public void StartRun(Transform transitionTarget, int depthId = DepthCatalog.SurfaceId, RunConfig config = null) =>
        StartRunInternal(transitionTarget, depthId, config, null, null);

    private void StartRunInternal(Transform transitionTarget, int depthId, RunConfig config, RunRouteGraph graph, RouteNode entry)
    {
        if (isTransitioning || SceneTransitionOverlay.IsTransitioning)
            return;

        if (config?.IsEscapeFinal == true && (SurfaceMap == null || config.MapId != SurfaceMap.Definition.Id ||
            SurfaceMap.GetStatus(config.SectorId) == SurfaceSectorStatus.Locked ||
            SurfaceMap.Definition.Find(config.SectorId) is not { IsEscapeFinal: true, FinalChallengeReady: true })) return;

        DepthCatalog.Entry depth = depths.Find(depthId);
        if (depth == null || depth.GetAvailability(MetaProgressionManager.EnsureExists().EscapeAccess) != DepthAvailability.Available)
            return;
        string gameplaySceneName = depth.gameplaySceneName;
        StageProfileData startingStageProfile = depth.startingStageProfile;

        if (!TryValidateRun(startingStageProfile, out CharacterData character))
            return;

        if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            Debug.LogError($"[BunkerRunStarter] Scene '{gameplaySceneName}' is not available.", this);
            return;
        }

        if (transitionTarget == null ||
            transitionCamera == null ||
            cameraRig == null ||
            cameraFollow == null ||
            runGate == null)
        {
            Debug.LogError(
                "[BunkerRunStarter] Run transition references are incomplete.",
                this);
            Notifications?.ShowError("bunker.run_transition_unavailable");
            return;
        }

        Vector3 startPosition = cameraRig.position;
        Vector3 targetPosition = transitionTarget.position;
        targetPosition.z = startPosition.z;
        float startSize = transitionCamera.orthographicSize;
        bool gateOpened = false;
        SceneTransitionOverlay.Load(gameplaySceneName, () =>
        {
            isTransitioning = true;
            BunkerContext.Instance?.Panels?.CloseAll(false);
            AnomalyStabilizerData stabilizer = RunSelectionManager.Instance.ConsumeAnomalyStabilizer();
            RunStateManager.EnsureExists().BeginNewRun(character, null, startingStageProfile,
                config?.WorldRule ?? startingWorldRule, config?.LocalAnomaly ?? startingLocalAnomaly,
                stabilizer, depth.id, false, config, graph, entry);
            AudioService.Instance?.Play(AudioCueId.StartRun);
        }, t =>
        {
            if (this == null || cameraFollow == null) return;
            cameraFollow.enabled = false;
            if (!gateOpened && t >= gateOpenNormalizedTime) { runGate.Open(); gateOpened = true; }
            cameraRig.position = Vector3.LerpUnclamped(startPosition, targetPosition, t);
            transitionCamera.orthographicSize = Mathf.Lerp(startSize, targetOrthographicSize, t);
        });
    }

    private bool TryValidateRun(StageProfileData startingStageProfile, out CharacterData character)
    {
        character = null;

        if (RunSelectionManager.Instance == null)
        {
            Notifications?.ShowError("bunker.run_selection_unavailable");
            return false;
        }

        if (RunSelectionManager.Instance.SelectedCharacter == null)
        {
            Notifications?.ShowWarning("bunker.run_choose_subject");
            return false;
        }

        character = RunSelectionManager.Instance.SelectedCharacter;

        if (startingStageProfile == null ||
            startingWorldRule == null ||
            startingLocalAnomaly == null)
        {
            Debug.LogError(
                "[BunkerRunStarter] Starting sector configuration is incomplete.",
                this
            );
            return false;
        }

        if (startingStageProfile.SectorNumber != 1)
        {
            Debug.LogError(
                "[BunkerRunStarter] Starting StageProfile must be sector 1.",
                this
            );
            return false;
        }

        return true;
    }

}
