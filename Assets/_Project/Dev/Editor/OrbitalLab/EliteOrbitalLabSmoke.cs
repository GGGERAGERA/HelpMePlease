#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using Subject42.DebugLabs.EliteOrbit;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// One bounded Play Mode scene smoke, not a test-suite/Golden Path/Batch Runner invocation.
[InitializeOnLoad]
public static class EliteOrbitalLabSmoke
{
    private const string Output="Artifacts/EliteOrbit",Key="Subject42.EliteOrbit.OneSmoke";
    private static IEnumerator routine;
    private static readonly List<string> report=new();
    private static double next,deadline;
    private static int errors;
    static EliteOrbitalLabSmoke()
    { EditorApplication.update+=Poll; EditorApplication.playModeStateChanged+=State; Application.logMessageReceived+=Log; }
    public static void CreateAndSmoke()
    {
        Directory.CreateDirectory(Output); EliteOrbitalLabAuthoring.Create(); EditorSceneManager.OpenScene(EliteOrbitalLab.ScenePath);
        var result=PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings { target=BuildTarget.StandaloneWindows64,
            group=BuildTargetGroup.Standalone,options=ScriptCompilationOptions.DevelopmentBuild },Output+"/Compilation");
        if (result.assemblies==null || result.assemblies.Count==0) throw new Exception("Development compilation failed");
        File.WriteAllText(Output+"/compile.txt","PASS: Editor domain loaded; Development scripts compiled.\n");
        SessionState.SetInt(Key,1); EditorApplication.EnterPlaymode();
    }
    private static void State(PlayModeStateChange state)
    {
        if (SessionState.GetInt(Key,0)==0) return;
        if (state==PlayModeStateChange.EnteredPlayMode) { routine=Checks(); errors=0; report.Clear(); deadline=EditorApplication.timeSinceStartup+90; next=0; }
        if (state==PlayModeStateChange.EnteredEditMode && SessionState.GetInt(Key,0)>=2)
        { int code=SessionState.GetInt(Key,0)==2 ? 0 : 1; SessionState.EraseInt(Key); if (Application.isBatchMode) EditorApplication.Exit(code); }
    }
    private static void Log(string text,string stack,LogType type)
    { if (routine!=null && (type==LogType.Error || type==LogType.Exception || type==LogType.Assert)) { errors++; report.Add("UNITY ERROR: "+text); } }
    private static void Poll()
    {
        if (File.Exists(Output+"/smoke.request") && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            try { File.Delete(Output+"/smoke.request"); } catch (IOException) { return; }
            try { CreateAndSmoke(); } catch (Exception error) { File.WriteAllText(Output+"/smoke.txt","FAIL: "+error); Debug.LogException(error); }
        }
        if (routine==null || EditorApplication.timeSinceStartup<next) return;
        try
        {
            if (EditorApplication.timeSinceStartup>deadline) throw new Exception("Smoke timeout");
            if (!routine.MoveNext()) { Finish(errors==0); return; }
            next=EditorApplication.timeSinceStartup+(routine.Current is float delay ? delay : .02);
        }
        catch (Exception error) { report.Add(error.ToString()); Finish(false); }
    }
    private static void Finish(bool pass)
    { routine=null; report.Add(pass ? "PASS: one Play Mode smoke; no runtime errors" : "FAIL"); File.WriteAllLines(Output+"/smoke.txt",report); Debug.Log("[EliteOrbitSmoke] "+report[report.Count-1]); SessionState.SetInt(Key,pass ? 2 : 3); EditorApplication.ExitPlaymode(); }
    private static void Require(bool condition,string message)
    { if (!condition) throw new Exception(message); report.Add("PASS: "+message); }
    private static void Fixture(EliteOrbitalLab lab)
    {
        lab.Station.enabled=false;
        var movement=lab.Player.GetComponent<CharacterMovement2D>(); movement.enabled=false;
        lab.Player.GetComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Kinematic;
        lab.Elite.GetComponent<CombatFeelTestDummy>().ApplyMode(CombatFeelDummyMode.Static);
        lab.Elite.transform.position=Vector3.zero; lab.Elite.DebugFreezeOrbit=true;
        lab.Elite.Ring.State.CurrentPhase=0; lab.Elite.Ring.Tick(0); Physics2D.SyncTransforms(); lab.CombatPolicy.Reset();
        PlayerAt(lab,new Vector2(-6,0));
    }
    private static void PlayerAt(EliteOrbitalLab lab,Vector2 position)
    {
        lab.Player.GetComponent<Rigidbody2D>().position=position; lab.Player.transform.position=position;
        foreach (var ring in lab.Station.Rings) ring.Tick(0);
        Physics2D.SyncTransforms();
    }
    private static IEnumerator Checks()
    {
        yield return .35f;
        var lab=Object.FindFirstObjectByType<EliteOrbitalLab>();
        Require(lab!=null && !lab.Busy && lab.Station.IsInitialized,"ordinary circular player station initialized without constructor");
        Require(Object.FindFirstObjectByType<ProductionSceneComposition>()==null,"no production/progression scene composition");
        lab.Spawn(0); yield return .2f; Fixture(lab);
        Require(lab.Elite.Ring.Mounts.Count==6 && lab.Elite.HasShield,"shield preset uses shared ring and six mounting points");
        float hp=lab.Elite.Health.CurrentHealth; int blocks=lab.Elite.Blocks;
        lab.Station.Combat.SpawnProjectile(new Vector2(3,0),lab.Elite.Health,13,8,Color.cyan);
        for (int i=0;i<12;i++) lab.Station.Combat.Tick(.025f);
        Require(lab.Elite.Blocks>blocks && lab.Elite.Health.CurrentHealth==hp,$"real production projectile hits armor and is released without body damage (blocks {blocks}->{lab.Elite.Blocks}, HP {hp}->{lab.Elite.Health.CurrentHealth}, phase {lab.Elite.Phase}, center {lab.Elite.Center}, mount {lab.Elite.Ring.Mounts[0].Transform.position})");
        lab.Station.Combat.SpawnProjectile(new Vector2(-3,0),lab.Elite.Health,13,8,Color.cyan);
        for (int i=0;i<12;i++) lab.Station.Combat.Tick(.025f);
        Require(lab.Elite.Health.CurrentHealth<hp,"production projectile damages body through open sector");
        // Isolate one existing player sword and sweep its actual authored body rapidly through the plate.
        var other=lab.Station.Modules[1].CurrentMount.Transform.GetComponentInChildren<OrbitalModuleView>(); other.gameObject.SetActive(false);
        hp=lab.Elite.Health.CurrentHealth; blocks=lab.Elite.Blocks;
        lab.CombatPolicy.Reset(); PlayerAt(lab,new Vector2(3,0)); lab.CombatPolicy.TickContacts(.02f);
        PlayerAt(lab,new Vector2(-1.1f,0)); lab.CombatPolicy.TickContacts(.02f);
        Require(lab.Elite.Blocks>blocks && lab.Elite.Health.CurrentHealth==hp,"fast actual sword sweep blocks before simultaneous body hit; player module survives");
        yield return .25f;
        PlayerAt(lab,new Vector2(-1.45f,0)); lab.CombatPolicy.Reset(); lab.CombatPolicy.TickContacts(.02f);
        Require(lab.Elite.Health.CurrentHealth<hp,"contact weapon damages exposed body from inside defensive radius");
        lab.SetWeapon(1); yield return .2f;
        Require(lab.Station.Modules.All(m=>m.Kind==OrbitalModuleKind.Saw) && Mathf.Abs(lab.Station.Rings[0].Radius-3.25f)<.01f,"far-contact load uses existing swords");
        lab.SetWeapon(2); yield return .2f;
        Require(lab.Station.Modules.All(m=>m.Kind==OrbitalModuleKind.Pistol),"shooting load uses existing pistols");
        lab.Spawn(1); yield return .2f; Fixture(lab);
        Require(lab.Elite.Preset.Slots.Count(s=>s.Kind==EliteModuleKind.ContactBlade)==2,"two opposite hostile blade modules");
        var blade=lab.Elite.GetComponentsInChildren<OrbitalModuleView>().First();
        var sprite=blade.Sprites.First(s=>s!=blade.Halo && s.enabled);
        PlayerAt(lab,sprite.bounds.center); hp=lab.Player.CurrentHealth; lab.Station.Combat.Tick(.02f);
        Require(lab.Player.CurrentHealth<hp,"hostile blade damages player only on actual authored blade contact");
        hp=lab.Player.CurrentHealth; PlayerAt(lab,new Vector2(0,2.25f)); yield return .75f; lab.Station.Combat.Tick(.02f);
        Require(lab.Player.CurrentHealth==hp,"gap between blades and orbit trajectory are harmless");
        lab.Spawn(2); yield return .2f; Fixture(lab);
        Require(lab.Elite.LiveNodes==3 && lab.Elite.DamageMultiplier>0,"three destructible satellites; body never fully invulnerable");
        hp=lab.Elite.Health.CurrentHealth; lab.Station.Combat.ApplyDamage(lab.Elite.Health,20,Vector2.zero);
        float reduced=hp-lab.Elite.Health.CurrentHealth;
        var node=lab.Elite.Nodes[0];
        for (int shot=0;shot<3;shot++)
        {
            lab.Station.Combat.SpawnProjectile(new Vector2(4,0),node.Health,13,8,Color.cyan);
            for (int i=0;i<12;i++) lab.Station.Combat.Tick(.025f);
        }
        Require(lab.Elite.LiveNodes==2 && node.Destroyed,"ordinary production shots destroy satellite permanently for encounter");
        hp=lab.Elite.Health.CurrentHealth; lab.Station.Combat.ApplyDamage(lab.Elite.Health,20,Vector2.zero);
        Require(hp-lab.Elite.Health.CurrentHealth>reduced,"destroying node weakens body reduction");
        lab.Elite.Health.TakeDamage(1000,Vector2.zero); yield return .1f;
        Require(lab.Elite==null && !EnemyHealth.ActiveInstances.Any(e=>e.gameObject.scene==lab.gameObject.scene),"death cleans body, orbit and all satellite health instances");
        lab.Restart(); yield return .2f;
        Require(lab.Elite.LiveNodes==3 && lab.Elite.Health.CurrentHealth==lab.Elite.Preset.BodyHealth,"restart restores identical encounter and node health");
        lab.Clear(); yield return .1f;
        Require(lab.Elite==null && !EnemyHealth.ActiveInstances.Any(e=>e.gameObject.scene==lab.gameObject.scene),"clear removes encounter and temporary state");
        lab.WithGroup=true; lab.Spawn(0); yield return .2f;
        Require(EnemyHealth.ActiveInstances.Count(e=>e.gameObject.scene==lab.gameObject.scene)==5,"optional encounter contains elite and four ordinary enemies");
        lab.WithGroup=false; lab.Restart(); yield return .2f;
        Require(EnemyHealth.ActiveInstances.Count(e=>e.gameObject.scene==lab.gameObject.scene)==5,"restart preserves originally spawned group despite changed next-spawn option");
        for (int preset=0;preset<3;preset++)
        {
            lab.Spawn(preset);
            while (lab.Busy) yield return .02f;
            lab.Station.enabled=false;
            (lab.Station.Combat as ProductionOrbitalCombatAdapter)?.DebugClearProjectiles();
            yield return .3f;
            Require(EnemyHealth.ActiveInstances.Count(e=>e.gameObject.scene==lab.gameObject.scene)==(preset==2 ? 4 : 1),"new solo spawn clears prior group, retaining only elite and its own nodes");
            lab.SetPaused(true); Capture(lab,preset); lab.SetPaused(false);
        }
        Require(errors==0,"no Unity runtime errors");
        report.Add("Smoke confirms mechanics only. Tactical interest and balance need manual testing.");
    }
    private static void Capture(EliteOrbitalLab lab,int preset)
    {
        var camera=lab.ArenaCamera; var prior=camera.targetTexture; var rect=camera.rect; var active=RenderTexture.active;
        var render=new RenderTexture(1280,960,24);
        try
        {
            camera.targetTexture=render; camera.rect=new Rect(0,0,1,1); camera.Render(); RenderTexture.active=render;
            var image=new Texture2D(1280,960,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1280,960),0,0); image.Apply();
            File.WriteAllBytes(Output+"/elite-"+preset+".png",image.EncodeToPNG()); Object.DestroyImmediate(image);
        }
        finally { camera.targetTexture=prior; camera.rect=rect; RenderTexture.active=active; render.Release(); Object.DestroyImmediate(render); }
    }
}
#endif
