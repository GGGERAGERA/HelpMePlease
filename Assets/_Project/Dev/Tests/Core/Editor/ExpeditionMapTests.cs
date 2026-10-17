#if UNITY_EDITOR
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class ExpeditionMapTests
{
    public sealed class Storage : ISurfaceMapStorage
    {
        public readonly Dictionary<string, string> Values = new();
        public string Load(string id) => Values.TryGetValue(id, out var json) ? json : null;
        public void Save(string id, string json) => Values[id] = json;
    }

    [Test]
    public void ScanningPersistsIntelWithoutUnlockingRoutesOrCompletingEvents()
    {
        var map = AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
        var storage = new Storage();
        var service = new SurfaceMapService(map, storage);
        var scan = typeof(SurfaceMapService).GetMethod("TryScan");
        var scanned = typeof(SurfaceMapService).GetMethod("IsScanned");
        Assert.That(scan, Is.Not.Null, "Scanning belongs to the existing map progression service.");
        Assert.That((bool)scan.Invoke(service, new object[] { "D2" }), Is.False, "Locked sectors cannot be scanned.");
        service.TrySelect("D1"); service.TryBuildRunConfig(out var first);
        service.CompleteVictory(first, RunEndReason.Victory, true);
        Assert.That(service.GetStatus("D2"), Is.EqualTo(SurfaceSectorStatus.Available));
        Assert.That((bool)scan.Invoke(service, new object[] { "D2" }), Is.True);
        Assert.That((bool)scan.Invoke(service, new object[] { "D2" }), Is.False, "Repeat scans must be inert.");
        var reloaded = new SurfaceMapService(map, storage);
        Assert.That((bool)scanned.Invoke(reloaded, new object[] { "D2" }), Is.True);
        Assert.That(reloaded.GetStatus("D2"), Is.EqualTo(SurfaceSectorStatus.Available));
        reloaded.TrySelect("D2"); reloaded.TryBuildRunConfig(out var config);
        Assert.That(config.Content.GuaranteedEvents.Count, Is.EqualTo(1), "Scanning must not consume the expedition event.");
        Assert.That(config.Content.GuaranteedEvents[0].AvailableInProduction, Is.True);
        Assert.That(reloaded.Content.TryGetMarker("D2", out var marker), Is.True);
        Assert.That(marker.State, Is.EqualTo(SurfaceMarkerState.Active));
    }

    [Test]
    public void ProductionMapContainsPlayableEventAndCompactAuthoredControls()
    {
        var map = AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
        var service = new SurfaceMapService(map, new Storage());
        var config = service.Content.Resolve(map.Find("D2").BuildRunConfig(map.Id));
        Assert.That(config.Content.GuaranteedEvents.Count, Is.EqualTo(1));
        Assert.That(config.Content.GuaranteedEvents[0], Is.TypeOf<OrbitalRelayEvent>());
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/SurfaceMap/PF_SurfaceMap.prefab");
        Assert.That(prefab.transform.Find("Coordinates"), Is.Null);
        Assert.That(prefab.transform.Find("Uplink"), Is.Null);
        Assert.That(new SerializedObject(prefab.GetComponent<SurfaceMapView>()).FindProperty("scanButton")?.objectReferenceValue,
            Is.Not.Null, "Scanning must have a real authored control.");
        var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/_Project/Data/Localization/LocalizationTable.asset");
        Assert.That(table.TryGet("map.title", GameLanguage.Russian, out var heading), Is.True,
            "Authored labels must reach the localization asset.");
        Assert.That(heading, Is.Not.Null.And.Not.Empty);
    }
}

