#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public sealed class SurfaceMapUxTests
{
    sealed class Storage:ISurfaceMapStorage
    {
        readonly System.Collections.Generic.Dictionary<string,string> values=new();
        public string Load(string id)=>values.TryGetValue(id,out var json)?json:null;
        public void Save(string id,string json)=>values[id]=json;
    }
    static T Read<T>(Object target,string field)=>(T)target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
    [Test] public void InspectionRouteAndViewStateStayInPresentation()
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/SurfaceMap/PF_SurfaceMap.prefab");
        Assert.That(asset.transform.Find("Map Viewport"),Is.Not.Null,"Fixed map shell must be authored before Awake.");
        var root=Object.Instantiate(asset); ((RectTransform)root.transform).sizeDelta=new Vector2(1280,800); var view=root.GetComponent<SurfaceMapView>();
        var previous=LocalizationService.Instance;
        var localization=root.AddComponent<LocalizationService>();
        typeof(LocalizationService).GetField("table",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(localization,AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/_Project/Data/Localization/LocalizationTable.asset"));
        typeof(LocalizationService).GetProperty("Instance").SetValue(null,localization);
        try
        {
            typeof(SurfaceMapView).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(view,null);
            var map=AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
            var storage=new Storage(); var service=new SurfaceMapService(map,storage); service.TrySelect("C1");
            view.Show(service,null,null,null);
            Assert.That(view.FindRoute("C2"),Is.EqualTo(new[]{"C1","C2"}));
            var nodes=Read<System.Collections.Generic.Dictionary<string,Button>>(view,"nodes");
            Assert.That(nodes["C2"].gameObject.activeSelf,Is.False,"Unknown landmarks and hit targets must be hidden, not merely tinted.");
            Assert.That(nodes["D2"].gameObject.activeSelf,Is.False);
            var fog=Read<SurfaceMapFog>(view,"fog");
            Assert.That(fog,Is.Not.Null);
            Assert.That(fog.CoverageAt(nodes["C2"].GetComponent<RectTransform>().anchoredPosition),Is.GreaterThan(.95f),"Unknown land remains fully fogged.");
            Assert.That(fog.CoverageAt(nodes["C1"].GetComponent<RectTransform>().anchoredPosition),Is.Zero);
            var facts=root.transform.Find("Sector card").GetComponentsInChildren<TMP_Text>();
            Assert.That(System.Array.Exists(facts,label=>label.text.Contains("+50%") && label.color==SurfaceMapGlyph.Tint(SurfaceMapMeaning.Reward)),Is.True);
            Assert.That(System.Array.Exists(facts,label=>label.text.StartsWith(localization.Get("map.risk.high")) && label.color==SurfaceMapGlyph.Tint(SurfaceMapMeaning.Danger)),Is.True);
            var frontier=Read<RectTransform>(view,"graph").Find("Trail C1-C2").GetComponent<Image>();
            var hint=frontier.sprite;
            Assert.That(hint.name,Does.Contain("Hint"),"Only a short continuation may enter the unexplored area.");
            view.Inspect("C2"); Assert.That(service.SelectedSectorId,Is.EqualTo("C1"));
            Assert.That(Read<TMP_Text>(view,"selectionLabel").text,Is.EqualTo(localization.Get("map.sector.C1")),"Code and UI clicks cannot reveal unknown sector briefings.");
            view.Inspect("D2");
            Assert.That(Read<Button>(view,"scanButton").gameObject.activeSelf,Is.False);
            Assert.That(Read<TMP_Text>(view,"details").text,Does.Not.Contain(localization.Get("map.event.relay")));
            var icons=Read<System.Collections.Generic.Dictionary<string,SurfaceMapGlyph>>(view,"markerIcons");
            Assert.That(icons["D2"].gameObject.activeInHierarchy,Is.False);
            Assert.That(SurfaceMapBriefing.AnomalyBias(map.Find("C1").BuildRunConfig(map.Id)),Is.Empty,"Legacy starting Berserk is not the site distribution.");
            var stasis=AssetDatabase.LoadAssetAtPath<LocalAnomalyData>("Assets/_Project/Data/Anomalies/LocalAnomaly_Stasis.asset");
            var eventRun=map.Find("D1").BuildRunConfig(map.Id);
            Assert.That(eventRun.AnomalyWeight(stasis),Is.EqualTo(3));
            Assert.That(SurfaceMapBriefing.AnomalyBias(eventRun),Does.Contain(localization.Get("map.anomaly.bias.Stasis")));
            var goldRun=map.Find("C1").BuildRunConfig(map.Id);
            Assert.That(SurfaceMapBriefing.EntryPressure(goldRun),Is.EqualTo(1f).Within(.001),"Golden pressure cancels the authored 1.25 multiplier on entry.");
            Assert.That(SurfaceMapBriefing.EntryHealth(goldRun),Is.EqualTo(1.4375f).Within(.001));
            foreach(string id in new[]{"A1","C1","D1"})
            {
                view.Inspect(id);
                Assert.That(nodes[id].transform.Find("Danger").gameObject.activeSelf,Is.True);
                Assert.That(nodes[id].transform.Find("Reward").gameObject.activeSelf,Is.True,"XP bonuses use the same reward indicator as gold bonuses.");
                var values=root.transform.Find("Sector card").GetComponentsInChildren<TMP_Text>();
                Assert.That(System.Array.Exists(values,label=>label.text.StartsWith(localization.Get("map.risk"),System.StringComparison.OrdinalIgnoreCase) && label.color==SurfaceMapGlyph.Tint(SurfaceMapMeaning.Danger)),Is.True);
            }
            view.Inspect("C1");
            Assert.That(SurfaceMapBriefing.RiskDrivers(goldRun),Does.Not.Contain("15"));
            Assert.That(SurfaceMapBriefing.RiskDrivers(goldRun).Split('\n').Length,Is.LessThanOrEqualTo(2));
            var nav=Read<SurfaceMapNavigation>(view,"navigation"); nav.Zoom(2,Vector2.zero); nav.Pan(new Vector2(80,30));
            var position=nav.Content.anchoredPosition; var zoom=nav.Content.localScale;
            root.SetActive(false); view.Show(service,null,null,null);
            Assert.That(nav.Content.anchoredPosition,Is.EqualTo(position)); Assert.That(nav.Content.localScale,Is.EqualTo(zoom));
            Assert.That(Read<TMP_Text>(view,"selectionLabel").text,Is.EqualTo(localization.Get("map.sector.C1")));
            nav.Pan(Vector2.one*100000); Assert.That(nav.Content.anchoredPosition.magnitude,Is.LessThan(2000));
            nav.Zoom(100,Vector2.zero); Assert.That(nav.Content.localScale.x,Is.EqualTo(SurfaceMapNavigation.MaxZoom).Within(.001));
            nav.Zoom(-100,Vector2.zero); Assert.That(nav.Content.localScale.x,Is.EqualTo(SurfaceMapNavigation.MinZoom).Within(.001));
            nav.ResetView(); Assert.That(nav.Content.anchoredPosition,Is.EqualTo(-map.BunkerPosition*1.15f));
            Assert.That(root.transform.Find("Legend"),Is.Null);
            Assert.That(root.transform.Find("Title"),Is.Null);
            var card=(RectTransform)root.transform.Find("Sector card");
            Assert.That(card.rect.width,Is.LessThanOrEqualTo(340));
            Assert.That(nodes["C1"].transform.Find("Landmark").GetComponent<SurfaceMapPlate>(),Is.Not.Null);
            Assert.That(nodes["C2"].gameObject.activeSelf,Is.False);
            Assert.That(nodes["C1"].gameObject.activeSelf,Is.True);
            view.Inspect("C1");
            typeof(SurfaceMapView).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(view,null);
            var before=card.anchoredPosition;
            nav.Pan(new Vector2(-40,20));
            typeof(SurfaceMapView).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(view,null);
            Assert.That(Vector2.Distance(before,card.anchoredPosition),Is.GreaterThan(1),"Local briefing must follow the inspected location.");
            Assert.That(service.TryBuildRunConfig(out var config),Is.True);
            Assert.That(service.CompleteVictory(config,RunEndReason.Victory,false),Is.False);
            Assert.That(nodes["C2"].gameObject.activeSelf,Is.False,"Only a confirmed complete victory reveals a branch.");
            service.CompleteVictory(config,RunEndReason.Victory,true);
            Assert.That(nodes["C1"].transform.Find("State").gameObject.activeSelf,Is.True);
            Assert.That(nodes["C2"].gameObject.activeSelf,Is.True,"Existing victory must reveal the next location immediately.");
            Assert.That(frontier.sprite,Is.Not.SameAs(hint),"Victory reveals the full existing road.");
            Assert.That(Read<SurfaceMapFog>(view,"fog").CoverageAt(nodes["C2"].GetComponent<RectTransform>().anchoredPosition),Is.Zero);
            foreach(string first in new[]{"A1","D1"})
            {
                Assert.That(service.TrySelect(first),Is.True);
                Assert.That(service.TryBuildRunConfig(out var branch),Is.True);
                service.CompleteVictory(branch,RunEndReason.Victory,true);
                Assert.That(nodes[first[0]+"2"].gameObject.activeSelf,Is.True);
            }
            var reloaded=new SurfaceMapService(map,storage); view.Show(reloaded,null,null,null);
            foreach(string second in new[]{"A2","C2","D2"})
                Assert.That(nodes[second].gameObject.activeSelf,Is.True,"Fog follows existing saved progression after reopening.");
        }
        finally { typeof(LocalizationService).GetProperty("Instance").SetValue(null,previous); Object.DestroyImmediate(root); }
    }
    [Test] public void MapUsesCurrentCharacterAndReusableLocationFamilies()
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/SurfaceMap/PF_SurfaceMap.prefab");
        var data=new SerializedObject(asset.GetComponent<SurfaceMapView>());
        var character=data.FindProperty("defaultCharacter");
        Assert.That(character,Is.Not.Null,"The old baked placeholder must be replaced with CharacterData.");
        var gera=AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset");
        Assert.That(character.objectReferenceValue,Is.SameAs(gera));
        Assert.That(gera.GameplayIcon.name,Does.StartWith("Gera4"));
        Assert.That(data.FindProperty("locationIcons").arraySize,Is.GreaterThanOrEqualTo(5));
        var family=typeof(SurfaceSectorDefinition).GetProperty("LocationFamily");
        Assert.That(family,Is.Not.Null,"Location families are presentation metadata, not a new progression system.");
        var map=AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
        Assert.That(family.GetValue(map.Find("A1")).ToString(),Is.EqualTo("Ruins"));
        Assert.That(family.GetValue(map.Find("D2")).ToString(),Is.EqualTo("Technogenic"));
        Assert.That(family.GetValue(map.Find("C2")).ToString(),Is.EqualTo("AdvancedAnomalous"));
        var weather=ScriptableObject.CreateInstance<SurfaceSectorDefinition>();
        try
        {
            var weatherData=new SerializedObject(weather);
            var rule=AssetDatabase.LoadAssetAtPath<WorldRuleData>("Assets/_Project/Data/World/Rules/WorldRule_Rain.asset");
            weatherData.FindProperty("parameters").FindPropertyRelative("worldRule").objectReferenceValue=rule;
            weatherData.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(family.GetValue(weather).ToString(),Is.EqualTo("Weather"),"Authored weather uses the existing weather illustration category.");
            Assert.That(weather.BuildRunConfig("preview").WorldRule,Is.SameAs(rule),"Presentation must use the authored rule unchanged.");
        }
        finally { Object.DestroyImmediate(weather); }
    }
    [Test] public void RouteSearchSupportsLargerBranchingGraphsAndCycles()
    {
        var root=new GameObject("Route view",typeof(SurfaceMapView));
        var definition=ScriptableObject.CreateInstance<SurfaceMapDefinition>();
        var sectors=new SurfaceSectorDefinition[24];
        void Set(Object target,string field,object value) => target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
        try
        {
            for(int i=0;i<sectors.Length;i++)
            {
                sectors[i]=ScriptableObject.CreateInstance<SurfaceSectorDefinition>(); Set(sectors[i],"id","node"+i);
                var links=new System.Collections.Generic.List<string>{"node"+((i+23)%24),"node"+((i+1)%24)};
                if(i==0)links.Add("node12"); if(i==12)links.Add("node0");
                Set(sectors[i],"neighbours",links.ToArray());
            }
            Set(definition,"sectors",sectors); Set(definition,"startingSectors",new[]{"node0"});
            var service=new SurfaceMapService(definition,new Storage()); var view=root.GetComponent<SurfaceMapView>();
            Set(view,"renderedMap",definition); Set(view,"service",service);
            Assert.That(view.FindRoute("node13"),Is.EqualTo(new[]{"node0","node12","node13"}));
            Assert.That(view.LockedReason("node13"),Does.Contain("node12"));
        }
        finally { Object.DestroyImmediate(root); foreach(var sector in sectors)if(sector!=null)Object.DestroyImmediate(sector); Object.DestroyImmediate(definition); }
    }
    [Test] public void ZoomKeepsCursorAnchorAndLargeMapBounds()
    {
        var root=new GameObject("Viewport",typeof(RectTransform),typeof(SurfaceMapNavigation));
        var content=new GameObject("Content",typeof(RectTransform)); content.transform.SetParent(root.transform,false);
        try
        {
            ((RectTransform)root.transform).sizeDelta=new Vector2(744,514);
            var nav=root.GetComponent<SurfaceMapNavigation>(); nav.Configure((RectTransform)content.transform);
            nav.SetBounds(new Rect(-2000,-1000,4000,2000),Vector2.zero); nav.ResetView();
            Vector2 pointer=new(50,40); Vector2 before=(pointer-nav.Content.anchoredPosition)/nav.Content.localScale.x;
            nav.Zoom(1,pointer); Vector2 after=(pointer-nav.Content.anchoredPosition)/nav.Content.localScale.x;
            Assert.That(Vector2.Distance(before,after),Is.LessThan(.001));
            nav.Pan(new Vector2(100000,100000)); Assert.That(nav.Content.anchoredPosition.x,Is.LessThan(3000));
            Assert.That(nav.Content.anchoredPosition.x,Is.GreaterThan(1000),"Bounds must follow the graph, not six-node limits");
        }
        finally { Object.DestroyImmediate(root); }
    }
}
#endif
