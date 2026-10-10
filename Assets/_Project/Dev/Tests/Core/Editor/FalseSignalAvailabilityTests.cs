#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class FalseSignalAvailabilityTests
{
    [Test] public void CarriedSnapshotRemovesOnlyUnavailableContentAndItsModifiers()
    {
        var root=new GameObject("Snapshot signal fixture"); root.SetActive(false);
        var signal=root.AddComponent<FalseSignalEvent>();
        var disabled=ScriptableObject.CreateInstance<SurfaceSectorContent>();
        var available=ScriptableObject.CreateInstance<SurfaceSectorContent>();
        try
        {
            typeof(WorldEvent).GetField("availableInProduction",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(signal,true);
            disabled.requiredEvent=signal; disabled.experienceMultiplier=1.4f; disabled.goldMultiplier=1.7f;
            disabled.threatGrowthMultiplier=0; disabled.spawnPressureMultiplier=1.6f;
            available.requiredEvent=AssetDatabase.LoadAssetAtPath<OrbitalRelayEvent>("Assets/_Project/prefabs/Environment/WorldEvents/OrbitalRelayEvent.prefab");
            available.experienceMultiplier=1.2f; available.goldMultiplier=1.3f;
            available.threatGrowthMultiplier=1.5f; available.spawnPressureMultiplier=1.1f;
            var basis=new RunConfig("map","sector",new RunConfigParameters{experience=2,gold=3,threatGrowth=4,spawnPressure=5});
            var old=RunContentResolver.Resolve(basis,new[]{("disabled",disabled),("available",available)});
            Assert.That(old.Content.GuaranteedEvents,Has.Count.EqualTo(2)); Assert.That(old.ThreatGrowth,Is.Zero);
            typeof(WorldEvent).GetField("availableInProduction",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(signal,false);
            var filtered=old.ValidateContentAvailability();
            Assert.That(filtered.Content.GuaranteedEvents,Is.EquivalentTo(new[]{available.requiredEvent}));
            Assert.That(filtered.Content.Objectives,Has.Count.EqualTo(1));
            Assert.That(filtered.Experience,Is.EqualTo(2.4f).Within(.0001f)); Assert.That(filtered.Gold,Is.EqualTo(3.9f).Within(.0001f));
            Assert.That(filtered.ThreatGrowth,Is.EqualTo(6)); Assert.That(filtered.SpawnPressure,Is.EqualTo(5.5f).Within(.0001f));
            Assert.That(filtered.ValidateContentAvailability(),Is.SameAs(filtered));
            var resolvedAgain=RunContentResolver.Resolve(filtered,new[]{("available",available)});
            Assert.That(resolvedAgain.Gold,Is.EqualTo(filtered.Gold),"Content is applied once.");
            var onlyDisabled=RunContentResolver.Resolve(basis,new[]{("disabled",disabled)});
            Assert.That(onlyDisabled.Content.Objectives,Is.Empty); Assert.That(onlyDisabled.Gold,Is.EqualTo(basis.Gold));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(disabled); Object.DestroyImmediate(available); }
    }
    [Test] public void ProductionMapDoesNotGuaranteeDisabledSignal()
    {
        var map=AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
        var service=new SurfaceMapService(map,new SurfaceMarkerTests.Storage());
        Assert.That(service.TrySelect("D1"),Is.True);
        Assert.That(service.TryBuildRunConfig(out var config),Is.True);
        Assert.That(config.Content.GuaranteedEvents,Is.Empty);
        Assert.That(config.Content.Objectives,Is.Empty);
    }
    [Test] public void SignalTraceCannotBeAcceptedWhileItsEventIsUnavailable()
    {
        var map=AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
        var catalog=AssetDatabase.LoadAssetAtPath<MissionCatalog>(MissionProductionAuthoring.CatalogPath);
        var service=new MissionService(catalog,map,new MissionServiceTests.Storage());
        Assert.That(service.Accept("MISSION_SIGNAL_TRACE"),Is.False);
    }
    [TestCase(MissionState.Active)]
    [TestCase(MissionState.ObjectiveCompleted)]
    [TestCase(MissionState.ReadyToTurnIn)]
    public void SavedMissionIsPausedWithoutLosingStateOrProgress(MissionState savedState)
    {
        var map=AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
        var catalog=AssetDatabase.LoadAssetAtPath<MissionCatalog>(MissionProductionAuthoring.CatalogPath);
        var definition=catalog.Missions[0];
        var saved=new MissionSaveState();
        var record=new MissionProgressRecord{missionId=definition.MissionId,state=savedState};
        record.objectiveProgress.Add(new MissionObjectiveProgress{objectiveId=definition.Objectives[0].Id,data="saved-progress"});
        if(savedState!=MissionState.Active)record.committedObjectives.Add(definition.Objectives[0].Id);
        saved.missions.Add(record);
        var storage=new MissionServiceTests.Storage{json=JsonUtility.ToJson(saved)};
        string original=storage.json;
        var service=new MissionService(catalog,map,storage);
        Assert.That(service.IsAvailable(definition.MissionId),Is.False);
        Assert.That(service.GetState(definition.MissionId),Is.EqualTo(savedState));
        Assert.That(service.GetActiveContent("D1"),Is.Empty);
        Assert.That(service.TryGetMarker("D1",out var marker,out _),Is.True);
        Assert.That(marker.Status,Is.EqualTo(SurfaceSectorContent.UnavailableStatusKey));
        service.Observe(new MissionObjectiveSignal(map.Find("D1").BuildRunConfig(map.Id),1,"CompleteEvent",definition.Objectives[0].RunContent.requiredEvent,"signal"));
        service.FinishRun(map.Find("D1").BuildRunConfig(map.Id),1,RunEndReason.Victory,true);
        service.ArriveAtBunker(); Assert.That(service.Claim(definition.MissionId),Is.False);
        Assert.That(storage.json,Is.EqualTo(original));
        var reloaded=new MissionService(catalog,map,storage);
        Assert.That(reloaded.GetState(definition.MissionId),Is.EqualTo(savedState));
        Assert.That(storage.gold,Is.Zero);
    }
    [Test] public void AllAuthoredSectorsAndSavedAssignmentsExcludeSignal()
    {
        var map=AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
        var content=AssetDatabase.LoadAssetAtPath<SurfaceSectorContent>("Assets/_Project/Data/SurfaceMap/Content_SignalMission.asset");
        var saved=new SurfaceContentState();
        saved.missions.Add(new SurfaceMissionRecord{missionId="old-signal",targetSectorId="D2",contentId=content.id});
        var storage=new SurfaceMarkerTests.Storage(); storage.values[map.Id+":content"]=JsonUtility.ToJson(saved);
        var service=new SurfaceContentService(map,storage);
        Assert.That(service.AssignMission("new-signal","D2",content),Is.False);
        foreach(var sector in map.Sectors)
        {
            var config=service.Resolve(sector.BuildRunConfig(map.Id));
            Assert.That(config.Content.GuaranteedEvents,Is.Empty,sector.Id);
            Assert.That(config.Content.Objectives,Is.Empty,sector.Id);
            Assert.That(config.Gold,Is.EqualTo(sector.BuildRunConfig(map.Id).Gold),sector.Id);
        }
        Assert.That(service.GetMissionState("old-signal"),Is.EqualTo(SurfaceMissionState.Active));
        Assert.That(service.TryGetMarker("D2",out var marker),Is.True);
        Assert.That(marker.Status,Is.EqualTo(SurfaceSectorContent.UnavailableStatusKey));
    }
}
#endif
