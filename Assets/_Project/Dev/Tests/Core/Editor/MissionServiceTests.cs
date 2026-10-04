#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
public sealed class MissionServiceTests
{
    public sealed class Storage:IMissionStorage
    {
        public string json; public int gold,claims; public readonly System.Collections.Generic.HashSet<string> receipts=new();
        public string Load()=>json;
        public void Save(string value)=>json=value;
        public bool HasClaimReceipt(string id)=>receipts.Contains(id);
        public bool TryClaimGold(string id,int amount,string value)
        { if(!receipts.Add(id))return false; gold+=amount; claims++; json=value; return true; }
    }
    const string Id="MISSION_SIGNAL_TRACE";
    static SurfaceMapDefinition Map => AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
    static MissionCatalog Catalog => AssetDatabase.LoadAssetAtPath<MissionCatalog>(MissionProductionAuthoring.CatalogPath);
    static (MissionService mission,SurfaceMapService map,Storage storage,RunConfig config) Begin()
    {
        var storage=new Storage(); var map=new SurfaceMapService(Map,new SurfaceMarkerTests.Storage());
        var mission=new MissionService(Catalog,Map,storage); map.Content.RegisterSource(mission);
        Assert.That(mission.GetState(Id),Is.EqualTo(MissionState.Available)); Assert.That(mission.Accept(Id),Is.True); Assert.That(mission.Accept(Id),Is.False);
        map.TrySelect("D1"); Assert.That(map.TryBuildRunConfig(out var config),Is.True); return(mission,map,storage,config);
    }
    static void Complete(MissionService mission,RunConfig config,int runId=1) => mission.Observe(new MissionObjectiveSignal(config,runId,"CompleteEvent",config.Content.GuaranteedEvents[0],"signal"));
    [Test] public void OperatorArrowIsVisibleInEditModeAndUsesStandardPrefab()
    {
        var provider=AssetDatabase.LoadAssetAtPath<GameObject>(MissionProductionAuthoring.ProviderPath);
        var arrow=provider.transform.Find("PF_InteractionArrow");
        Assert.That(arrow,Is.Not.Null);
        Assert.That(arrow.gameObject.activeSelf,Is.True,"Standard arrow should be authored visible in Edit Mode.");
        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(arrow.gameObject),Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Bunker/PF_InteractionArrow.prefab")));
    }
    [Test] public void PrioritySourceDeduplicatesEventAndPreservesUnknown()
    {
        var flow=Begin(); Assert.That(flow.config.Content.GuaranteedEvents.Count,Is.EqualTo(1));
        Assert.That(flow.config.Content.Objectives.Count,Is.EqualTo(2)); Assert.That(flow.config.Gold,Is.EqualTo(Map.Find("D1").BuildRunConfig(Map.Id).Gold));
        flow.map.Content.TryGetMarker("D1",out var marker); Assert.That(marker.Marker.icon,Is.EqualTo("!")); Assert.That(marker.Status,Is.EqualTo("ACTIVE MISSION")); Assert.That(marker.Reward,Is.EqualTo("100 GOLD"));
        flow.map.Content.UnregisterSource(flow.mission); flow.map.Content.TryGetMarker("D1",out marker); Assert.That(marker.Marker.icon,Is.EqualTo("?"));
    }
    [TestCase(RunEndReason.PlayerDied,false)]
    [TestCase(RunEndReason.ReturnedToBunker,false)]
    [TestCase(RunEndReason.Victory,false)]
    public void FailureDiscardsProvisionalCompletion(RunEndReason reason,bool victory)
    {
        var flow=Begin(); Complete(flow.mission,flow.config);
        Assert.That(flow.mission.GetState(Id),Is.EqualTo(MissionState.ObjectiveCompleted)); Assert.That(flow.mission.Claim(Id),Is.False);
        var saved=JsonUtility.FromJson<MissionSaveState>(flow.storage.json).missions[0];
        Assert.That(saved.state,Is.EqualTo(MissionState.Active)); Assert.That(saved.committedObjectives,Is.Empty);
        flow.mission.FinishRun(flow.config,1,reason,victory); flow.mission.ArriveAtBunker();
        Assert.That(flow.mission.GetState(Id),Is.EqualTo(MissionState.Active)); Assert.That(flow.storage.gold,Is.Zero);
        Assert.That(new MissionService(Catalog,Map,flow.storage).GetState(Id),Is.EqualTo(MissionState.Active));
    }
    [Test] public void VictoryCommitsButTurnInRequiresArrivalAndPaysExactlyOnceAcrossReload()
    {
        var flow=Begin(); Complete(flow.mission,flow.config); flow.mission.FinishRun(flow.config,1,RunEndReason.Victory,true);
        Assert.That(flow.mission.GetState(Id),Is.EqualTo(MissionState.ObjectiveCompleted)); Assert.That(flow.mission.Claim(Id),Is.False);
        var service=new MissionService(Catalog,Map,flow.storage); service.ArriveAtBunker();
        Assert.That(service.GetState(Id),Is.EqualTo(MissionState.ReadyToTurnIn)); Assert.That(service.Claim(Id),Is.True);
        Assert.That(flow.storage.gold,Is.EqualTo(100)); Assert.That(service.Claim(Id),Is.False);
        var reloaded=new MissionService(Catalog,Map,flow.storage); Assert.That(reloaded.GetState(Id),Is.EqualTo(MissionState.Completed)); Assert.That(reloaded.IsRewardClaimed(Id),Is.True);
        Assert.That(reloaded.Claim(Id),Is.False); Assert.That(flow.storage.claims,Is.EqualTo(1));
    }
    [Test] public void WrongSectorSignalAndRunCannotCommit()
    {
        var flow=Begin();
        flow.map.TrySelect("C1"); flow.map.TryBuildRunConfig(out var wrong);
        flow.mission.Observe(new MissionObjectiveSignal(wrong,1,"CompleteEvent",flow.config.Content.GuaranteedEvents[0],"signal"));
        flow.mission.Observe(new MissionObjectiveSignal(flow.config,1,"CompleteEvent",flow.config.Content.GuaranteedEvents[0],"wrong-tag"));
        flow.mission.Observe(new MissionObjectiveSignal(flow.config,1,"CompleteEvent",null,"signal"));
        flow.mission.FinishRun(flow.config,1,RunEndReason.Victory,true); flow.mission.ArriveAtBunker(); Assert.That(flow.mission.GetState(Id),Is.EqualTo(MissionState.Active));
        Complete(flow.mission,flow.config,2); flow.mission.FinishRun(flow.config,3,RunEndReason.Victory,true);
        flow.mission.ArriveAtBunker(); Assert.That(flow.mission.GetState(Id),Is.EqualTo(MissionState.Active));
    }
    [Test] public void ObjectiveHandlersRestoreCommittedPartialProgressWithoutServiceChanges()
    {
        var mission=Object.Instantiate(AssetDatabase.LoadAssetAtPath<MissionDefinition>(MissionProductionAuthoring.MissionPath));
        var objective=ScriptableObject.CreateInstance<MissionProbeObjective>(); var catalog=ScriptableObject.CreateInstance<MissionCatalog>();
        try
        {
            var o=new SerializedObject(objective); o.FindProperty("id").stringValue="probe"; o.ApplyModifiedPropertiesWithoutUndo();
            var m=new SerializedObject(mission); m.FindProperty("objectives").arraySize=1; m.FindProperty("objectives").GetArrayElementAtIndex(0).objectReferenceValue=objective; m.ApplyModifiedPropertiesWithoutUndo();
            var c=new SerializedObject(catalog); c.FindProperty("missions").arraySize=1; c.FindProperty("missions").GetArrayElementAtIndex(0).objectReferenceValue=mission; c.ApplyModifiedPropertiesWithoutUndo();
            var storage=new Storage(); var service=new MissionService(catalog,Map,storage); Assert.That(service.Accept(Id),Is.True);
            var config=Map.Find("D1").BuildRunConfig(Map.Id);
            service.Observe(new MissionObjectiveSignal(config,1,"Probe",null)); service.FinishRun(config,1,RunEndReason.Victory,true);
            Assert.That(service.GetState(Id),Is.EqualTo(MissionState.Active));
            var reloaded=new MissionService(catalog,Map,storage); reloaded.Observe(new MissionObjectiveSignal(config,2,"Probe",null));
            Assert.That(reloaded.GetState(Id),Is.EqualTo(MissionState.ObjectiveCompleted));
            reloaded.FinishRun(config,2,RunEndReason.PlayerDied,false); Assert.That(reloaded.GetState(Id),Is.EqualTo(MissionState.Active));
            reloaded.Observe(new MissionObjectiveSignal(config,3,"Probe",null)); reloaded.FinishRun(config,3,RunEndReason.Victory,true); reloaded.ArriveAtBunker();
            Assert.That(reloaded.GetState(Id),Is.EqualTo(MissionState.ReadyToTurnIn));
        }
        finally { Object.DestroyImmediate(mission); Object.DestroyImmediate(objective); Object.DestroyImmediate(catalog); }
    }
}
#endif
