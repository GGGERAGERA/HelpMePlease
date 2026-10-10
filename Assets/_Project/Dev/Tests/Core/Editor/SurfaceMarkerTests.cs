#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
public sealed class SurfaceMarkerTests
{
    public sealed class Storage : ISurfaceMapStorage
    {
        public readonly Dictionary<string,string> values = new();
        public string Load(string id) => values.TryGetValue(id,out var json) ? json : "";
        public void Save(string id,string json) => values[id] = json;
    }
    static SurfaceMapDefinition Map;
    static SurfaceSectorContent MissionContent;
    readonly List<Object> fixtures=new();
    [SetUp] public void AvailableContentFixture()
    {
        Map=Object.Instantiate(AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath)); fixtures.Add(Map);
        var copies=new Dictionary<SurfaceSectorContent,SurfaceSectorContent>();
        var relay=AssetDatabase.LoadAssetAtPath<OrbitalRelayEvent>("Assets/_Project/prefabs/Environment/WorldEvents/OrbitalRelayEvent.prefab");
        SurfaceSectorContent Copy(SurfaceSectorContent source)
        {
            if(copies.TryGetValue(source,out var copy))return copy;
            copy=Object.Instantiate(source); fixtures.Add(copy); copies.Add(source,copy);
            if(copy.requiredEvent is FalseSignalEvent){copy.requiredEvent=relay;copy.requiredEventTag=relay.EventTag;}
            return copy;
        }
        var catalog=new List<SurfaceSectorContent>(); foreach(var item in Map.ContentCatalog)catalog.Add(Copy(item));
        var sectors=new List<SurfaceSectorDefinition>();
        foreach(var source in Map.Sectors)
        {
            var sector=Object.Instantiate(source); fixtures.Add(sector); sectors.Add(sector);
            var content=new List<SurfaceSectorContent>(); foreach(var item in source.Content)content.Add(Copy(item));
            CorridorMigrationTestSupport.Set(sector,"content",content.ToArray());
        }
        CorridorMigrationTestSupport.Set(Map,"contentCatalog",catalog.ToArray());
        CorridorMigrationTestSupport.Set(Map,"sectors",sectors.ToArray());
        MissionContent=Copy(AssetDatabase.LoadAssetAtPath<SurfaceSectorContent>("Assets/_Project/Data/SurfaceMap/Content_SignalMission.asset"));
    }
    [TearDown] public void ClearFixtures()
    { foreach(var fixture in fixtures)Object.DestroyImmediate(fixture);fixtures.Clear();Map=null;MissionContent=null; }
    [Test] public void UnknownContentResolvesGuaranteedEventAndCommitsOnlyAfterVictory()
    {
        var storage = new Storage(); var service = new SurfaceMapService(Map,storage);
        Assert.That(service.TrySelect("D1"),Is.True); service.TryBuildRunConfig(out var config);
        Assert.That(service.Content.TryGetMarker("D1",out var marker),Is.True);
        Assert.That(marker.Concealed,Is.True); Assert.That(marker.Title,Is.EqualTo("UNKNOWN ACTIVITY"));
        Assert.That(config.Content.GuaranteedEvents.Count,Is.EqualTo(1));
        var required = config.Content.GuaranteedEvents[0]; Assert.That(required,Is.TypeOf<OrbitalRelayEvent>());
        service.Content.ObserveEvent(config,1,required,required.EventTag);
        Assert.That(storage.values.Count,Is.Zero,"Event completion must not persist before run victory");
        Assert.That(service.Content.FinishRun(config,1,RunEndReason.PlayerDied,false),Is.False);
        service.Content.ObserveEvent(config,2,required,required.EventTag);
        Assert.That(service.Content.FinishRun(config,3,RunEndReason.Victory,true),Is.False,"Restart/new run must not reuse pending event");
        service.Content.ObserveEvent(config,4,required,required.EventTag);
        Assert.That(service.Content.FinishRun(config,4,RunEndReason.Victory,false),Is.False,"Unconfirmed/dev victory");
        service.Content.ObserveEvent(config,5,required,required.EventTag);
        Assert.That(service.Content.FinishRun(config,5,RunEndReason.Victory,true),Is.True);
        var reloaded = new SurfaceMapService(Map,storage); reloaded.Content.TryGetMarker("D1",out marker);
        Assert.That(marker.State,Is.EqualTo(SurfaceMarkerState.Revealed)); Assert.That(marker.Concealed,Is.False);
        reloaded.TrySelect("D1"); reloaded.TryBuildRunConfig(out config); Assert.That(config.Content.GuaranteedEvents,Is.Empty);
    }
    [Test] public void MissionCanTargetAnyExistingSectorAndApplyRewards()
    {
        var storage = new Storage(); var service = new SurfaceMapService(Map,storage);
        var content = MissionContent;
        Assert.That(service.Content.AssignMission("npc-signal","D2",content),Is.True);
        Assert.That(service.Content.AssignMission("npc-signal","D2",content),Is.False);
        Assert.That(service.Content.AssignMission("invalid","missing",content),Is.False);
        var config = service.Content.Resolve(Map.Find("D2").BuildRunConfig(Map.Id));
        Assert.That(config.Content.GuaranteedEvents.Count,Is.EqualTo(1));
        Assert.That(config.Gold,Is.EqualTo(1.15f).Within(.001));
        var required = config.Content.GuaranteedEvents[0];
        service.Content.ObserveEvent(config,1,required,"wrong-tag");
        Assert.That(service.Content.FinishRun(config,1,RunEndReason.Victory,true),Is.False);
        service.Content.ObserveEvent(config,2,required,required.EventTag);
        Assert.That(service.Content.FinishRun(config,2,RunEndReason.Victory,true),Is.True);
        Assert.That(new SurfaceMapService(Map,storage).Content.GetMissionState("npc-signal"),Is.EqualTo(SurfaceMissionState.Completed));
        Assert.That(service.GetStatus("D2"),Is.EqualTo(SurfaceSectorStatus.Locked),"Mission assignment does not bypass frontier progression");
    }
    [Test] public void NavigationPansAndZoomsWithinLimits()
    {
        var root = new GameObject("Viewport",typeof(RectTransform),typeof(SurfaceMapNavigation));
        var graph = new GameObject("Content",typeof(RectTransform)); graph.transform.SetParent(root.transform,false);
        try {
            var nav = root.GetComponent<SurfaceMapNavigation>(); var rect = (RectTransform)graph.transform; nav.Configure(rect);
            nav.Pan(new Vector2(100,25)); Assert.That(rect.anchoredPosition,Is.EqualTo(new Vector2(100,25)));
            nav.Zoom(1,Vector2.zero); Assert.That(rect.localScale.x,Is.GreaterThan(1));
            nav.Zoom(100,Vector2.zero); Assert.That(rect.localScale.x,Is.EqualTo(2.2f).Within(.001));
            nav.Zoom(-100,Vector2.zero); Assert.That(rect.localScale.x,Is.EqualTo(.65f).Within(.001));
        } finally { Object.DestroyImmediate(root); }
    }
}
#endif
