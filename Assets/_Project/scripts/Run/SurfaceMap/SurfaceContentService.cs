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
    public readonly string Title, Description, Status, Objective, Reward;
    public SurfaceMarkerPresentation(SurfaceSectorContent content, SurfaceMarkerState state,string status=null,string objective=null,string reward=null,string title=null,string description=null)
    { Marker = content.marker; State = state; Title = title??content.title; Description = description??content.description; Status=status; Objective=objective; Reward=reward; }
    public bool Concealed => Marker != null && Marker.concealDetails && State == SurfaceMarkerState.Active;
}

// Dynamic mission/content state is independent of frontier progression and immutable definitions.
public sealed class SurfaceContentService
{
    public event Action Changed;
    private readonly SurfaceMapDefinition map;
    private readonly ISurfaceMapStorage storage;
    private readonly SurfaceContentState state;
    private readonly List<ISurfaceContentSource> sources=new();
    public void RegisterSource(ISurfaceContentSource source)
    { if(source==null||sources.Contains(source))return; sources.Add(source); source.Changed+=NotifyChanged; Changed?.Invoke(); }
    public void UnregisterSource(ISurfaceContentSource source)
    { if(source!=null&&sources.Remove(source)) { source.Changed-=NotifyChanged; Changed?.Invoke(); } }
    private void NotifyChanged() => Changed?.Invoke();
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
        if (string.IsNullOrWhiteSpace(missionId) || map.Find(targetSectorId) == null || content == null || !content.AvailableInProduction || FindContent(content.id) != content ||
            !AddRecord(missionId, targetSectorId, content)) return false;
        Save(); Changed?.Invoke(); return true;
    }
    public SurfaceMissionState? GetMissionState(string missionId) => state.missions.Find(r => r.missionId == missionId)?.missionState;
    public bool TryGetMarker(string sectorId, out SurfaceMarkerPresentation presentation)
    {
        presentation=default; bool found=false; int best=int.MinValue;
        foreach(var record in state.missions)
        {
            if(record.targetSectorId!=sectorId)continue;
            var content=FindContent(record.contentId); if(content?.marker==null)continue;
            if(!content.AvailableInProduction && record.missionId.StartsWith("sector:",StringComparison.Ordinal))continue;
            int priority=record.missionState==SurfaceMissionState.Active?(record.missionId.StartsWith("sector:",StringComparison.Ordinal)?10:50):0;
            if(priority>best) { presentation=content.AvailableInProduction
                ? new SurfaceMarkerPresentation(content,record.markerState)
                : new SurfaceMarkerPresentation(content,record.markerState,SurfaceSectorContent.UnavailableStatusKey,
                    description:SurfaceSectorContent.UnavailableDescriptionKey); best=priority; found=true; }
        }
        foreach(var source in sources)
            if(source.TryGetMarker(sectorId,out var candidate,out int priority)&&priority>best)
            { presentation=candidate; best=priority; found=true; }
        return found;
    }
    public RunConfig Resolve(RunConfig basis)
    {
        var active = new List<(string, SurfaceSectorContent)>();
        foreach (var r in state.missions)
            if (r.targetSectorId == basis.SectorId && r.missionState == SurfaceMissionState.Active)
                active.Add((r.missionId, FindContent(r.contentId)));
        foreach(var source in sources)active.AddRange(source.GetActiveContent(basis.SectorId));
        return RunContentResolver.Resolve(basis, active);
    }
    public void ObserveEvent(RunConfig config, int runId, WorldEvent source, string tag)
    {
        if (config == null || config.MapId != map.Id) return;
        if (pendingRunId != runId) { pending.Clear(); pendingRunId = runId; }
        foreach (var objective in config.Content.Objectives)
            if ((source == null || source.AvailableInProduction) &&
                (objective.RequiredEvent == null || objective.RequiredEvent.AvailableInProduction) && objective.Matches(source, tag)) pending.Add(objective.Id);
    }
    public bool FinishRun(RunConfig config, int runId, RunEndReason reason, bool confirmedProductionVictory)
    {
        bool changed = false;
        if (config != null && config.MapId == map.Id && pendingRunId == runId && confirmedProductionVictory && reason == RunEndReason.Victory)
            foreach (var objective in config.Content.Objectives)
            {
                var r = state.missions.Find(r => r.missionId == objective.Id && r.targetSectorId == config.SectorId);
                if (r == null || !FindContent(r.contentId).AvailableInProduction || r.missionState != SurfaceMissionState.Active || !pending.Contains(objective.Id)) continue;
                r.missionState = SurfaceMissionState.Completed; r.markerState = FindContent(r.contentId).completionState; changed = true;
            }
        pending.Clear(); pendingRunId = -1;
        if (changed) { Save(); Changed?.Invoke(); } return changed;
    }
    private void Save() => storage.Save(StorageId, JsonUtility.ToJson(state));
}
