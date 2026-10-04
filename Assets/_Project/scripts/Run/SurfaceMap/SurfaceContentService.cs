using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SurfaceMissionRecord
{
    public string missionId, targetSectorId, contentId, requiredEventId, requiredEventTag;
    public SurfaceMissionState missionState;
    public SurfaceMarkerState markerState;
    public float rewardExperience, rewardGold;
}
[Serializable] public sealed class SurfaceContentState { public List<SurfaceMissionRecord> missions = new(); }
public readonly struct SurfaceMarkerPresentation
{
    public readonly SurfaceMarker Marker;
    public readonly SurfaceMarkerState State;
    public readonly string Title, Description;
    public SurfaceMarkerPresentation(SurfaceSectorContent content, SurfaceMarkerState state)
    { Marker = content.marker; State = state; Title = content.title; Description = content.description; }
    public bool Concealed => Marker != null && Marker.concealDetails && State == SurfaceMarkerState.Active;
}

// Dynamic mission/content state is independent of frontier progression and immutable definitions.
public sealed class SurfaceContentService
{
    public event Action Changed;
    private readonly SurfaceMapDefinition map;
    private readonly ISurfaceMapStorage storage;
    private readonly SurfaceContentState state;
    private readonly HashSet<string> pending = new();
    private int pendingRunId = -1;
    private string StorageId => map.Id + ":content";
    public SurfaceContentService(SurfaceMapDefinition map, ISurfaceMapStorage storage)
    {
        this.map = map; this.storage = storage;
        try { string json = storage.Load(StorageId); state = string.IsNullOrEmpty(json) ? new() : JsonUtility.FromJson<SurfaceContentState>(json) ?? new(); }
        catch (ArgumentException) { state = new(); }
        state.missions ??= new();
        state.missions.RemoveAll(r => r == null || map.Find(r.targetSectorId) == null || FindContent(r.contentId) == null);
        foreach (var sector in map.Sectors)
            foreach (var c in sector.Content)
                if (c != null && !string.IsNullOrWhiteSpace(c.id)) AddRecord("sector:" + sector.Id + ":" + c.id, sector.Id, c);
    }
    private SurfaceSectorContent FindContent(string id)
    {
        foreach (var c in map.ContentCatalog) if (c != null && c.id == id) return c;
        foreach (var sector in map.Sectors) foreach (var c in sector.Content) if (c != null && c.id == id) return c;
        return null;
    }
    private bool AddRecord(string id, string sectorId, SurfaceSectorContent c)
    {
        if (state.missions.Exists(r => r.missionId == id)) return false;
        state.missions.Add(new SurfaceMissionRecord { missionId = id, targetSectorId = sectorId, contentId = c.id,
            requiredEventId = c.requiredEvent != null ? c.requiredEvent.EventId : "", requiredEventTag = c.requiredEventTag,
            rewardExperience = c.experienceMultiplier, rewardGold = c.goldMultiplier }); return true;
    }
    public bool AssignMission(string missionId, string targetSectorId, SurfaceSectorContent content)
    {
        if (string.IsNullOrWhiteSpace(missionId) || map.Find(targetSectorId) == null || content == null || FindContent(content.id) != content ||
            !AddRecord(missionId, targetSectorId, content)) return false;
        Save(); Changed?.Invoke(); return true;
    }
    public SurfaceMissionState? GetMissionState(string missionId) => state.missions.Find(r => r.missionId == missionId)?.missionState;
    public bool TryGetMarker(string sectorId, out SurfaceMarkerPresentation presentation)
    {
        // Active assigned missions take precedence over completed/revealed static content.
        SurfaceMissionRecord selected = null;
        foreach (var r in state.missions)
            if (r.targetSectorId == sectorId && (selected == null || r.missionState == SurfaceMissionState.Active)) selected = r;
        if (selected == null) { presentation = default; return false; }
        presentation = new SurfaceMarkerPresentation(FindContent(selected.contentId), selected.markerState); return true;
    }
    public RunConfig Resolve(RunConfig basis)
    {
        var active = new List<(string, SurfaceSectorContent)>();
        foreach (var r in state.missions)
            if (r.targetSectorId == basis.SectorId && r.missionState == SurfaceMissionState.Active)
                active.Add((r.missionId, FindContent(r.contentId)));
        return RunContentResolver.Resolve(basis, active);
    }
    public void ObserveEvent(RunConfig config, int runId, WorldEvent source, string tag)
    {
        if (config == null || config.MapId != map.Id) return;
        if (pendingRunId != runId) { pending.Clear(); pendingRunId = runId; }
        foreach (var objective in config.Content.Objectives)
            if (objective.Matches(source, tag)) pending.Add(objective.Id);
    }
    public bool FinishRun(RunConfig config, int runId, RunEndReason reason, bool confirmedProductionVictory)
    {
        bool changed = false;
        if (config != null && config.MapId == map.Id && pendingRunId == runId && confirmedProductionVictory && reason == RunEndReason.Victory)
            foreach (var objective in config.Content.Objectives)
            {
                var r = state.missions.Find(r => r.missionId == objective.Id && r.targetSectorId == config.SectorId);
                if (r == null || r.missionState != SurfaceMissionState.Active || !pending.Contains(objective.Id)) continue;
                r.missionState = SurfaceMissionState.Completed; r.markerState = FindContent(r.contentId).completionState; changed = true;
            }
        pending.Clear(); pendingRunId = -1;
        if (changed) { Save(); Changed?.Invoke(); } return changed;
    }
    private void Save() => storage.Save(StorageId, JsonUtility.ToJson(state));
}
