using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BunkerNavigationPlayModeQA
{
    private const string Output = "Assets/_Project/Documentation/BunkerNavigationQA/";
    private const string Session = "BunkerNavigationQA";
    private static int phase;
    private static double until, deadline;
    private static readonly List<string> report = new();
    static BunkerNavigationPlayModeQA()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Session, false))
            { phase=0; until=EditorApplication.timeSinceStartup+3; deadline=until+120; report.Clear(); }
            if (s == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Session,false))
            {
                foreach (string key in Keys) Restore(key);
                PlayerPrefs.Save(); SessionState.SetBool(Session,false);
            }
        };
    }
    private static readonly string[] Keys = { BunkerStationProgressionService.OnboardingKey, BunkerIntroController.ViewedPreferenceKey, "TOTAL_GOLD" };
    private static void Backup(string key)
    { SessionState.SetBool(Session+key+"Exists",PlayerPrefs.HasKey(key)); SessionState.SetInt(Session+key,PlayerPrefs.GetInt(key,0)); }
    private static void Restore(string key)
    { if(SessionState.GetBool(Session+key+"Exists",false)) PlayerPrefs.SetInt(key,SessionState.GetInt(Session+key,0)); else PlayerPrefs.DeleteKey(key); }
    [MenuItem("Tools/Subject42/Bunker/Run Navigation Play Mode QA")]
    public static void Start()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name!="MainMenu" || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Open saved MainMenu in Edit Mode.");
        foreach(string key in Keys) Backup(key);
        PlayerPrefs.DeleteKey(BunkerStationProgressionService.OnboardingKey);
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey,1);
        SessionState.SetBool(Session,true);
        EditorApplication.isPlaying=true;
    }
    private static void Check(bool pass,string message)
    { report.Add((pass?"PASS ":"FAIL ")+message); File.WriteAllLines(Output+"PlayMode.txt",report); if(!pass)throw new Exception(message); }
    private static void Step(BunkerOnboardingStep expected,string description)
        => Check(BunkerStationProgressionService.OnboardingStep==expected,description);
    private static T Find<T>() where T:UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    private static BunkerStation Station(BunkerStationType type) => UnityEngine.Object.FindObjectsByType<BunkerStation>(FindObjectsSortMode.None)
        .Single(s=>new SerializedObject(s).FindProperty("stationType").intValue==(int)type);
    private static void ConfirmCharacter()
    {
        var source=Find<BunkerSelectionSourceHub>().Characters;
        var model=source.BuildModel();
        source.Confirm(model.Entries.First(e=>e.Enabled&&!e.Locked&&e.CanConfirm).Id);
    }
    private static void Tick()
    {
        if(!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Output+"play.request"))
        { File.Delete(Output+"play.request"); try { Start(); }catch(Exception e){File.WriteAllText(Output+"PlayMode.txt",e.ToString());} }
        if(!SessionState.GetBool(Session,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling) return;
        if(EditorApplication.timeSinceStartup<until)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Play Mode timeout phase "+phase);
            switch(phase)
            {
                case 0:
                    Check(Find<BunkerNavigationView>()!=null,"Authored navigation loaded");
                    InspectFloorClearance();
                    Step(BunkerOnboardingStep.Character,"New save targets Character");
                    Capture("STEP-1",BunkerOnboardingStep.Character);
                    var tests=new BunkerOnboardingTests();
                    foreach(Action test in new Action[]{tests.FreshSave_SequentialVisits_CompletionPersists,tests.WeaponFirst_IsRemembered_CharacterConfirmationStillRequired,tests.DirectRun_RepeatedVisits_CannotRestartGuidance})
                    { tests.Backup();try{test();}finally{tests.Restore();} }
                    Check(true,"Persistence/regression assertions passed");
                    Station(BunkerStationType.CharacterSelection).Interact();
                    Step(BunkerOnboardingStep.Character,"Opening Character without confirmation does not advance");
                    ConfirmCharacter(); BunkerContext.Instance.Panels.CloseAll(false);
                    phase++; until=EditorApplication.timeSinceStartup+.3; break;
                case 1:
                    Step(BunkerOnboardingStep.Weapon,"Confirmed Character targets Weapon");
                    Capture("STEP-2",BunkerOnboardingStep.Weapon);
                    ConfirmCharacter(); Step(BunkerOnboardingStep.Weapon,"Repeated Character does not advance");
                    Station(BunkerStationType.WeaponSelection).Interact();
                    Check(BunkerContext.Instance.Panels.IsAnyPanelOpen,"Weapon Station opened its production panel");
                    BunkerContext.Instance.Panels.CloseAll(false);
                    phase++; until=EditorApplication.timeSinceStartup+.3; break;
                case 2:
                    Step(BunkerOnboardingStep.RunGate,"Visited Weapon targets Run Gate");
                    Capture("STEP-3",BunkerOnboardingStep.RunGate);
                    Station(BunkerStationType.StartRun).Interact();
                    Step(BunkerOnboardingStep.RunGate,"Gate transition does not complete onboarding before scene load");
                    phase++; until=EditorApplication.timeSinceStartup+.2; break;
                case 3:
                    if(SceneManager.GetActiveScene().name!="MVP")return;
                    if(RunEndService.Instance==null)return;
                    Step(BunkerOnboardingStep.Complete,"Successful MVP load completes onboarding");
                    RunEndService.Instance.EndRunAfterDeath(); phase++; until=EditorApplication.timeSinceStartup+1; break;
                case 4:
                    if(SceneManager.GetActiveScene().name!="MainMenu"||BunkerContext.Instance==null)return;
                    Step(BunkerOnboardingStep.Complete,"Death returns to a clean Bunker");
                    Capture("NORMAL",BunkerOnboardingStep.Complete);
                    BunkerStationProgressionService.DebugSetOnboarding(BunkerOnboardingStep.Character);
                    Station(BunkerStationType.WeaponSelection).Interact(); BunkerContext.Instance.Panels.CloseAll(false);
                    Step(BunkerOnboardingStep.Character,"Weapon first preserves Character target");
                    ConfirmCharacter(); Step(BunkerOnboardingStep.RunGate,"Earlier Weapon visit is retained");
                    BunkerStationProgressionService.DebugSetOnboarding(BunkerOnboardingStep.Character);
                    Station(BunkerStationType.StartRun).Interact(); phase++; until=EditorApplication.timeSinceStartup+.2; break;
                case 5:
                    if(SceneManager.GetActiveScene().name!="MVP"||RunEndService.Instance==null)return;
                    Step(BunkerOnboardingStep.Complete,"Direct Gate works with defaults and completes guidance");
                    RunStateManager.Instance.EndRun(RunEndReason.Victory);
                    SceneManager.LoadScene("MainMenu"); phase++; until=EditorApplication.timeSinceStartup+1; break;
                case 6:
                    if(SceneManager.GetActiveScene().name!="MainMenu"||BunkerContext.Instance==null)return;
                    Step(BunkerOnboardingStep.Complete,"Victory summary return does not restart onboarding (final boss flow not simulated)");
                    Station(BunkerStationType.WeaponSelection).Interact(); BunkerContext.Instance.Panels.CloseAll(false); ConfirmCharacter();
                    Step(BunkerOnboardingStep.Complete,"Repeat station interactions after completion stay complete");
                    Check(true,"QA finished; restoring prior onboarding, intro and gold preferences");
                    EditorApplication.isPlaying=false; break;
            }
        }
        catch(Exception e) { report.Add("FAIL "+e); File.WriteAllLines(Output+"PlayMode.txt",report); EditorApplication.isPlaying=false; }
    }
    private static void Capture(string name,BunkerOnboardingStep step)
    {
        var camera=Camera.main; var old=camera.transform.position; float size=camera.orthographicSize;
        try
        {
            Find<BunkerNavigationView>().Apply(step,true);
            camera.transform.position=new Vector3(38,-4,-10); camera.orthographicSize=17;
            Render(camera,Output+name+".png",1920,1080);
            // Representative camera zoom and viewport variants use the same authored geometry.
            if(step==BunkerOnboardingStep.Character)
            {
                var follow=Find<CameraFollow>(); camera.transform.position=follow.target.position+new Vector3(0,0,-10);
                camera.orthographicSize=8;
                Render(camera,Output+"Spawn-16x9.png",1600,900);
                Render(camera,Output+"Spawn-4x3.png",1200,900);
                Render(camera,Output+"Spawn-21x9.png",2100,900);
                camera.orthographicSize=5; Render(camera,Output+"Spawn-Zoom.png",1600,900);
            }
        }
        finally{camera.transform.position=old;camera.orthographicSize=size;}
    }
    private static void InspectFloorClearance()
    {
        Physics2D.SyncTransforms();
        var collisions=new HashSet<string>();
        foreach(var filter in Find<BunkerNavigationView>().GetComponentsInChildren<MeshFilter>())
        {
            if(filter.GetComponent<TMPro.TMP_Text>()!=null)continue;
            var v=filter.sharedMesh.vertices; var markers=filter.sharedMesh.uv2;
            for(int i=0;i<v.Length;i+=4)
            {
                if(markers[i].x>0)continue;
                Vector2 a=filter.transform.TransformPoint((v[i]+v[i+1])*.5f);
                Vector2 b=filter.transform.TransformPoint((v[i+2]+v[i+3])*.5f);
                int count=Mathf.CeilToInt(Vector2.Distance(a,b)/.2f);
                for(int n=0;n<=count;n++)
                {
                    Vector2 p=Vector2.Lerp(a,b,(float)n/count);
                    foreach(var c in Physics2D.OverlapCircleAll(p,.25f))
                        if(!c.isTrigger && c.attachedRigidbody==null && c.gameObject.layer!=9)
                            collisions.Add(filter.name+" at "+p+" overlaps "+c.name+" parent="+c.transform.parent.name+" bounds="+c.bounds);
                }
            }
        }
        File.WriteAllLines(Output+"Clearance.txt",collisions.Count==0?new[]{"PASS Route centerlines have .25 world-unit solid-collider clearance."}:collisions);
    }
    private static void Render(Camera camera,string path,int width,int height)
    {
        var rt=new RenderTexture(width,height,24); var previous=RenderTexture.active; var target=camera.targetTexture;
        var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally{camera.targetTexture=target;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);}
    }
}
