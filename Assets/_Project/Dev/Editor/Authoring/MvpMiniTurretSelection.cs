#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class MvpMiniTurretSelection
{
    public const string Output="Artifacts/GeneratedQA/MvpMiniTurret";
    public const string OldPath="Assets/_Project/prefabs/Enemies/p_EnemyTurret1.prefab", ApprovedPath="Assets/_Project/prefabs/Enemies/p_EnemyTurret2.prefab";
    static bool editModeReady=!EditorApplication.isPlaying;
    static MvpMiniTurretSelection()
    {
        EditorApplication.update+=Poll;
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state==PlayModeStateChange.EnteredEditMode) editModeReady=true;
            if(state==PlayModeStateChange.ExitingPlayMode || state==PlayModeStateChange.EnteredPlayMode) editModeReady=false;
        };
        ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results());
    }
    static void Poll()
    {
        string request=Output+"/editor.request";
        if(!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string command=File.ReadAllText(request).Trim();
        if(command!="audit" && (!editModeReady || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode))
        { if(EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode(); return; }
        File.Delete(request);
        try
        {
            if(command=="audit") Audit();
            else if(command=="apply") Apply();
            else if(command=="smoke")
            {
                SessionState.SetBool("MvpMiniTurret.Smoke",true);
                ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter
                    {testMode=TestMode.EditMode,testNames=new[]{"MvpMiniTurretSmoke.AppearanceShootAndDestruction"}}));
            }
        }
        catch(Exception e) { File.WriteAllText(Output+"/error.txt",e.ToString()); Debug.LogException(e); }
    }
    public static void Apply()
    {
        Directory.CreateDirectory(Output);
        var old=PrefabUtility.LoadPrefabContents(OldPath);
        var approved=PrefabUtility.LoadPrefabContents(ApprovedPath);
        try
        {
            old.transform.position=approved.transform.position=Vector3.zero;
            Require(Gameplay(old)==Gameplay(approved),"approved prefab already matches old health, damage, AI, projectile, identity, rigidbody and colliders");
            var oldAi=new SerializedObject(old.GetComponent<TurretEnemyBehaviour>());
            var ai=new SerializedObject(approved.GetComponent<TurretEnemyBehaviour>());
            var oldPivot=(Transform)oldAi.FindProperty("aimPivot").objectReferenceValue;
            var oldMuzzle=(Transform)oldAi.FindProperty("firePoint").objectReferenceValue;
            var pivot=(Transform)ai.FindProperty("aimPivot").objectReferenceValue;
            var muzzle=(Transform)ai.FindProperty("firePoint").objectReferenceValue;
            Vector3 wanted=oldMuzzle.position-oldPivot.position, actual=muzzle.position-pivot.position;
            Require(Mathf.Abs(actual.x)>.01f && Mathf.Abs(actual.y)<.0001f && Mathf.Abs(wanted.y)<.0001f,"existing barrel rigs are horizontal");
            Vector3 scale=pivot.localScale; scale.x*=wanted.x/actual.x; pivot.localScale=scale;
            pivot.position=oldPivot.position;
            foreach(float angle in new[]{0f,90f,180f,270f})
            {
                oldPivot.rotation=pivot.rotation=Quaternion.Euler(0,0,angle);
                Require(Vector2.Distance(oldMuzzle.position,muzzle.position)<.0001f,"same physical firePoint at "+angle+" degrees");
            }
            oldPivot.rotation=pivot.rotation=Quaternion.identity;
            var flash=approved.GetComponent<EnemyWhiteFlash>();
            var body=approved.GetComponentsInChildren<SpriteRenderer>().Single(s=>s.name=="MiniTurret4UpP1");
            var flashSettings=new SerializedObject(flash); flashSettings.FindProperty("targetRenderer").objectReferenceValue=body;
            flashSettings.ApplyModifiedPropertiesWithoutUndo();
            Require(Gameplay(old)==Gameplay(approved),"gameplay data and collision shapes unchanged after visual rig alignment");
            PrefabUtility.SaveAsPrefabAsset(approved,ApprovedPath);
            const string profile="Assets/_Project/Data/Stages/EnemySpawnProfiles/ESP_Level_03.asset";
            string text=File.ReadAllText(profile), oldGuid=AssetDatabase.AssetPathToGUID(OldPath), newGuid=AssetDatabase.AssetPathToGUID(ApprovedPath);
            int count=System.Text.RegularExpressions.Regex.Matches(text,"guid: "+oldGuid).Count;
            Require(count==5 || count==0 && text.Contains("guid: "+newGuid),"only existing five phase entries are replaced");
            if(count>0) File.WriteAllText(profile,text.Replace("guid: "+oldGuid,"guid: "+newGuid));
            AssetDatabase.ImportAsset(profile);
            File.WriteAllText(Output+"/applied.txt","PASS: MiniTurret4 selected in ESP_Level_03; unchanged gameplay/colliders and physical firePoint; existing phase data preserved.");
        }
        finally { PrefabUtility.UnloadPrefabContents(old); PrefabUtility.UnloadPrefabContents(approved); }
    }
    static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
    public static string Gameplay(GameObject root)
    {
        var types=new[]{typeof(EnemyHealth),typeof(EnemyCollisionHandler),typeof(EnemyIdentity),typeof(TurretEnemyBehaviour),typeof(Rigidbody2D),typeof(CapsuleCollider2D),typeof(CircleCollider2D)};
        return string.Join("\n",types.Select(type=>System.Text.RegularExpressions.Regex.Replace(
            EditorJsonUtility.ToJson(root.GetComponent(type)),"\"(aimPivot|firePoint)\":\\{[^}]*\\}","\"$1\":null")))+"\n"+root.transform.localScale;
    }
    sealed class Results : IErrorCallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            if(!SessionState.GetBool("MvpMiniTurret.Smoke",false)) return;
            TestRunnerApi.SaveResultToFile(result,Output+"/smoke.xml");
            File.WriteAllText(Output+"/result.txt",$"{result.ResultState}: passed={result.PassCount}, failed={result.FailCount}; MVP spawn, AI shot and destruction.");
            SessionState.EraseBool("MvpMiniTurret.Smoke");
        }
        public void OnError(string message) { if(SessionState.GetBool("MvpMiniTurret.Smoke",false)) File.WriteAllText(Output+"/smoke-error.txt",message); }
    }
    public static void Audit()
    {
        Directory.CreateDirectory(Output); var lines=new List<string>();
        foreach(string path in new[]{OldPath,ApprovedPath})
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                root.transform.position=Vector3.zero;
                lines.Add(path+" rootScale="+root.transform.localScale);
                foreach(var component in root.GetComponents<Component>())
                    if(component is not Transform && component is not Renderer) lines.Add(component.GetType().Name+": "+EditorJsonUtility.ToJson(component));
                foreach(var collider in root.GetComponentsInChildren<Collider2D>(true))
                    lines.Add("COLLIDER "+Route(collider.transform,root.transform)+" "+collider.GetType().Name+" pos="+collider.transform.position+" scale="+collider.transform.lossyScale+" "+EditorJsonUtility.ToJson(collider));
                var ai=new SerializedObject(root.GetComponent<TurretEnemyBehaviour>());
                foreach(string key in new[]{"aimPivot","firePoint"})
                {
                    var point=(Transform)ai.FindProperty(key).objectReferenceValue;
                    lines.Add(key+" "+Route(point,root.transform)+" pos="+point.position+" rotation="+point.rotation.eulerAngles);
                }
                foreach(var sprite in root.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.enabled))
                    lines.Add("VISIBLE "+Route(sprite.transform,root.transform)+" "+sprite.sprite?.name);
            }
            finally { Object.DestroyImmediate(root); }
        }
        File.WriteAllLines(Output+"/audit.txt",lines);
    }
    static string Route(Transform node,Transform root) => node==root?"root":Route(node.parent,root)+"/"+node.name;
}
#endif
