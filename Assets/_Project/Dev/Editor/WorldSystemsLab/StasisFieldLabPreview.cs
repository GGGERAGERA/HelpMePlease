#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit local preview and a short stasis-only check. No build, production writes or test suites.
[InitializeOnLoad]
public static class StasisFieldLabPreview
{
    public const string Output = "Artifacts/GeneratedQA/StasisField";
    const string Pending = "Subject42.StasisPreview.Pending";
    static IEnumerator routine;
    static double next, deadline;
    static string report;
    static WorldSystemsLabController framingOwner;
    static CameraFollow framedCamera;
    static StasisFieldLabPreview()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += state =>
        {
            if(state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending,false))
            { SessionState.EraseBool(Pending); routine=Preview(); deadline=EditorApplication.timeSinceStartup+30; }
        };
    }
    [MenuItem("Tools/Subject42/Dev/WorldSystemsLab/Preview Stasis Art (1600x900)")]
    public static void Open()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        for(int i=0;i<SceneManager.sceneCount;i++)
            if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve unsaved scene edits before opening the lab.");
        EditorSceneManager.OpenScene(WorldSystemsLabController.ScenePath);
        GameView(); Directory.CreateDirectory(Output);
        SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
    }
    static void GameView()
    {
        var assembly=typeof(Editor).Assembly;
        var type=assembly.GetType("UnityEditor.GameView");
        var window=EditorWindow.GetWindow(type); window.Show(); window.Focus();
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.FlattenHierarchy;
        var sizes=sizesType.GetProperty("instance",flags).GetValue(null);
        var groupType=sizesType.GetProperty("currentGroupType",flags).GetValue(sizes);
        var group=sizesType.GetMethod("GetGroup",flags).Invoke(sizes,new[]{groupType});
        var gt=group.GetType(); int count=(int)gt.GetMethod("GetTotalCount",flags).Invoke(group,null), index=-1;
        for(int i=0;i<count;i++)
        {
            var size=gt.GetMethod("GetGameViewSize",flags).Invoke(group,new object[]{i}); var st=size.GetType();
            if((int)st.GetProperty("width",flags).GetValue(size)==1600 && (int)st.GetProperty("height",flags).GetValue(size)==900) {index=i;break;}
        }
        if(index<0)
        {
            var sizeType=assembly.GetType("UnityEditor.GameViewSize"); var enumType=assembly.GetType("UnityEditor.GameViewSizeType");
            var size=Activator.CreateInstance(sizeType,flags,null,new[]{Enum.ToObject(enumType,1),(object)1600,900,"1600 x 900 Stasis"},null);
            gt.GetMethod("AddCustomSize",flags).Invoke(group,new[]{size}); index=count;
        }
        type.GetProperty("selectedSizeIndex",flags).SetValue(window,index);
        window.Repaint();
    }
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating) return;
        if(framingOwner!=null && (!EditorApplication.isPlaying || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) ||
            Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D) ||
            framingOwner.GetComponent<WorldSystemsLabOrbitExperiment>().ActiveAnomaly!=WorldSystemsLabOrbitExperiment.Anomaly.Blue))
        { framedCamera?.EndWorldBoundsFocus(framingOwner); framingOwner=null; framedCamera=null; }
        string request=Output+"/preview.request";
        if(File.Exists(request))
        {
            string command=File.ReadAllText(request).Trim();File.Delete(request);
            try
            {
                if(command=="open")Open();
                else if(command=="stop")EditorApplication.ExitPlaymode();
                else if(command=="capture" && EditorApplication.isPlaying)
                {ScreenCapture.CaptureScreenshot(Output+"/stasis-gameview-1600x900.png");File.WriteAllText(Output+"/capture.txt",$"Game View {Screen.width}x{Screen.height}; Play={EditorApplication.isPlaying}");}
            }
            catch(Exception e) {File.WriteAllText(Output+"/result.txt","FAIL: "+e); Debug.LogException(e);}
        }
        if(routine==null || EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Stasis preview exceeded 30-second limit.");
            if(!routine.MoveNext()){routine=null;return;}
            next=EditorApplication.timeSinceStartup+(routine.Current is float delay?delay:.03);
        }
        catch(Exception e){routine=null;File.WriteAllText(Output+"/result.txt",report+"\nFAIL: "+e);Debug.LogException(e);}
    }
    static void Check(bool condition,string description)
    {if(!condition)throw new Exception(description);report+="PASS: "+description+"\n";}
    static IEnumerator Preview()
    {
        report="Stasis visual prototype / existing WorldSystemsLab / Play Mode\n";
        // Entering Play can recompile scripts and rebuild Game View; restore the requested size afterwards.
        GameView();
        while(Screen.width!=1600 || Screen.height!=900) yield return .03f;
        var world=Object.FindFirstObjectByType<WorldSystemsLabController>();
        var experiment=world.GetComponent<WorldSystemsLabOrbitExperiment>();
        experiment.Begin(); while(!experiment.Ready)yield return .03f;
        var lab=experiment.Orbit;
        while(lab.Segmented.Preset!="Multi-Layer")yield return .03f;
        int blue=Enumerable.Range(0,experiment.ZoneCount).Single(i=>experiment.ZoneKind(i)==WorldSystemsLabOrbitExperiment.Anomaly.Blue);
        experiment.MoveTo(-1);lab.SetPaused(false); experiment.TickEffects(0);
        float walking=lab.Player.GetComponent<CharacterMovement2D>().WalkingSpeed;
        var site=world.SpawnedSites.Single(s=>s.AnomalyZone.AnomalyType==LocalAnomalyType.Stasis);
        var box=(BoxCollider2D)site.AnomalyZone.FocusArea; Vector2 size=box.size, offset=box.offset; bool enabled=box.enabled;
        var presentation=site.GetComponent<StasisFieldLabVisual>();
        Check(presentation!=null,"Art attached to blue site in lab");
        var sourceSprites=new SerializedObject(world).FindProperty("stasisDebrisSprites");
        report+=$"Debris: {presentation.DebrisCount}; serialized sources: {sourceSprites.arraySize}; "+string.Join(",",Enumerable.Range(0,sourceSprites.arraySize).Select(i=>sourceSprites.GetArrayElementAtIndex(i).objectReferenceValue?.name ?? "null"))+"\n";
        Check(presentation.DebrisCount==8,"Eight existing asset fragments, with visual transforms only");
        var art=site.GetComponentsInChildren<SpriteRenderer>().Single(r=>r.gameObject.name.StartsWith("STASIS /"));
        Check(art.GetComponentsInChildren<Collider2D>().Length==0,"Art has no additional collision geometry");
        report+=$"Original focus size: {size}; offset {offset}; world centre {experiment.ZoneCenter(blue)}\n";
        experiment.MoveTo(blue);experiment.TickEffects(0);
        Check(Mathf.Abs(lab.Player.GetComponent<CharacterMovement2D>().WalkingSpeed/walking-.65f)<.002f,"Player movement x0.65 inside blue");
        lab.Station.State.RotationEnergy=100; float phase=lab.Station.Rings[0].Phase;
        lab.Segmented.TickManualRotation(1,true,false,true);
        Check(Mathf.Abs(Mathf.DeltaAngle(phase,lab.Station.Rings[0].Phase)-45)<.01f,"Q/E remains 45 degrees per second (x0.5)");
        Check(Mathf.Abs(lab.Station.RotationEnergy-75)<.01f,"Q/E energy cost remains 25 per second");
        lab.SelectedEnemy=Array.FindIndex(lab.EnemyPrefabs,p=>p.GetComponent<EnemyChaseMovement>()!=null);
        Check(lab.SelectedEnemy>=0,"Existing chase enemy fixture found"); lab.Spawn(1);
        var enemy=lab.LabEnemies.First();enemy.transform.position=experiment.ZoneCenter(blue)+new Vector2(3,1);
        experiment.TickEffects(0);
        var speed=typeof(EnemyChaseMovement).GetField("anomalySpeedMultiplier",BindingFlags.NonPublic|BindingFlags.Instance);
        Check(Mathf.Abs((float)speed.GetValue(enemy.GetComponent<EnemyChaseMovement>())-.65f)<.002f,"Enemy movement x0.65 inside blue");
        enemy.transform.position=new Vector2(1000,1000); experiment.TickEffects(0);
        Check(Mathf.Abs((float)speed.GetValue(enemy.GetComponent<EnemyChaseMovement>())-1f)<.002f,"Enemy speed restores on exit");
        experiment.MoveTo(-1); experiment.TickEffects(0);
        Check(Mathf.Abs(lab.Player.GetComponent<CharacterMovement2D>().WalkingSpeed-walking)<.002f && experiment.RotationSpeedFactor==1,"Player movement and rotation restore on exit");
        Check(box.size==size && box.offset==offset && box.enabled==enabled,"Original zone box, offset and enabled state unchanged");
        Check(site.GetComponentsInChildren<StasisFieldLabVisual>().Length==1 && world.SpawnedSites.All(s=>s==site || s.GetComponent<StasisFieldLabVisual>()==null),"Only blue site receives new presentation");
        lab.ClearSandboxArena();experiment.MoveTo(blue);
        // A close view of the real boundary; keep the weapons large enough to judge readability.
        Vector2 worldSize=Vector2.Scale(size,box.transform.lossyScale);
        float available=(Screen.width-experiment.PanelRect.width-40)/Screen.width;
        float aspect=(float)Screen.width/Screen.height;
        float zoom=Mathf.Min(8f,Mathf.Max(5f,worldSize.y*.5f));
        Vector2 previewPoint=presentation.PreviewPosition;
        var playerBody=world.Player.GetComponent<Rigidbody2D>();
        if(playerBody!=null) {playerBody.position=previewPoint;playerBody.linearVelocity=Vector2.zero;}
        world.Player.position=new Vector3(previewPoint.x,previewPoint.y,world.Player.position.z);
        Physics2D.SyncTransforms();
        Vector2 centre=previewPoint+Vector2.right*(zoom*aspect*(1-available));
        lab.CameraRig.BeginWorldBoundsFocus(world,centre,zoom);
        framingOwner=world; framedCamera=lab.CameraRig;
        experiment.TickEffects(0);
        yield return .15f;
        Check(Screen.width==1600 && Screen.height==900,"Actual Game View is 1600x900");
        Check(!ShaderUtil.GetShaderMessages(Shader.Find("Subject42/Lab/StasisField")).Any(m=>m.severity.ToString()=="Error"),"Stasis shader has no compilation errors");
        // Observe actual running presentation on both sides of the boundary; no gameplay time changes.
        yield return 6f;
        Check(presentation.BoundaryCrossings>=2,"Moving fragments actually cross the original zone boundary");
        Check(presentation.ObservedEntrySlowdown,"Crossing fragments settle below x0.25 speed inside");
        Check(presentation.ObservedExitRecovery,"Crossing fragments recover above x0.85 speed outside");
        Check(art.GetComponentsInChildren<SpriteRenderer>().All(r=>!r.forceRenderingOff),"Floor and fragments stay visible after the debug-art refresh");
        report+=$"Observed fragment boundary crossings: {presentation.BoundaryCrossings}\n";
        // Spawn the existing enemies just before capture so their silhouettes remain separate.
        lab.SetEnemiesInvincible(true); lab.Spawn(5);
        int enemyIndex=0;
        foreach(var visibleEnemy in lab.LabEnemies)
        {
            float a=(enemyIndex++*72+20)*Mathf.Deg2Rad;
            visibleEnemy.transform.position=previewPoint+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*6.5f;
        }
        experiment.TickEffects(0);
        yield return .15f;
        ScreenCapture.CaptureScreenshot(Output+"/stasis-gameview-1600x900.png");
        yield return 1.1f;
        ScreenCapture.CaptureScreenshot(Output+"/stasis-animation-1600x900.png");
        File.WriteAllText(Output+"/result.txt",report+"Play Mode left running for visual review.\n");
    }
}
#endif
