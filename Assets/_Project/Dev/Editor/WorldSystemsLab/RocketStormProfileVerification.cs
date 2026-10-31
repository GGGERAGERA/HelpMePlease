#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Opt-in, bounded production profiling; never runs on import or during normal play.
[InitializeOnLoad]
public static class RocketStormProfileVerification
{
    const string Output = "Artifacts/GeneratedQA/RocketStormProfile";
    const string Key = "Subject42.RocketProfile.Active", Any = "Subject42.Verification.AnyRunActive";
    const string Mvp = "Assets/_Project/Scenes/MainBuild/MVP.unity";
    static IEnumerator routine;
    static double readyAfter = EditorApplication.timeSinceStartup + 2, deadline, next;
    static bool oldProfiler, oldEditor, oldDeep, oldGpu;
    static int oldFrameCount;
    static PropertyInfo frameCount;
    static string label;
    static int errors;
    static RocketStormProfileVerification()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Key,false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                label = SessionState.GetString(Key+".Label","before"); errors=0;
                deadline=EditorApplication.timeSinceStartup+45;
                try { BeginRun(); SceneManager.LoadSceneAsync(Mvp); routine=Checks(); }
                catch (Exception e) { Finish(e); }
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseBool(Key); SessionState.EraseBool(Any);
                var cleanup=CoreTestSupport.CleanupPlayMode(); while(cleanup.MoveNext()) { }
                EditorSceneManager.OpenScene(Mvp);
            }
        };
        Application.logMessageReceived += (message,stack,type) =>
        {
            if (routine != null && type is LogType.Error or LogType.Exception or LogType.Assert)
            { errors++; File.AppendAllText(Output+"/"+label+"-errors.txt",message+"\n"+stack+"\n"); }
        };
    }
    static void Poll()
    {
        string request = Output + "/editor.request";
        bool busy=EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Any,false);
        if (busy) readyAfter=EditorApplication.timeSinceStartup+2;
        if (File.Exists(request) && !busy && EditorApplication.timeSinceStartup >= readyAfter)
        {
            string command = File.ReadAllText(request).Trim(); File.Delete(request);
            try
            {
                if (command is "before" or "after") Run(command);
                else if (command == "reuse")
                {
                    var tests=new PooledVisualConfigurationTests();
                    var result=new List<string>();
                    foreach(var method in typeof(PooledVisualConfigurationTests).GetMethods().Where(m=>m.GetCustomAttributes(typeof(NUnit.Framework.TestAttribute),true).Length>0))
                    { try { method.Invoke(tests,null); result.Add("PASS "+method.Name); } catch(Exception e) { result.Add("FAIL "+method.Name+": "+(e.InnerException??e)); } }
                    File.WriteAllLines(Output+"/reuse-result.txt",result);
                }
                else if (command == "api")
                {
                    var types = new[] { typeof(ProfilerDriver), typeof(UnityEditor.Profiling.RawFrameDataView), typeof(UnityEditor.Profiling.FrameDataView), typeof(Unity.Profiling.ProfilerRecorder) }.Concat(typeof(Editor).Assembly.GetTypes().Where(t=>t.Name.Contains("ProfilerUserSettings")));
                    File.WriteAllText(Output + "/api.txt", string.Join("\n",types.Select(t => t.FullName + "\n" + string.Join("\n", t.GetMembers(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance).Select(m=>m.ToString())))));
                }
            }
            catch (Exception e) { File.WriteAllText(Output + "/error.txt", e.ToString()); }
        }
        if (routine == null || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Bounded profiling timed out.");
            if (!routine.MoveNext()) { Finish(null); return; }
            next=EditorApplication.timeSinceStartup+(routine.Current is float delay ? delay : .02);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Run(string name)
    {
        for (int i=0;i<SceneManager.sceneCount;i++) if (SceneManager.GetSceneAt(i).isDirty) throw new Exception("Preserve the edited scene before profiling.");
        CoreTestSupport.PreservePreferences();
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        SessionState.SetString(Key+".Label",name); SessionState.SetBool(Key,true); SessionState.SetBool(Any,true);
        EditorApplication.EnterPlaymode();
    }
    static void BeginRun()
    {
        GameplayRandom.Begin(5);
        RunStateManager.EnsureExists().BeginNewRun(
            AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"),null,
            AssetDatabase.LoadAssetAtPath<StageProfileData>("Assets/_Project/Data/Stages/StageProfiles/StageProfile_01.asset"),
            AssetDatabase.LoadAssetAtPath<WorldRuleData>(AssetDatabase.GUIDToAssetPath("f1012da158e24a20bb764c93aeee5266")),
            AssetDatabase.LoadAssetAtPath<LocalAnomalyData>("Assets/_Project/Data/Anomalies/LocalAnomaly_Stasis.asset"),true);
    }
    static IEnumerator Checks()
    {
        while (SceneManager.GetActiveScene().name != "MVP" || ProductionSceneComposition.Active is not { IsReady:true } || SceneTransitionOverlay.IsTransitioning) yield return .02f;
        var spawner=Object.FindFirstObjectByType<WorldEventSpawner>();
        var storm=spawner.SpawnedEvents.OfType<RocketStormEvent>().First();
        var player=Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        player.GetComponent<PlayerHealth>().DebugInvulnerable=true;
        player.GetComponent<CharacterMovement2D>().MovementIntent=()=>Vector2.zero;
        foreach(var enemies in Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None)) enemies.StopSpawning();
        foreach(var enemy in EnemyHealth.ActiveInstances.ToArray()) Object.Destroy(enemy.gameObject);
        player.transform.position=storm.transform.position;
        var body=player.GetComponent<Rigidbody2D>(); body.position=storm.transform.position; body.linearVelocity=Vector2.zero;
        var camera=Object.FindFirstObjectByType<CameraFollow>(); camera.transform.position=player.transform.position+camera.offset;
        Physics2D.SyncTransforms();
        yield return .4f;
        int oldVsync=QualitySettings.vSyncCount, oldTarget=Application.targetFrameRate;
        QualitySettings.vSyncCount=0; Application.targetFrameRate=60;
        oldProfiler=ProfilerDriver.enabled; oldEditor=ProfilerDriver.profileEditor; oldDeep=ProfilerDriver.deepProfiling; oldGpu=ProfilerDriver.profileGPU;
        var userSettings=typeof(Editor).Assembly.GetTypes().FirstOrDefault(t=>t.Name=="ProfilerUserSettings");
        frameCount=userSettings?.GetProperty("frameCount",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
        if(frameCount!=null) { oldFrameCount=(int)frameCount.GetValue(null); frameCount.SetValue(null,2000); }
        ProfilerDriver.enabled=false; ProfilerDriver.profileEditor=false; ProfilerDriver.deepProfiling=false;
        ProfilerDriver.profileGPU=ProfilerDriver.isGPUProfilerSupported;
        ProfilerDriver.ClearAllFrames(); ProfilerDriver.enabled=true;
        var probe=storm.gameObject.AddComponent<RocketStormProfileProbe>(); probe.Storm=storm; probe.Recording=true;
        if(!spawner.TryStartProductionEvent(storm)) throw new Exception("Stock production event did not start.");
        // One mass wave only: stock 2s preparation, 2s warning/flight, first explosions.
        while(probe.ImpactCount==0) yield return .01f;
        yield return .45f;
        probe.Recording=false;
        yield return .05f; ProfilerDriver.enabled=false;
        ProfilerDriver.SaveProfile(Output+"/"+label+".data");
        DumpFrames(label);
        File.WriteAllLines(Output+"/"+label+"-observations.csv",new[]{"frame,stage,dt_seconds,event_seconds"}.Concat(probe.Frames.Select(f=>$"{f.frame},{f.stage},{f.dt.ToString("R",CultureInfo.InvariantCulture)},{f.elapsed.ToString("R",CultureInfo.InvariantCulture)}")));
        File.WriteAllLines(Output+"/"+label+"-manifest.txt",new[]{ $"seed=5; viewport=1600x900; vsync=0; targetFPS=60; CPU deep profile=false; frameBuffer={frameCount?.GetValue(null)}", $"storm={storm.transform.position}; settings="+JsonUtility.ToJson(typeof(RocketStormEvent).GetField("settings",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(storm)), $"warnings={probe.WarningCount}; falling={probe.FallingCount}; impacts={probe.ImpactCount}; marker/damage-centre/diameter mismatches={probe.Mismatches}; errors={errors}" }.Concat(probe.Manifest).Concat(probe.ImpactTargets.Select(p=>$"impact={p.x:R},{p.y:R}")));
        if(probe.WarningCount==0 || probe.WarningCount!=probe.ImpactCount || probe.Mismatches!=0) throw new Exception("Warning/impact manifest mismatch.");
        storm.Cancel();
        QualitySettings.vSyncCount=oldVsync; Application.targetFrameRate=oldTarget;
        RestoreProfiler();
        if(errors>0) throw new Exception("Unity errors during profiling: "+errors);
    }
    static void RestoreProfiler()
    {
        ProfilerDriver.enabled=oldProfiler; ProfilerDriver.profileEditor=oldEditor; ProfilerDriver.deepProfiling=oldDeep; ProfilerDriver.profileGPU=oldGpu;
        if(frameCount!=null) frameCount.SetValue(null,oldFrameCount);
    }
    static void Finish(Exception exception)
    {
        routine=null;
        if(exception!=null) { ProfilerDriver.enabled=false; RestoreProfiler(); }
        File.WriteAllText(Output+"/"+label+"-result.txt",exception?.ToString() ?? "PASS: one bounded production mass wave; markers match impacts; no Unity errors.");
        EditorApplication.ExitPlaymode();
    }
    static void DumpFrames(string name)
    {
        var frames=new List<string>{"frame,stage,main_ms,gpu_ms,gc_bytes,render_ms"};
        var scopes=new List<string>{"frame,stage,scope,calls,total_ms,self_ms"};
        var peaks=new List<(float ms,int frame)>();
        int lastStage=0;
        for(int f=ProfilerDriver.firstFrameIndex;f<=ProfilerDriver.lastFrameIndex;f++)
        {
            using var view=ProfilerDriver.GetRawFrameDataView(f,0);
            if(!view.valid) continue;
            int stage=-1; long gc=0;
            var grouped=new Dictionary<string,(int count,double total,double self)>();
            for(int i=0;i<view.sampleCount;i++)
            {
                string sample=view.GetSampleName(i);
                if(sample.StartsWith("RocketProfile."))
                { int s=Array.IndexOf(new[]{"RocketProfile.Before","RocketProfile.Warnings","RocketProfile.PreImpact","RocketProfile.Explosions"},sample); if(s>=0) stage=s; }
                if(sample=="GC.Alloc" && view.GetSampleMetadataCount(i)>0) gc+=view.GetSampleMetadataAsLong(i,0);
                double time=view.GetSampleTimeMs(i), children=0;
                int end=i+view.GetSampleChildrenCountRecursive(i)+1;
                for(int c=i+1;c<end;c+=view.GetSampleChildrenCountRecursive(c)+1) children+=view.GetSampleTimeMs(c);
                if(grouped.TryGetValue(sample,out var v)) grouped[sample]=(v.count+1,v.total+time,v.self+Math.Max(0,time-children));
                else grouped.Add(sample,(1,time,Math.Max(0,time-children)));
            }
            if(stage<0) continue; lastStage=stage;
            float render=0;
            for(int thread=1;thread<32;thread++)
            { using var data=ProfilerDriver.GetRawFrameDataView(f,thread); if(!data.valid) break; if(data.threadName.Contains("Render")) { render=data.frameTimeMs; break; } }
            frames.Add(FormattableString.Invariant($"{f},{stage},{view.frameTimeMs:R},{view.frameGpuTimeMs:R},{gc},{render:R}"));
            foreach(var pair in grouped) scopes.Add(FormattableString.Invariant($"{f},{stage},\"{pair.Key.Replace("\"","'")}\",{pair.Value.count},{pair.Value.total:0.######},{pair.Value.self:0.######}"));
            peaks.Add((view.frameTimeMs,f));
        }
        File.WriteAllLines(Output+"/"+name+"-frames.csv",frames);
        File.WriteAllLines(Output+"/"+name+"-scopes.csv",scopes);
        foreach(var peak in peaks.OrderByDescending(p=>p.ms).Take(6))
        {
            using var view=ProfilerDriver.GetRawFrameDataView(peak.frame,0);
            File.WriteAllLines(Output+"/"+name+"-peak-"+peak.frame+".csv",new[]{"sample,total_ms,children,name"}.Concat(Enumerable.Range(0,view.sampleCount).Select(i=>FormattableString.Invariant($"{i},{view.GetSampleTimeMs(i):0.######},{view.GetSampleChildrenCountRecursive(i)},\"{view.GetSampleName(i).Replace("\"","'")}\""))));
        }
    }
}
#endif
