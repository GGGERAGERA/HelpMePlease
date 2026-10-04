#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public sealed class SurfaceMapUxTests
{
    sealed class Storage:ISurfaceMapStorage { public string Load(string id)=>null; public void Save(string id,string json){} }
    static T Read<T>(Object target,string field)=>(T)target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
    [Test] public void InspectionRouteAndViewStateStayInPresentation()
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/SurfaceMap/PF_SurfaceMap.prefab");
        var root=Object.Instantiate(asset); var view=root.GetComponent<SurfaceMapView>();
        var previous=LocalizationService.Instance;
        var localization=root.AddComponent<LocalizationService>();
        typeof(LocalizationService).GetField("table",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(localization,AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/_Project/Data/Localization/LocalizationTable.asset"));
        typeof(LocalizationService).GetProperty("Instance").SetValue(null,localization);
        try
        {
            typeof(SurfaceMapView).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(view,null);
            var map=AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
            var service=new SurfaceMapService(map,new Storage()); service.TrySelect("C1");
            view.Show(service,null,null,null);
            Assert.That(view.FindRoute("C2"),Is.EqualTo(new[]{"C1","C2"}));
            view.Inspect("C2"); Assert.That(service.SelectedSectorId,Is.EqualTo("C1"));
            Assert.That(Read<Button>(view,"startButton").interactable,Is.False);
            Assert.That(Read<Button>(view,"startButton").GetComponentInChildren<TMP_Text>().text,Is.EqualTo("ROUTE LOCKED"));
            Assert.That(Read<TMP_Text>(view,"details").text,Does.Contain("ROUTE LOCKED").And.Contain("Complete C1"));
            view.Inspect("D1"); Assert.That(Read<TMP_Text>(view,"details").text,Does.Contain("UNKNOWN ACTIVITY").And.Contain("???"));
            var nav=Read<SurfaceMapNavigation>(view,"navigation"); nav.Zoom(2,Vector2.zero); nav.Pan(new Vector2(80,30));
            var position=nav.Content.anchoredPosition; var zoom=nav.Content.localScale;
            root.SetActive(false); view.Show(service,null,null,null);
            Assert.That(nav.Content.anchoredPosition,Is.EqualTo(position)); Assert.That(nav.Content.localScale,Is.EqualTo(zoom));
            Assert.That(Read<TMP_Text>(view,"details").text,Does.Contain("SECTOR D1"));
            nav.Pan(Vector2.one*100000); Assert.That(nav.Content.anchoredPosition.magnitude,Is.LessThan(2000));
            nav.Zoom(100,Vector2.zero); Assert.That(nav.Content.localScale.x,Is.EqualTo(SurfaceMapNavigation.MaxZoom).Within(.001));
            nav.Zoom(-100,Vector2.zero); Assert.That(nav.Content.localScale.x,Is.EqualTo(SurfaceMapNavigation.MinZoom).Within(.001));
            nav.ResetView(); Assert.That(nav.Content.anchoredPosition,Is.EqualTo(-map.BunkerPosition*1.15f));
            Assert.That(root.transform.Find("Legend"),Is.Not.Null); Assert.That(root.transform.Find("Reset View"),Is.Not.Null);
        }
        finally { typeof(LocalizationService).GetProperty("Instance").SetValue(null,previous); Object.DestroyImmediate(root); }
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