public sealed class ExpeditionMapPlayTests
{
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();
    private static T Read<T>(Object target, string field) => (T)target.GetType().GetField(field,
        BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

    [UnityTest]
    public IEnumerator ScanThenLaunchCarriesRealRelayIntoProductionExpedition()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        yield return CoreTestSupport.LoadBunker();
        var definition = AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
        var storage = new ExpeditionMapTests.Storage();
        var service = new SurfaceMapService(definition, storage);
        typeof(MetaProgressionManager).GetProperty("SurfaceMap").SetValue(MetaProgressionManager.Instance, service);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        var panels = Object.FindFirstObjectByType<BunkerPanelManager>();
        Assert.That(Read<SurfaceMapView>(panels, "surfaceMapPanel"), Is.Not.Null, "The bunker must reference the production map.");
        panels.OpenDepthSelect(starter.transform);
        var view = Object.FindFirstObjectByType<SurfaceMapView>();
        Assert.That(view, Is.Not.Null, "Opening the production map must activate its hierarchy.");
        var mapRect=(RectTransform)view.transform;
        Assert.That(mapRect.anchorMin,Is.EqualTo(Vector2.zero));
        Assert.That(mapRect.anchorMax,Is.EqualTo(Vector2.one));
        Assert.That(mapRect.rect.width,Is.EqualTo(((RectTransform)mapRect.parent).rect.width).Within(.1f),"The world must fill the actual bunker UI canvas.");
        var gera=AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset");
        var vika=AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/03_Vika.asset");
        RunSelectionManager.Instance.SelectCharacter(vika);
        Assert.That(Read<Image>(view,"hero").sprite,Is.SameAs(vika.GameplayIcon),"The map must react to the actual character selection.");
        RunSelectionManager.Instance.SelectCharacter(gera);
        Assert.That(Read<Image>(view,"hero").sprite,Is.SameAs(gera.GameplayIcon));
        Directory.CreateDirectory("Artifacts/GeneratedQA/ExpeditionMap");
        view.Inspect("C1");
        yield return Capture(view, "presentation-normal.png");
        var navigation=Read<SurfaceMapNavigation>(view,"navigation");
        navigation.Zoom(1,Vector2.zero); navigation.Pan(new Vector2(-220,100));
        yield return Capture(view, "presentation-panned.png");
        navigation.ResetView(); view.Inspect("A1");
        Assert.That(service.GetStatus("A2"),Is.EqualTo(SurfaceSectorStatus.Locked));
        Assert.That(service.TryBuildRunConfig(out var firstBranch),Is.True);
        Assert.That(service.CompleteVictory(firstBranch,RunEndReason.Victory,true),Is.True);
        Assert.That(service.GetStatus("A2"),Is.EqualTo(SurfaceSectorStatus.Available));
        view.Inspect("A2");
        foreach(string first in new[]{"C1","D1"})
        {
            service.TrySelect(first); service.TryBuildRunConfig(out var branch);
            service.CompleteVictory(branch,RunEndReason.Victory,true);
        }
        view.Inspect("D2");
        yield return null;
        var scan = Read<Button>(view, "scanButton");
        Assert.That(scan.interactable, Is.True);
        Assert.That(Read<TMP_Text>(view, "details").text, Does.Not.Contain(LocalizationService.Instance.Get("map.event.relay")));
        scan.onClick.Invoke();
        yield return null;
        Assert.That(service.IsScanned("D2"), Is.True);
        Assert.That(Read<TMP_Text>(view, "details").text, Does.Contain(LocalizationService.Instance.Get("map.event.relay")));
        Assert.That(scan.gameObject.activeSelf, Is.False);
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"));
        var launch = Read<Button>(view, "startButton");
        Assert.That(launch.interactable, Is.True);
        launch.onClick.Invoke();
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" && !SceneTransitionOverlay.IsTransitioning &&
            ProductionAnomalySite.ActiveSites.Any(site => Read<WorldEvent>(site, "activeEvent") is OrbitalRelayEvent));
        Assert.That(RunStateManager.Instance.CurrentConfig.SectorId, Is.EqualTo("D2"));
        var required = RunStateManager.Instance.CurrentConfig.Content.GuaranteedEvents.Single();
        Assert.That(required, Is.TypeOf<OrbitalRelayEvent>());
        var relay = ProductionAnomalySite.ActiveSites.Select(site => Read<WorldEvent>(site, "activeEvent")).OfType<OrbitalRelayEvent>().First();
        Assert.That(relay.SourcePrefab, Is.SameAs(required), "The map marker must lead to the real authored event.");
    }

    // Keep this helper outside the iterator: a captured closure does not survive EnterPlayMode's domain reload.
    private static IEnumerator Capture(SurfaceMapView view, string name)
    {
        yield return null;
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/ExpeditionMap/"+name);
        yield return new WaitForSecondsRealtime(.25f);
        if(name=="presentation-normal.png")
        {
            // A crop of the actual captured game frame, without re-rendering or altering the UI.
            var card=Read<RectTransform>(view,"card"); var corners=new Vector3[4]; card.GetWorldCorners(corners);
            var canvas=card.GetComponentInParent<Canvas>(); var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            Vector2 min=RectTransformUtility.WorldToScreenPoint(camera,corners[0]),max=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
            var screen=new Texture2D(2,2); Texture2D crop=null;
            try
            {
                Assert.That(screen.LoadImage(File.ReadAllBytes("Artifacts/GeneratedQA/ExpeditionMap/"+name)),Is.True);
                int x=Mathf.Max(0,Mathf.FloorToInt(min.x)-8),y=Mathf.Max(0,Mathf.FloorToInt(min.y)-8);
                int width=Mathf.Min(screen.width,Mathf.CeilToInt(max.x)+8)-x,height=Mathf.Min(screen.height,Mathf.CeilToInt(max.y)+8)-y;
                crop=new Texture2D(width,height,TextureFormat.RGB24,false);
                crop.SetPixels(screen.GetPixels(x,y,width,height)); crop.Apply();
                File.WriteAllBytes("Artifacts/GeneratedQA/ExpeditionMap/presentation-card.png",crop.EncodeToPNG());
            }
            finally { Object.Destroy(screen); if(crop!=null)Object.Destroy(crop); }
        }
    }
}
#endif
