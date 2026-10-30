#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Subject42.DebugLabs.Constructor;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// One short scene-local Play Mode smoke. Does not invoke Test Runner, Golden Path or Batch Runner.
[InitializeOnLoad]
public static class OrbitalConstructorSmoke
{
    private const string Key="Subject42.Constructor.SingleSmoke";
    private const string Output="Artifacts/OrbitalConstructor";
    private static IEnumerator routine;
    private static double next, deadline;
    private static readonly List<string> report=new();
    private static int errors;
    static OrbitalConstructorSmoke()
    {
        EditorApplication.playModeStateChanged+=State;
        EditorApplication.update+=Update;
        Application.logMessageReceived+=Log;
    }
    public static void CreateAndSmoke()
    {
        Directory.CreateDirectory(Output);
        OrbitalConstructorAuthoring.Create();
        EditorSceneManager.OpenScene(OrbitalConstructorLab.ScenePath);
        OrbitalConstructorAuthoring.ConfigureTrainingScene();
        var result=PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings
        {
            target=BuildTarget.StandaloneWindows64, group=BuildTargetGroup.Standalone,
            options=ScriptCompilationOptions.DevelopmentBuild
        },Output+"/Compilation");
        if (result.assemblies==null || result.assemblies.Count==0) throw new Exception("Development scripts did not compile");
        File.WriteAllText(Output+"/compile.txt","PASS: Editor domain loaded; Development scripts compiled ("+result.assemblies.Count+" assemblies).\n");
        SessionState.SetInt(Key,1);
        EditorApplication.EnterPlaymode();
    }
    private static void State(PlayModeStateChange state)
    {
        if (SessionState.GetInt(Key,0)==0) return;
        if (state==PlayModeStateChange.EnteredPlayMode)
        { report.Clear(); errors=0; routine=Checks(); next=0; deadline=EditorApplication.timeSinceStartup+90; }
        if (state==PlayModeStateChange.EnteredEditMode && SessionState.GetInt(Key,0)>=2)
        {
            int code=SessionState.GetInt(Key,0)==2 ? 0 : 1;
            SessionState.EraseInt(Key);
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
    private static void Log(string text,string stack,LogType type)
    { if (routine!=null && (type==LogType.Error || type==LogType.Exception || type==LogType.Assert)) { errors++; report.Add("UNITY ERROR: "+text); } }
    private static void Update()
    {
        if (File.Exists(Output+"/smoke.request") && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            try { File.Delete(Output+"/smoke.request"); }
            catch (IOException) { return; }
            try { CreateAndSmoke(); }
            catch (Exception error) { File.WriteAllText(Output+"/smoke.txt","FAIL: "+error); Debug.LogException(error); }
        }
        if (routine==null || EditorApplication.timeSinceStartup<next) return;
        try
        {
            if (EditorApplication.timeSinceStartup>deadline) throw new Exception("Smoke timed out");
            if (!routine.MoveNext()) { Finish(true); return; }
            next=EditorApplication.timeSinceStartup+(routine.Current is float wait ? wait : .02);
        }
        catch (Exception error) { report.Add("FAIL: "+error); Finish(false); }
    }
    private static void Finish(bool success)
    {
        routine=null; success &= errors==0;
        report.Add(success ? "PASS: single Play Mode smoke, no runtime errors" : "FAIL");
        File.WriteAllLines(Output+"/smoke.txt",report);
        Debug.Log("[OrbitalConstructorSmoke] "+report[report.Count-1]);
        SessionState.SetInt(Key,success ? 2 : 3); EditorApplication.ExitPlaymode();
    }
    private static void Require(bool condition,string message)
    { if (!condition) throw new Exception(message); report.Add("PASS: "+message); }
    private static IEnumerator Checks()
    {
        yield return .25f;
        var lab=Object.FindFirstObjectByType<OrbitalConstructorLab>();
        Require(lab!=null && lab.Player!=null && lab.Editing && Time.timeScale==0,"lab starts in paused editor");
        Require(Object.FindFirstObjectByType<ProductionSceneComposition>()==null && Object.FindFirstObjectByType<EnemySpawner>()==null,"training has no progression composition or automatic spawner");
        float farRadius=0,nearRadius=0,farSpeed=0;
        for (int preset=0;preset<3;preset++)
        {
            lab.LoadPreset(preset);
            Require(lab.Assembly.Nodes.Count==7 && lab.Assembly.Nodes.Count(n=>n.Kind==PartKind.Beam)==4 &&
                lab.Assembly.Nodes.Count(n=>n.Kind==PartKind.Fork)==1 && lab.Assembly.Nodes.Count(n=>n.Kind==PartKind.Blade)==2,
                "preset "+preset+": same four beams, fork and two saws");
            Require(!lab.Assembly.Nodes.Any(n=>n.Kind==PartKind.Hook || n.Kind==PartKind.Shield || n.Kind==PartKind.Electrode) &&
                !lab.Assembly.Bridge(out _,out _) && !lab.Combat.HooksEnabled,"preset excludes hooks and auxiliary combat parts");
            var radii=lab.Assembly.Nodes.Where(n=>n.Kind==PartKind.Blade).Select(n=>n.Point(OrbitalPartCatalog.Get(n.Kind).WorkingCenter).magnitude).OrderBy(r=>r).ToArray();
            report.Add($"GEOMETRY preset {preset}: saw radii {radii[0]:F2}/{radii[1]:F2} m; revolution {lab.TurnSeconds:F2} s; damage {lab.Combat.BladeDamage:F0}");
            Require(lab.MachineRoot.GetComponentsInChildren<Collider2D>(true).Length==0 &&
                lab.MachineRoot.GetComponentsInChildren<Rigidbody2D>(true).Length==0,"machine and guides have no blocking or damage colliders");
            if (preset==0) { farRadius=radii[0]; farSpeed=lab.RotationSpeed; }
            if (preset==1) { nearRadius=radii[0]; Require(nearRadius<farRadius*.5f && lab.RotationSpeed>farSpeed,"compact geometry has smaller radii and faster cached rotation"); }
            if (preset==2) Require(Mathf.Abs(radii[0]-nearRadius)<.01f && radii[1]>radii[0]*2,"mixed geometry has two distinct working radii");
        }
        lab.LoadPreset(0); lab.DebugFreezeRotation=true;
        var saw=lab.Assembly.Nodes.First(n=>n.Kind==PartKind.Blade);
        var def=OrbitalPartCatalog.Get(saw.Kind);
        Vector2 center=saw.Point(def.WorkingCenter);
        var beam=lab.Assembly.Nodes.First(n=>n.Kind==PartKind.Beam);
        var onFrame=lab.SpawnTarget(false,beam.Point(new Vector2(.55f,0)));
        var inside=lab.SpawnTarget(false,center);
        var outside=lab.SpawnTarget(false,center+Vector2.right*(def.DiskRadius+.6f));
        foreach (var enemy in new[] { onFrame,inside,outside })
        { enemy.GetComponent<LabEnemyDummy>().ApplyMode(LabEnemyDummyMode.Static); enemy.SetRuntimeMaxHealth(1000); }
        int calculations=lab.RotationRecalculations;
        lab.Resume(); yield return .08f;
        Require(inside.CurrentHealth==988 && lab.Combat.ContactHits==1,"only the actual saw disk applies one 12-damage hit");
        Require(onFrame.CurrentHealth==1000 && outside.CurrentHealth==1000,"frame and target outside disk remain unharmed");
        Require(lab.ContactSoundPlays>0 && lab.SawHitClip!=null,"real disk contact plays existing impact sound");
        Require(lab.MachineRoot.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name.StartsWith("Saw disk")).All(r=>
            Mathf.Abs(r.transform.localScale.x-def.DiskRadius*2)<.001f),"visible disk diameter agrees with damage radius");
        Require(lab.RotationRecalculations==calculations,"rotation is not recalculated each frame");
        yield return .1f;
        Require(lab.Combat.ContactHits==1,"per-target repeat interval still applies");
        outside.GetComponent<LabEnemyDummy>().MoveRoot(center+Vector2.right*(def.DiskRadius-.1f));
        yield return .08f;
        Require(outside.CurrentHealth==988,"moving target into visible disk produces contact");
        lab.Pause(); lab.ClearEncounter();
        var near=lab.SpawnTarget(false,new Vector2(1.2f,.4f));
        near.GetComponent<LabEnemyDummy>().ApplyMode(LabEnemyDummyMode.Static); near.SetRuntimeMaxHealth(1000);
        lab.Resume();
        for (int angle=0;angle<=360;angle+=15) lab.Combat.Tick(lab.Assembly,lab.gameObject.scene,Vector2.zero,angle);
        Require(near.CurrentHealth==1000 && lab.Combat.ContactHits==0,"full far-saw revolution cannot damage dummy inside the empty inner region");
        lab.ShowCombatTrajectories=false; yield return .05f;
        Require(!lab.MachineRoot.Find("Visual trajectories only (no colliders)").gameObject.activeSelf,"combat trajectory hints can be hidden");
        lab.ShowCombatTrajectories=true; yield return .05f;
        Require(lab.MachineRoot.Find("Visual trajectories only (no colliders)").gameObject.activeSelf,"combat trajectory hints can be shown");
        lab.Pause(); lab.ClearEncounter();
        var walker=lab.SpawnTarget(false,new Vector2(2.2f,0)); walker.SetRuntimeMaxHealth(1000);
        lab.Resume(); yield return .25f;
        Require(walker.transform.position.x<1.95f && walker.CurrentHealth==1000,"existing melee AI freely crosses harmless frame toward player");
        lab.Pause(); float before=lab.RotationSpeed;
        Require(lab.Remove(lab.Assembly.Nodes.First().Id)==7 && lab.RotationSpeed>before,"parent removal clears branch and recalculates speed");
        Require(lab.Install(PartKind.Beam,0,0,0),"manual installation remains available");
        Require(!lab.Install(PartKind.Beam,0,0,0),"occupied socket rejected");
        lab.LoadPreset(1); lab.DummyDistance=2; lab.StartContactProbe(); yield return .2f;
        Require(lab.Alive==1 && !lab.Busy && !lab.Editing,"stationary dummy training starts at chosen distance");
        lab.Pause();
        var dummy=EnemyHealth.ActiveInstances.First(e=>e.gameObject.scene==lab.gameObject.scene);
        Require(dummy.GetComponent<EnemyMovement>().enabled==false,"dummy AI is stationary; real wave AI remains unchanged");
        lab.StartWave(0); yield return .2f;
        Require(lab.Alive==12 && !lab.Busy,"existing melee wave starts");
        lab.StartWave(1); yield return .2f;
        Require(lab.Alive==6 && !lab.Busy && lab.DamageReceived==0,"existing shooter wave starts with clean player state");
        lab.FullReset(); yield return .2f;
        Require(lab.Assembly.Nodes.Count==0 && !lab.Assembly.Bridge(out _,out _) && lab.Alive==0 &&
            !Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Any(p=>p.gameObject.scene==lab.gameObject.scene) &&
            !lab.GetComponent<AudioSource>().isPlaying,"full reset clears construction, enemies, projectiles, temporary effects and contact audio");
        lab.DebugFreezeRotation=false; lab.LoadPreset(2); lab.StartWave(2); yield return .55f;
        lab.Pause(); Capture(lab); yield return .1f;
        Require(errors==0,"no Unity runtime errors");
        report.Add("This smoke verifies geometry and lifecycle only; fun and balance require manual movement/combat testing.");
    }
    private static void Capture(OrbitalConstructorLab lab)
    {
        var camera=lab.ArenaCamera;
        var previous=camera.targetTexture; Rect rect=camera.rect;
        var render=new RenderTexture(1280,960,24);
        var active=RenderTexture.active;
        try
        {
            camera.rect=new Rect(0,0,1,1); camera.targetTexture=render; camera.Render(); RenderTexture.active=render;
            var image=new Texture2D(1280,960,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1280,960),0,0); image.Apply();
            File.WriteAllBytes(Output+"/unity-frame.png",image.EncodeToPNG()); Object.DestroyImmediate(image);
            report.Add("Captured Unity camera: "+Output+"/unity-frame.png");
        }
        finally { camera.targetTexture=previous; camera.rect=rect; RenderTexture.active=active; render.Release(); Object.DestroyImmediate(render); }
    }
}
#endif
