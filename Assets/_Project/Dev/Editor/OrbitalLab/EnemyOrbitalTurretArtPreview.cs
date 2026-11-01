#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Explicit, short lab authoring/preview. No production asset writes or player builds.
[InitializeOnLoad]
public static class EnemyOrbitalTurretArtPreview
{
    const string Output="Artifacts/GeneratedQA/TurretArt", Folder="Assets/_Project/Dev/Labs/OrbitalLab/TurretArt", Key="EnemyOrbital.TurretArtPreview";
    static IEnumerator routine;
    static double next,deadline;
    static int errors;
    static bool editModeReady=!EditorApplication.isPlaying;
    static readonly List<string> report=new();
    static EnemyOrbitalTurretArtPreview()
    {
        EditorApplication.update+=Poll;
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state==PlayModeStateChange.ExitingPlayMode || state==PlayModeStateChange.EnteredPlayMode) editModeReady=false;
            if(state==PlayModeStateChange.EnteredEditMode) editModeReady=true;
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
            { report.Clear(); errors=0; deadline=EditorApplication.timeSinceStartup+30; routine=Preview(); }
            if(state==PlayModeStateChange.EnteredEditMode) { routine=null; SessionState.EraseBool(Key); }
        };
        Application.logMessageReceived+=(message,stack,type)=>
        { if(routine!=null && (type==LogType.Error||type==LogType.Exception||type==LogType.Assert)) { errors++; report.Add("UNITY ERROR: "+message); } };
    }
    static void Poll()
    {
        string request=Output+"/editor.request";
        if(File.Exists(request) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            string command=File.ReadAllText(request).Trim();
            if(command=="run" && (!editModeReady || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode))
            { if(EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode(); return; }
            File.Delete(request);
            try
            {
                if(command=="run") Open();
                else if(command=="refresh") AssetDatabase.Refresh();
                else if(command=="capture") ScreenCapture.CaptureScreenshot(Output+"/comparison.png");
            }
            catch(Exception e) { File.WriteAllText(Output+"/error.txt",e.ToString()); Debug.LogException(e); }
        }
        if(routine==null || EditorApplication.timeSinceStartup<next) return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new Exception("30-second turret preview deadline");
            if(!routine.MoveNext()) { Finish(errors==0?"PASS: short preview, zero runtime errors; left on lab pause.":"FAIL: runtime errors"); return; }
            next=EditorApplication.timeSinceStartup+(routine.Current is float delay?delay:.01);
        }
        catch(Exception e) { Finish("FAIL: "+e); }
    }
    static void Finish(string result) { routine=null; report.Add(result); File.WriteAllLines(Output+"/result.txt",report); }
    static void Require(bool value,string message) { if(!value) throw new Exception(message); report.Add("PASS: "+message); }
    [MenuItem("Tools/Subject42/Dev/OrbitalLab/Compare Stationary Turret Art")]
    public static void Open()
    {
        if(!editModeReady || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory(Output); Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var scene=EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath);
        var lab=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EnemyOrbitalLabController>(true)).Single();
        var config=AssetDatabase.LoadAssetAtPath<OrbitalPresentationConfig>("Assets/_Project/Data/Orbital/OrbitalPresentationConfig.asset");
        lab.Pistol.WeaponVisual=config.PistolPrefab; lab.Shotgun.WeaponVisual=config.ShotgunPrefab; lab.Sniper.WeaponVisual=config.SniperPrefab;
        lab.ArtistComparisonEnabled=true; lab.ArtistCleanView=true; lab.LaserTurretArtistIndex=-1;
        string enemies="Assets/_Project/prefabs/Enemies/", art="Assets/_Project/art/Sprites/";
        var old=AssetDatabase.LoadAssetAtPath<GameObject>(enemies+"p_EnemyTurret1.prefab");
        var mini=AssetDatabase.LoadAssetAtPath<GameObject>(enemies+"p_EnemyTurret2.prefab");
        var nativeMini=AssetDatabase.LoadAssetAtPath<GameObject>(art+"Aseprite/newEnemyes1/MiniTurret1.aseprite");
        var nativeLaser=Model(nativeMini,"MiniSourceLaser",true);
        lab.ArtistTurrets=new[] {
            Candidate("Текущий Перехват","EnemyOrbitalLabLaserTurret.cs",null),
            Candidate("Turret3 · готовый prefab","p_EnemyTurret1.prefab\nTurret3.png",Model(old,"Turret3"),old,Model(old,"Turret3Laser",true)),
            Candidate("MiniTurret4 · готовый prefab","p_EnemyTurret2.prefab\nMiniTurret1.png",Model(mini,"MiniTurret4"),mini,Model(mini,"MiniTurret4Laser",true)),
            Candidate("MiniTurret1 · белая","newEnemyes1/\nMiniTurret1.aseprite",Model(nativeMini,"MiniSource"),MiniCombat(old,nativeLaser),nativeLaser),
            Candidate("Turret1 · слои / анимация","Aseprite/Turret1.aseprite",Model(AssetDatabase.LoadAssetAtPath<GameObject>(art+"Aseprite/Turret1.aseprite"),"Turret1Source")),
            Candidate("Turret2 · старый красный","Sprites/Turret2.png\nTurret2_20",SpriteModel(art+"Turret2.png","Turret2_20","OldRed")),
            Candidate("Turret1 · старый серый","Sprites/Turret1.png\nTurret1_10",SpriteModel(art+"Turret1.png","Turret1_10","OldGrey")),
            Candidate("MiniTurret1 · старые детали","MiniTurret1.png @bbc17af8\nАтлас деталей, без prefab",HistoricalAtlas()) };
        EditorUtility.SetDirty(lab); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Audit(old,mini,art);
        typeof(EnemyOrbitalInterceptionPreview).GetMethod("SetGameView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        SessionState.SetBool(Key,true); EditorApplication.EnterPlaymode();
    }
    static EnemyOrbitalLabController.TurretArtCandidate Candidate(string name,string source,GameObject visual,GameObject combat=null,GameObject laser=null)
        =>new(){Name=name,Source=source,Visual=visual,CombatPrefab=combat,LaserVisual=laser};
    static GameObject HistoricalAtlas()
    {
        string path=Folder+"/MiniTurret1-bbc17af8.png";
        if(!File.Exists(path)) File.Copy(Output+"/MiniTurret1-bbc17af8.png",path);
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        if(importer.textureType!=TextureImporterType.Sprite || importer.spriteImportMode!=SpriteImportMode.Single)
        {
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=32; importer.mipmapEnabled=false; importer.alphaIsTransparency=true; importer.filterMode=FilterMode.Point;
            importer.SaveAndReimport();
        }
        return SpriteModel(path,"MiniTurret1-bbc17af8","HistoricalMiniParts");
    }
    static GameObject MiniCombat(GameObject source,GameObject visual)
    {
        var root=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(source));
        try
        {
            root.name="LabMiniTurret1";
            foreach(var sprite in root.GetComponentsInChildren<SpriteRenderer>(true)) sprite.enabled=false;
            var so=new SerializedObject(root.GetComponent<TurretEnemyBehaviour>());
            var muzzle=(Transform)so.FindProperty("firePoint").objectReferenceValue;
            var artist=(GameObject)PrefabUtility.InstantiatePrefab(visual,muzzle);
            artist.transform.localPosition=Vector3.right*.72f; artist.transform.localRotation=Quaternion.Euler(0,0,180);
            var flash=root.GetComponentInChildren<EnemyWhiteFlash>(true);
            if(flash!=null)
            {
                var flashSo=new SerializedObject(flash);
                flashSo.FindProperty("targetRenderer").objectReferenceValue=artist.GetComponentInChildren<SpriteRenderer>();
                flashSo.ApplyModifiedPropertiesWithoutUndo();
            }
            return PrefabUtility.SaveAsPrefabAsset(root,Folder+"/LabMiniTurret1.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static GameObject SpriteModel(string path,string spriteName,string name)
    {
        var go=new GameObject(name); var renderer=go.AddComponent<SpriteRenderer>();
        renderer.sprite=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single(s=>s.name==spriteName);
        try { return Model(go,name); } finally { Object.DestroyImmediate(go); }
    }
    static GameObject Model(GameObject source,string name,bool laser=false)
    {
        var original=Object.Instantiate(source); var result=new GameObject(name); var content=new GameObject("Authored sprite parts").transform;
        content.SetParent(result.transform,false);
        try
        {
            original.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity); original.transform.localScale=Vector3.one;
            var behaviour=original.GetComponent<TurretEnemyBehaviour>(); Vector3 muzzle=Vector3.zero;
            if(behaviour!=null)
            {
                var so=new SerializedObject(behaviour);
                var pivot=(Transform)so.FindProperty("aimPivot").objectReferenceValue;
                if(pivot!=null) pivot.rotation=Quaternion.Euler(0,0,180); // Existing turret code authors barrels along -X.
                var fire=(Transform)so.FindProperty("firePoint").objectReferenceValue;
                if(fire!=null) muzzle=fire.position;
            }
            else
            {
                string path=AssetDatabase.GetAssetPath(source);
                var clip=string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault();
                if(clip!=null) clip.SampleAnimation(original,clip.length*.6f);
            }
            var sprites=original.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.enabled&&s.sprite!=null).OrderBy(s=>s.sortingOrder).ToArray();
            if(sprites.Length==0) throw new Exception("No visible turret sprites: "+name);
            foreach(var sprite in sprites)
            {
                var copy=new GameObject(sprite.name).AddComponent<SpriteRenderer>(); copy.transform.SetParent(content,false);
                copy.sprite=sprite.sprite; copy.color=sprite.color; copy.sharedMaterial=sprite.sharedMaterial;
                copy.flipX=sprite.flipX; copy.flipY=sprite.flipY; copy.sortingLayerName="Player"; copy.sortingOrder=124+sprite.sortingOrder;
                copy.transform.localPosition=sprite.transform.position; copy.transform.localRotation=sprite.transform.rotation;
                copy.transform.localScale=sprite.transform.lossyScale;
            }
            var renderers=content.GetComponentsInChildren<SpriteRenderer>(); Bounds bounds=renderers[0].bounds;
            foreach(var sprite in renderers) bounds.Encapsulate(sprite.bounds);
            if(laser && behaviour==null) muzzle=OpaqueMuzzle(sprites.Single());
            float scale=(laser?1.4f:2.2f)/Mathf.Max(.01f,bounds.size.y);
            bool mirror=laser && muzzle.x<bounds.center.x;
            content.localScale=new Vector3(mirror?-scale:scale,scale,scale);
            content.localPosition=laser?Vector3.right*.72f-Vector3.Scale(muzzle,content.localScale):-new Vector3(bounds.center.x,bounds.min.y,0)*scale;
            if(laser)
            {
                var marker=new GameObject("Authored muzzle").transform; marker.SetParent(content,false); marker.localPosition=muzzle;
            }
            return PrefabUtility.SaveAsPrefabAsset(result,Folder+"/"+name+".prefab");
        }
        finally { Object.DestroyImmediate(original); Object.DestroyImmediate(result); }
    }
    static Vector3 OpaqueMuzzle(SpriteRenderer renderer)
    {
        var sprite=renderer.sprite; var rt=RenderTexture.GetTemporary(sprite.texture.width,sprite.texture.height,0,RenderTextureFormat.ARGB32);
        var previous=RenderTexture.active; Texture2D texture=null;
        try
        {
            Graphics.Blit(sprite.texture,rt); RenderTexture.active=rt;
            texture=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
            texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); texture.Apply();
            var rect=sprite.rect; int right=0; float sum=0,count=0;
            for(int x=0;x<rect.width;x++) for(int y=0;y<rect.height;y++)
                if(texture.GetPixel((int)rect.x+x,(int)rect.y+y).a>.1f) right=Math.Max(right,x);
            for(int y=0;y<rect.height;y++) if(texture.GetPixel((int)rect.x+right,(int)rect.y+y).a>.1f) { sum+=y+.5f; count++; }
            return renderer.transform.TransformPoint(new Vector3((right+1-sprite.pivot.x)/sprite.pixelsPerUnit,(sum/Mathf.Max(1,count)-sprite.pivot.y)/sprite.pixelsPerUnit,0));
        }
        finally { if(texture!=null) Object.DestroyImmediate(texture); RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); }
    }
    static void Audit(GameObject old,GameObject mini,string art)
    {
        var lines=new List<string>();
        foreach(var source in new[]{old,mini})
        {
            lines.Add(AssetDatabase.GetAssetPath(source));
            foreach(var renderer in source.GetComponentsInChildren<SpriteRenderer>(true))
            {
                bool active=true; for(var node=renderer.transform;node!=null;node=node.parent) active &= node.gameObject.activeSelf;
                lines.Add($"  {renderer.name}: activeInPrefab={active}, enabled={renderer.enabled}, color={renderer.color}, sprite={renderer.sprite?.name}, source={AssetDatabase.GetAssetPath(renderer.sprite)}");
            }
            var so=new SerializedObject(source.GetComponent<TurretEnemyBehaviour>());
            lines.Add($"  aim={so.FindProperty("aimDuration").floatValue}; cooldown={so.FindProperty("cooldown").floatValue}; projectile={AssetDatabase.GetAssetPath(so.FindProperty("projectilePrefab").objectReferenceValue)}");
        }
        foreach(string path in new[]{art+"Aseprite/Turret1.aseprite",art+"Aseprite/Turret3.aseprite",art+"Aseprite/newEnemyes1/MiniTurret1.aseprite"})
        { lines.Add(path); foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path)) lines.Add("  "+asset.GetType().Name+" "+asset.name); }
        File.WriteAllLines(Output+"/audit.txt",lines);
    }
    static IEnumerator Preview()
    {
        EnemyOrbitalLabController lab;
        while((lab=Object.FindFirstObjectByType<EnemyOrbitalLabController>())==null || !lab.Ready || !lab.SandboxReady) yield return .02f;
        lab.AutomaticInterceptions=false; lab.CancelInterception(); lab.SetSegmentedOrbit(true);
        while(!lab.Ready || !lab.Segmented.Bound) yield return .02f;
        lab.Segmented.LoadPreset("Шашечка"); // Same six-weapon construction as the preceding review scene.
        lab.Segmented.SetMotion(SegmentedOrbitConfiguration.Motion.Fixed);
        lab.PrepareArtistComparisonField();
        Require(lab.ArtistSpiderPrefabs.Length==5 && lab.ManualEnemyTypes.Count(t=>t<=-100)==5,"same five spider variants and manual buttons");
        Require(lab.Station.Modules.All(g=>g is not EnemyOrbitalLabWeapon w || w.SourceProfile.WeaponVisual.GetComponent<LabWeaponVisualComparison>()==null),"original orbital visuals restored; six-weapon construction retained");
        var old=lab.ArtistTurrets[1].CombatPrefab.GetComponent<TurretEnemyBehaviour>();
        var mini=lab.ArtistTurrets[2].CombatPrefab.GetComponent<TurretEnemyBehaviour>();
        var first=new SerializedObject(old); var second=new SerializedObject(mini);
        Require(old.ProjectilePrefab==mini.ProjectilePrefab && first.FindProperty("aimDuration").floatValue==second.FindProperty("aimDuration").floatValue &&
            first.FindProperty("cooldown").floatValue==second.FindProperty("cooldown").floatValue,"ready turret variants share existing AI, projectile, 1s aim and 2.5s cooldown");
        var shoot=typeof(TurretEnemyBehaviour).GetMethod("Shoot",BindingFlags.Instance|BindingFlags.NonPublic);
        int initial=Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length;
        foreach(int index in new[]{1,2,3})
        {
            var actor=lab.SpawnArtistTurret(index); Require(actor!=null,"spawn ready turret "+index);
            if(index==3)
            {
                var actorSettings=new SerializedObject(actor.GetComponent<TurretEnemyBehaviour>());
                var fire=(Transform)actorSettings.FindProperty("firePoint").objectReferenceValue;
                var visualMuzzle=actor.GetComponentsInChildren<Transform>().Single(t=>t.name=="Authored muzzle");
                Require(Vector2.Distance(fire.position,visualMuzzle.position)<.001f,"white turret visual muzzle matches unchanged projectile firePoint");
            }
            shoot.Invoke(actor.GetComponent<TurretEnemyBehaviour>(),new object[]{Vector2.down});
        }
        Require(Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length==initial+3,"ready prefabs and white lab variant fire the unchanged real enemy projectile");
        foreach(int style in new[]{-1,1,2,3})
        {
            lab.CancelInterception(); lab.LaserTurretArtistIndex=style;
            Require(lab.LaunchInterception(EnemyOrbitalLabController.InterceptionVariant.Single),"existing Interception spawn, art="+style);
            var turret=lab.LaserTurrets.Single(); int shots=lab.LaserShots;
            Vector2 expected=(Vector2)turret.transform.position+turret.Direction*.72f;
            Require(turret.Origin==expected && turret.GetComponent<CircleCollider2D>().radius==.55f,"same laser origin and collider, art="+style);
            if(style>=0)
            {
                var visualMuzzle=turret.GetComponentsInChildren<Transform>().Single(t=>t.name=="Authored muzzle");
                Require(Vector2.Distance(turret.Origin,visualMuzzle.position)<.001f,"artist barrel tip matches unchanged laser origin, art="+style);
            }
            turret.Tick(EnemyOrbitalLabLaserTurret.EnterSeconds); turret.Tick(EnemyOrbitalLabLaserTurret.ChargeSeconds);
            Require(turret.Firing && lab.LaserShots==shots+1,"same charge/fire state machine, art="+style);
            turret.Tick(EnemyOrbitalLabLaserTurret.FireSeconds);
            Require(!turret.Firing,"same fire duration, art="+style);
        }
        lab.CancelInterception(); lab.LaserTurretArtistIndex=-1;
        foreach(var projectile in Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
        { var pooled=projectile.GetComponent<PooledGameObject>(); if(pooled!=null) pooled.Release(); else Object.Destroy(projectile.gameObject); }
        lab.PrepareArtistComparisonField();
        lab.ShowTurretArtGallery();
        var view=typeof(EnemyOrbitalLabController).GetField("segmentedView",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(lab);
        view.GetType().GetField("tab",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(view,1);
        typeof(EnemyOrbitalLabController).GetField("manualSpawnScroll",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(lab,new Vector2(0,100));
        Require(lab.Station.ValidateState(out _),"orbital state remains valid");
        yield return .15f;
        ScreenCapture.CaptureScreenshot(Output+"/comparison.png"); yield return .2f;
        File.WriteAllText(Output+"/state.txt",$"scene={lab.gameObject.scene.path}\nplaying={EditorApplication.isPlaying}\npaused={lab.Paused}\nreferences={lab.ArtistTurrets.Length}\nspiders={lab.ManualAliveEnemies}\nselectedLaserArt={lab.LaserTurretArtistIndex}\n");
    }
}
#endif
