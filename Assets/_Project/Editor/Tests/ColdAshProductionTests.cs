using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;

public class ColdAshProductionTests
{
    const string Output=SurfaceVisualLabEditor.Output+"/Production";
    readonly Subject42FinalBossFlowTests preferences=new();
    [SetUp] public void PreserveProgress()=>preferences.PreserveRewardsAndUnlockProgress();
    [UnityTearDown] public IEnumerator Cleanup()=>preferences.CleanupPlayMode();
    static T One<T>() where T:Object=>Object.FindFirstObjectByType<T>();
    static object Get(object o,string n)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
    static void Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
    static IEnumerator Existing(string n,params object[] args)=>(IEnumerator)typeof(Subject42FinalBossFlowTests).GetMethod(n,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
    static GameObject Player=>One<CharacterSpawner>().SpawnedPlayer;
    static void Log(string message)=>File.AppendAllText(Output+"/checks.txt",message+"\n");
    static IEnumerator Capture(string name)
    {
        Assert.That(SceneManager.GetActiveScene().path,Is.EqualTo("Assets/_Project/Scenes/MainBuild/MVP.unity"));
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        // Let the real camera settle in rendered frames, then pause the sampled combat moment.
        // This avoids judging pixel art from the transient motion-blur history after a QA teleport.
        if(!name.StartsWith("03")&&!name.StartsWith("04"))
            for(int i=0;i<45;i++)yield return null;
        float scale=Time.timeScale;Time.timeScale=0;
        for(int i=0;i<8;i++)yield return null;
        ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");
        yield return new WaitForSecondsRealtime(.3f);
        Time.timeScale=scale;
        Log($"{name}: scene=MVP sector={RunStateManager.Instance.CurrentLevel} phase={RunFlowController.Instance.Phase} enemies={EnemyHealth.ActiveInstances.Count} XP={Object.FindObjectsByType<ExperiencePickup>(FindObjectsSortMode.None).Length} sites={ProductionAnomalySite.ActiveSites.Count} crates={WorldBreakable.ActiveInstances.Count} camera={Camera.main.orthographicSize}");
    }
    [Test]
    public void PackedEnvironmentReimportAndLabAuthoringPreserveContent()
    {
        string cold=ColdAshSurfaceAuthoring.ArtPath,labArt=SurfaceVisualLabEditor.Root+"/Art";
        string[] assets={cold+"/ColdAsh.png",cold+"/ColdAshTiles.asset",labArt+"/SurfaceComparisons.png",labArt+"/SurfaceComparisonsTiles.asset"};
        string[] Ids(string path)=>AssetDatabase.LoadAllAssetsAtPath(path).Where(a=>a is Sprite||a is Tile).Select(a=>
        {AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a,out string guid,out long id);return a.name+":"+guid+":"+id;}).OrderBy(v=>v).ToArray();
        foreach(string path in assets)
        {
            var before=Ids(path);Assert.That(before,Is.Not.Empty);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            Assert.That(Ids(path),Is.EqualTo(before),"Sub-asset IDs changed after reimport: "+path);
            if(path.EndsWith(".png"))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.spritePixelsPerUnit,Is.EqualTo(32));Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));
                Assert.That(importer.mipmapEnabled,Is.False);Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));
            }
        }
        string[] Fingerprint(GameObject root)
        {
            var records=new List<string>();
            foreach(var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Assert.That(sr.sprite,Is.Not.Null,"Missing micro/decoration sprite");
                records.Add($"S {sr.sprite.name} {sr.transform.position:R} {sr.transform.lossyScale:R} {sr.color} {sr.flipX} {sr.flipY} {sr.sortingLayerID} {sr.sortingOrder}");
            }
            foreach(var map in root.GetComponentsInChildren<Tilemap>(true))
            {
                Assert.That(map.GetUsedTilesCount(),Is.EqualTo(4));
                foreach(var pos in map.cellBounds.allPositionsWithin)
                {
                    Assert.That(map.GetTile(pos),Is.Not.Null,"Missing Tile at "+pos);Assert.That(map.GetSprite(pos),Is.Not.Null,"Missing Tile sprite at "+pos);
                    records.Add($"T {pos} {map.GetTile(pos).name} {map.GetSprite(pos).name} {map.GetColor(pos)} {map.GetTransformMatrix(pos)}");
                }
            }
            return records.OrderBy(v=>v,StringComparer.Ordinal).ToArray();
        }
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MVP.unity");
        var surface=SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="Cold Ash Ground");
        Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(surface),Is.EqualTo(ColdAshSurfaceAuthoring.PrefabPath));
        Fingerprint(surface);Assert.That(surface.GetComponentsInChildren<SpriteRenderer>(true).Length,Is.EqualTo(127));
        var original=File.ReadAllBytes(SurfaceVisualLabEditor.ScenePath);
        EditorSceneManager.OpenScene(SurfaceVisualLabEditor.ScenePath);
        var expected=One<SurfaceVisualLab>().Presets.Select(Fingerprint).ToArray();
        var assetPaths=AssetDatabase.GetAllAssetPaths().OrderBy(v=>v).ToArray();
        try
        {
            SurfaceVisualLabEditor.Build();
            var actual=One<SurfaceVisualLab>().Presets.Select(Fingerprint).ToArray();
            for(int i=0;i<4;i++)Assert.That(actual[i],Is.EqualTo(expected[i]),"Authoring changed preset "+(i+1));
            Assert.That(AssetDatabase.GetAllAssetPaths().OrderBy(v=>v).ToArray(),Is.EqualTo(assetPaths),"Authoring created extra assets");
        }
        finally
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            File.WriteAllBytes(SurfaceVisualLabEditor.ScenePath,original);
            AssetDatabase.ImportAsset(SurfaceVisualLabEditor.ScenePath,ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MVP.unity");
        }
    }

    [UnityTest]
    public IEnumerator ProductionMovementCrowdsEventsSectorsAndBoss()
    {
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/checks.txt","");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseProduction();
    }
    static IEnumerator ExerciseProduction()
    {
        Application.runInBackground=true;
        yield return Existing("StartRun","Gera");
        Log("Scene after StartRun: "+SceneManager.GetActiveScene().path);
        Log("Loaded roots: "+string.Join(",",SceneManager.GetActiveScene().GetRootGameObjects().Select(g=>g?g.name+" active="+g.activeSelf:"NULL ROOT")));
        Assert.That(One<CharacterSpawner>(),Is.Not.Null,"CharacterSpawner absent after StartRun");
        var player=Player;var station=One<OrbitalStationRuntime>();
        player.GetComponent<PlayerHealth>().SetRuntimeHealth(100,100);
        player.GetComponent<PlayerHealth>().SetIncomingDamageMultiplier(0);
        var maps=Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        Log("Tilemaps: "+string.Join(",",maps.Select(t=>t.name+" active="+t.gameObject.activeInHierarchy)));
        var ground=One<Tilemap>();Assert.That(ground,Is.Not.Null,"Production Cold Ash Tilemap is absent");Assert.That(ground.name,Is.EqualTo("Ground"));
        Assert.That(ground.GetUsedTilesCount(),Is.EqualTo(4));
        Assert.That(ground.transform.parent.GetComponentsInChildren<Collider2D>(),Is.Empty);
        Assert.That(ProductionAnomalySite.ActiveSites.Count,Is.EqualTo(4));
        Assert.That(WorldBreakable.ActiveInstances.Count,Is.GreaterThanOrEqualTo(4));
        yield return Capture("01-sector-start");
        var movement=player.GetComponent<CharacterMovement2D>();var rb=player.GetComponent<Rigidbody2D>();var origin=rb.position;
        // Exercise the actual FixedUpdate motor with its normal input state, without an input shim in production.
        movement.enabled=false;Set(movement,"moveInput",Vector2.right);
        for(int i=0;i<50;i++){Call(movement,"FixedUpdate");yield return new WaitForFixedUpdate();}
        Assert.That(rb.position.x-origin.x,Is.GreaterThan(.5f),"Motor must move despite sector-specific speed modifiers");
        Set(movement,"moveInput",Vector2.zero);movement.enabled=true;
        Log("PASS production movement motor across Cold Ash; no decorative collision.");
        yield return Capture("02-movement-open-field");
        // Frame a real large production site interior at normal gameplay magnification, away from its focus boundary.
        var clearSite=ProductionAnomalySite.ActiveSites.First(s=>!s.IsSpecial&&s.DebugZoneName.Contains("BERSERK"));
        rb.position=clearSite.transform.position;Physics2D.SyncTransforms();
        yield return new WaitForSeconds(5);
        station.ApplyPresetMid();Assert.That(station.Rings.Count,Is.EqualTo(4));
        for(int i=0;i<60;i++)yield return null;
        int hits=0;station.Combat.Hit+=(e,d)=>hits++;
        var spawner=player.GetComponent<EnemySpawner>();
        var paths=new[]{"p_Enemy_classic","p_Enemy_default","p_Enemy_Shooter","p_Enemy_Bomber"};
        var prefabs=paths.Select(p=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/"+p+".prefab")).ToArray();
        var xp=(GameObject)new SerializedObject(prefabs[0].GetComponent<EnemyHealth>()).FindProperty("lootPrefab").objectReferenceValue;
        void SpawnCrowd(int count)
        {for(int i=0;i<count;i++){float a=i*2.39996f;float r=4.5f+(i%5)*.7f;var e=spawner.SpawnDebugEnemyAt(prefabs[i%4],player.transform.position+new Vector3(Mathf.Cos(a)*r*1.4f,Mathf.Sin(a)*r));e.GetComponent<EnemyHealth>().SetRuntimeMaxHealth(100000);}}
        for(int i=0;i<24;i++){float a=i*2.39996f;Object.Instantiate(xp,player.transform.position+new Vector3(Mathf.Cos(a)*(7+i%3),Mathf.Sin(a)*(7+i%3)),Quaternion.identity);}
        SpawnCrowd(24);yield return new WaitForSeconds(.6f);yield return Capture("03-medium-crowd-orbital-xp");
        SpawnCrowd(38);yield return new WaitForSeconds(.6f);yield return Capture("04-dense-combat");
        Assert.That(hits,Is.GreaterThan(0));Assert.That(station.ValidateState(out string error),Is.True,error);Log($"PASS live ORBITAL hits={hits}, rings={station.Rings.Count}, modules={station.Modules.Count}");
        var crate=WorldBreakable.ActiveInstances.First(c=>!c.IsBroken);
        rb.position=(Vector2)crate.transform.position+Vector2.down*2;Physics2D.SyncTransforms();yield return new WaitForSeconds(1);
        yield return Capture("05-production-breakable");
        if(!crate.IsBroken)crate.TakeDamage(float.MaxValue,crate.transform.position);
        Assert.That(crate.IsBroken,Is.True);Log("PASS production crate damage/break.");
        var site=ProductionAnomalySite.ActiveSites.First(s=>!s.IsSpecial);
        var worldEvent=(WorldEvent)Get(site,"activeEvent");Assert.That(worldEvent,Is.Not.Null);
        rb.position=(Vector2)worldEvent.transform.position+Vector2.down*3;Physics2D.SyncTransforms();yield return new WaitForSeconds(1);
        yield return Capture("06-anomaly-event");Log("PASS actual site event: "+worldEvent.GetType().Name);
        yield return Existing("ReachFinalSector");
        Player.GetComponent<PlayerHealth>().SetRuntimeHealth(100,100);Player.GetComponent<PlayerHealth>().SetIncomingDamageMultiplier(0);
        Assert.That(RunStateManager.Instance.CurrentLevel,Is.EqualTo(RunRoute.TotalSectors));
        Assert.That(One<Tilemap>().GetUsedTilesCount(),Is.EqualTo(4));
        yield return Capture("07-final-sector-after-transitions");
        Assert.That(RunFlowController.Instance.HandleExitReached(),Is.True);
        float deadline=Time.realtimeSinceStartup+30;
        while(RunFlowController.Instance.Phase!=RunPhase.FinalBossCombat&&Time.realtimeSinceStartup<deadline)yield return null;
        var boss=RunFlowController.Instance.FinalBoss;Assert.That(boss,Is.Not.Null);
        Assert.That(EnemyHealth.ActiveInstances.Count(e=>e.IsBoss),Is.EqualTo(1));
        Player.GetComponent<Rigidbody2D>().position=(Vector2)boss.transform.position+Vector2.down*5;Physics2D.SyncTransforms();yield return new WaitForSeconds(1);
        yield return Capture("08-final-boss");
        Log("PASS real transitions through sectors 1–3 and single final boss; original spawning remains enabled.");
    }
}
