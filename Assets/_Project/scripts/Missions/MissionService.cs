using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Meta mission lifecycle. All objective-type behavior is delegated to objective definitions.</summary>
public sealed class MissionService : ISurfaceContentSource
{
    public event Action Changed;
    private readonly MissionCatalog catalog;
    private readonly SurfaceMapDefinition map;
    private readonly IMissionStorage storage;
    private readonly Dictionary<string,MissionDefinition> definitions=new(StringComparer.Ordinal);
    private readonly MissionSaveState state;
    private readonly Dictionary<string,IMissionObjectiveHandler> provisional=new(StringComparer.Ordinal);
    private int pendingRunId=-1;
    private bool claiming;
    public MissionCatalog Catalog => catalog;

    public MissionService(MissionCatalog catalog,SurfaceMapDefinition map,IMissionStorage storage)
    {
        this.catalog=catalog!=null?catalog:throw new ArgumentNullException(nameof(catalog));
        this.map=map!=null?map:throw new ArgumentNullException(nameof(map));
        this.storage=storage??throw new ArgumentNullException(nameof(storage));
        foreach(var definition in catalog.Missions)
        {
            if(definition==null||string.IsNullOrWhiteSpace(definition.MissionId)||definitions.ContainsKey(definition.MissionId)||
                map.Find(definition.TargetSectorId)==null||definition.Reward==null||definition.Presentation?.marker==null||definition.Objectives.Count==0)
                throw new ArgumentException("Mission catalog has invalid identity, sector, reward or presentation.");
            var objectiveIds=new HashSet<string>(StringComparer.Ordinal);
            foreach(var objective in definition.Objectives)
                if(objective==null||!objective.IsValid||!objectiveIds.Add(objective.Id))throw new ArgumentException("Mission objectives must be valid and uniquely identified: "+definition.MissionId);
            definitions.Add(definition.MissionId,definition);
        }
        try { string json=storage.Load(); state=string.IsNullOrEmpty(json)?new():JsonUtility.FromJson<MissionSaveState>(json)??new(); }
        catch(ArgumentException) { state=new(); }
        if(state.version!=1)throw new ArgumentException("Unsupported mission storage version: "+state.version);
        state.missions??=new();
        foreach(var record in state.missions)if(record!=null) { record.committedObjectives??=new(); record.objectiveProgress??=new(); }
        foreach(var definition in catalog.Missions)
            if(storage.HasClaimReceipt(definition.MissionId))
            {
                var record=Record(definition.MissionId);
                if(record==null) { record=new MissionProgressRecord{missionId=definition.MissionId}; state.missions.Add(record); }
                record.state=MissionState.Completed; record.rewardClaimed=true;
            }
    }
    public MissionDefinition Find(string id) => id!=null&&definitions.TryGetValue(id,out var definition)?definition:null;
    private MissionProgressRecord Record(string id) => state.missions.Find(r=>r!=null&&r.missionId==id);
    public MissionState GetState(string id)
    {
        var record=Record(id); if(record==null)return MissionState.Available;
        if(record.state==MissionState.Active&&AllObjectives(Find(id),record,true))return MissionState.ObjectiveCompleted;
        return record.state;
    }
    public bool IsRewardClaimed(string id) => Record(id)?.rewardClaimed==true||storage.HasClaimReceipt(id);
    public static string ObjectiveKey(string missionId,string objectiveId) => "mission:"+missionId+":"+objectiveId;
    private bool AllObjectives(MissionDefinition definition,MissionProgressRecord record,bool includeProvisional)
    {
        if(definition==null)return false;
        foreach(var objective in definition.Objectives)
            if(!record.committedObjectives.Contains(objective.Id)&&(!includeProvisional||!provisional.TryGetValue(ObjectiveKey(definition.MissionId,objective.Id),out var handler)||!handler.IsCompleted))return false;
        return true;
    }
    public bool Accept(string missionId)
    {
        if(Find(missionId)==null||Record(missionId)!=null||storage.HasClaimReceipt(missionId))return false;
        state.missions.Add(new MissionProgressRecord{missionId=missionId,state=MissionState.Active});
        Save(); Changed?.Invoke(); return true;
    }
    public IEnumerable<(string objectiveId,SurfaceSectorContent content)> GetActiveContent(string sectorId)
    {
        foreach(var definition in catalog.Missions)
        {
            var record=Record(definition.MissionId);
            if(definition.TargetSectorId!=sectorId||record?.state!=MissionState.Active)continue;
            foreach(var objective in definition.Objectives)
                if(!record.committedObjectives.Contains(objective.Id)&&objective.RunContent!=null)
                    yield return (ObjectiveKey(definition.MissionId,objective.Id),objective.RunContent);
        }
    }
    public bool TryGetMarker(string sectorId,out SurfaceMarkerPresentation presentation,out int priority)
    {
        MissionDefinition selected=null;
        foreach(var definition in catalog.Missions)
        {
            var record=Record(definition.MissionId);
            if(definition.TargetSectorId!=sectorId||record==null||record.state==MissionState.Completed)continue;
            if(selected==null||definition.MarkerPriority>selected.MarkerPriority||definition.MarkerPriority==selected.MarkerPriority&&string.CompareOrdinal(definition.MissionId,selected.MissionId)<0)selected=definition;
        }
        if(selected==null) { presentation=default; priority=0; return false; }
        var current=GetState(selected.MissionId);
        string status=current==MissionState.ReadyToTurnIn?"READY TO TURN IN":current==MissionState.ObjectiveCompleted?"OBJECTIVE COMPLETE / EXTRACT":"ACTIVE MISSION";
        var objectiveLines=new List<string>(); foreach(var objective in selected.Objectives)objectiveLines.Add(objective.Description);
        presentation=new SurfaceMarkerPresentation(selected.Presentation,SurfaceMarkerState.Active,status,string.Join("\n",objectiveLines),selected.Reward.Gold+" GOLD",selected.Title,
            current==MissionState.ReadyToTurnIn?"Return to the mission provider.":selected.Description);
        priority=selected.MarkerPriority; return true;
    }
    public void Observe(MissionObjectiveSignal signal)
    {
        if(signal.Config==null||signal.Config.MapId!=map.Id)return;
        if(pendingRunId!=signal.RunId) { provisional.Clear(); pendingRunId=signal.RunId; }
        bool changed=false;
        foreach(var definition in catalog.Missions)
        {
            var record=Record(definition.MissionId);
            if(record?.state!=MissionState.Active||definition.TargetSectorId!=signal.Config.SectorId)continue;
            foreach(var objective in definition.Objectives)
            {
                string key=ObjectiveKey(definition.MissionId,objective.Id);
                bool bound=objective.RunContent==null;
                foreach(var resolved in signal.Config.Content.Objectives)if(resolved.Id==key) { bound=true; break; }
                if(!bound||record.committedObjectives.Contains(objective.Id))continue;
                if(!provisional.TryGetValue(key,out var handler))
                {
                    handler=objective.CreateHandler()??throw new InvalidOperationException("Objective did not create a runtime handler: "+objective.Id);
                    handler.RestoreProgress(record.objectiveProgress.Find(p=>p.objectiveId==objective.Id)?.data);
                    provisional.Add(key,handler);
                }
                changed|=handler.Observe(signal);
            }
        }
        if(changed)Changed?.Invoke(); // Provisional state deliberately never reaches persistence.
    }
    public void FinishRun(RunConfig config,int runId,RunEndReason reason,bool confirmedProductionVictory)
    {
        bool changed=false;
        if(config!=null&&config.MapId==map.Id&&pendingRunId==runId&&reason==RunEndReason.Victory&&confirmedProductionVictory)
            foreach(var definition in catalog.Missions)
            {
                var record=Record(definition.MissionId);
                if(record?.state!=MissionState.Active||definition.TargetSectorId!=config.SectorId)continue;
                foreach(var objective in definition.Objectives)
                    if(provisional.TryGetValue(ObjectiveKey(definition.MissionId,objective.Id),out var handler))
                    {
                        string data=handler.CaptureProgress();
                        var progress=record.objectiveProgress.Find(p=>p.objectiveId==objective.Id);
                        if(progress==null) { progress=new MissionObjectiveProgress{objectiveId=objective.Id}; record.objectiveProgress.Add(progress); }
                        if(progress.data!=data) { progress.data=data; changed=true; }
                        if(handler.IsCompleted&&!record.committedObjectives.Contains(objective.Id)) { record.committedObjectives.Add(objective.Id); changed=true; }
                    }
                if(AllObjectives(definition,record,false))record.state=MissionState.ObjectiveCompleted;
            }
        provisional.Clear(); pendingRunId=-1;
        if(changed)Save(); Changed?.Invoke();
    }
    public void ArriveAtBunker()
    {
        bool changed=false;
        foreach(var record in state.missions)
            if(record!=null&&record.state==MissionState.ObjectiveCompleted&&AllObjectives(Find(record.missionId),record,false))
            { record.state=MissionState.ReadyToTurnIn; changed=true; }
        if(changed) { Save(); Changed?.Invoke(); }
    }
    public bool Claim(string missionId)
    {
        var record=Record(missionId); var definition=Find(missionId);
        if(claiming||definition==null||record?.state!=MissionState.ReadyToTurnIn||record.rewardClaimed||!AllObjectives(definition,record,false))return false;
        claiming=true;
        try
        {
            record.state=MissionState.Completed; record.rewardClaimed=true;
            if(!storage.TryClaimGold(missionId,definition.Reward.Gold,JsonUtility.ToJson(state)))
            {
                if(!storage.HasClaimReceipt(missionId)) { record.state=MissionState.ReadyToTurnIn; record.rewardClaimed=false; }
                else Save();
                Changed?.Invoke(); return false;
            }
            Changed?.Invoke(); return true;
        }
        finally { claiming=false; }
    }
    private void Save() => storage.Save(JsonUtility.ToJson(state));
}
