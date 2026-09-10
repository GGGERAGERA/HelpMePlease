using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class SurfaceVisualLabEditor
{
    const string QASession="SurfaceVisualLab.QA";
    static readonly List<string> qaErrors=new();
    static SurfaceVisualLab labQA;
    static int qaStep;
    static double qaNext;
    static Vector3[] fixedPositions;
    [InitializeOnLoadMethod]
    static void RegisterQA()
    {
        EditorApplication.update+=TickQA;
        EditorApplication.playModeStateChanged+=state=>
        {
            if(!SessionState.GetBool(QASession,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {qaStep=0;labQA=null;qaErrors.Clear();qaNext=EditorApplication.timeSinceStartup+2;Application.logMessageReceived+=QALog;}
            if(state==PlayModeStateChange.EnteredEditMode)
            {SessionState.SetBool(QASession,false);Application.logMessageReceived-=QALog;File.WriteAllText(Output+"/complete.txt","Returned to Edit Mode");}
        };
    }
    [MenuItem("Tools/Subject42/Surface Visual Lab/Capture 12 comparisons and live combat")]
    public static void StartQA()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before QA.");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene changes before QA.");
        EditorSceneManager.OpenScene(ScenePath);
        Directory.CreateDirectory(Output);
        if(File.Exists(Output+"/complete.txt"))File.Delete(Output+"/complete.txt");
        if(File.Exists(Output+"/qa-failure.txt"))File.Delete(Output+"/qa-failure.txt");
        SessionState.SetBool(QASession,true);EditorApplication.isPlaying=true;
    }
    static void QALog(string text,string trace,LogType type)
    {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)qaErrors.Add(text+"\n"+trace);}
    static void TickQA()
    {
        if(!SessionState.GetBool(QASession,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<qaNext)return;
        try
        {
            if(labQA==null)labQA=Object.FindFirstObjectByType<SurfaceVisualLab>();
            if(labQA==null||!labQA.Ready){qaNext=EditorApplication.timeSinceStartup+.5;return;}
            labQA.ShowHelp=false;labQA.FollowPlayer=false;
            if(qaStep<6)
            {
                int scenario=qaStep/2;
                if(qaStep%2==0)
                {Time.timeScale=1;labQA.SetScenario(scenario);qaNext=EditorApplication.timeSinceStartup+(scenario==0?3.5:scenario==2?1.35:.5);}
                else
                {
                    Time.timeScale=0;
                    if(scenario==2)foreach(var fx in labQA.ImpactSamples)fx.Simulate(.12f,true,true,true);
                    for(int p=0;p<4;p++){labQA.SelectPreset(p);CaptureQA($"{p+1}-{new[]{"solo","crowd","combat"}[scenario]}");}
                    qaNext=EditorApplication.timeSinceStartup+.1;
                }
                qaStep++;return;
            }
            if(qaStep==6)
            {Time.timeScale=1;labQA.SelectPreset(3);labQA.SetScenario(3);qaStep++;qaNext=EditorApplication.timeSinceStartup+1.1;return;}
            if(qaStep==7)
            {
                Time.timeScale=0;CaptureQA("4-dense-live");
                var text=$"Play Mode camera renders: 12 matched comparisons + live crowd.\nRings={labQA.Station.Rings.Count}, modules={labQA.Station.Modules.Count}, actual damage hits={labQA.Hits}\n";
                text+=$"Player active={labQA.Player.activeInHierarchy}; enemies alive={labQA.Enemies.Count(e=>e!=null&&e.activeInHierarchy)}; XP={Object.FindObjectsByType<ExperiencePickup>(FindObjectsSortMode.None).Length}; FX samples={labQA.ImpactSamples.Length}\n";
                text+=$"ORBITAL state valid={labQA.Station.ValidateState(out string error)} {error}\nErrors={qaErrors.Count}\n"+string.Join("\n",qaErrors);
                File.WriteAllText(Output+"/qa.txt",text);
                // Prove that repeat use restores destroyed fixture references, not just surviving actors.
                Object.DestroyImmediate(labQA.Enemies[0]);Object.DestroyImmediate(labQA.Fixtures[0]);
                Object.DestroyImmediate(labQA.PickupGroup.transform.GetChild(0).gameObject);
                labQA.SetScenario(2);
                if(labQA.Enemies.Count(e=>e!=null&&e.activeInHierarchy)!=24 || labQA.Fixtures.Any(f=>f==null||f.GetComponent<WorldBreakable>().IsBroken))
                    throw new InvalidOperationException("Reset did not restore production fixtures.");
                fixedPositions=labQA.Enemies.Take(24).Select(e=>e.transform.position).ToArray();
                Time.timeScale=1;qaNext=EditorApplication.timeSinceStartup+2;qaStep++;return;
            }
            if(qaStep==8)
            {
                if(labQA.Enemies.Take(24).Where((e,i)=>(e.transform.position-fixedPositions[i]).sqrMagnitude>.0001f).Any())
                    throw new InvalidOperationException("Comparison targets drift under production impulse attacks.");
                File.AppendAllText(Output+"/qa.txt","\nPASS reset after destroyed enemy/crate/XP; PASS comparison target positions stable under combat for 2 seconds.\n");
                if(qaErrors.Count>0)File.AppendAllText(Output+"/qa.txt","Errors after reset: "+string.Join("\n",qaErrors));
                Time.timeScale=1;EditorApplication.isPlaying=false;qaStep++;
            }
        }
        catch(Exception e)
        {File.WriteAllText(Output+"/qa-failure.txt",e.ToString());Time.timeScale=1;EditorApplication.isPlaying=false;}
    }
    static void CaptureQA(string name)
    {
        var camera=labQA.GameplayCamera;
        var previous=RenderTexture.active;
        var oldTarget=camera.targetTexture;
        var target=RenderTexture.GetTemporary(768,432,24,RenderTextureFormat.ARGB32);
        target.filterMode=FilterMode.Point;camera.targetTexture=target;
        var raw=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        var output=new Texture2D(target.width*2,target.height*2,TextureFormat.RGB24,false);
        try
        {
            camera.Render();RenderTexture.active=target;raw.ReadPixels(new Rect(0,0,target.width,target.height),0,0);raw.Apply();
            var source=raw.GetPixels32();var pixels=new Color32[source.Length*4];
            for(int y=0;y<target.height*2;y++)for(int x=0;x<target.width*2;x++)pixels[y*target.width*2+x]=source[(y/2)*target.width+x/2];
            output.SetPixels32(pixels);output.Apply();File.WriteAllBytes(Output+"/"+name+".png",output.EncodeToPNG());
        }
        finally{camera.targetTexture=oldTarget;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(raw);Object.DestroyImmediate(output);}
    }
}
