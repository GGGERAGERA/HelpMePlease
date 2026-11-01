#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Subject42.Combat.OrbitalStation;
using Object = UnityEngine.Object;

// Explicit file requests, like the other lab previews. Never runs on import alone.
[InitializeOnLoad]
public static class EnemyOrbitalArtistComparisonAuthoring
{
    public const string Output = "Artifacts/GeneratedQA/ArtistComparison";
    const string Folder = "Assets/_Project/Dev/Labs/OrbitalLab/ArtistComparison";
    static EnemyOrbitalArtistComparisonAuthoring() => EditorApplication.update += Poll;
    static void Poll()
    {
        string request = Output + "/editor.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string command = File.ReadAllText(request).Trim();
        if (command == "prepare" && EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.ExitPlaymode(); return; }
        File.Delete(request);
        try
        {
            if (command == "audit") Audit();
            else if (command == "refresh") AssetDatabase.Refresh();
            else if (command == "prepare" && !EditorApplication.isPlayingOrWillChangePlaymode) Prepare();
        }
        catch (Exception e) { File.WriteAllText(Output + "/error.txt", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/Subject42/Dev/OrbitalLab/Prepare Artist Comparison")]
    public static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != EnemyOrbitalLabController.ScenePath) scene = EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath);
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var lab = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EnemyOrbitalLabController>(true)).Single();
        var config = AssetDatabase.LoadAssetAtPath<OrbitalPresentationConfig>("Assets/_Project/Data/Orbital/OrbitalPresentationConfig.asset");
        lab.Pistol.WeaponVisual = config.PistolPrefab;
        lab.Shotgun.WeaponVisual = config.ShotgunPrefab;
        lab.Sniper.WeaponVisual = config.SniperPrefab;
        // Preserve all five existing spider prefab references and their animation assets.
        lab.ArtistComparisonEnabled = true;
        EditorUtility.SetDirty(lab); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Output + "/prepared.txt", "Restored original orbital visuals; all five existing spiders preserved.");
    }
    static GameObject Weapon(GameObject source, string kind, string spriteName, float width, bool flip)
    {
        var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(source));
        try
        {
            root.name = "LabArtist" + kind;
            var view = root.GetComponent<OrbitalModuleView>();
            var legacy = view.Body;
            var carrier = new GameObject("Shared recoil body").transform;
            carrier.SetParent(root.transform,false); legacy.SetParent(carrier,false); view.Body=carrier;
            var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/art/Sprites/weapons/Weapons4.png").OfType<Sprite>().Single(s=>s.name==spriteName);
            var compact = new GameObject("Compact artist sprite").AddComponent<SpriteRenderer>();
            compact.transform.SetParent(carrier,false); compact.sprite=sprite;
            var old = view.Sprites.First(s=>s!=view.Halo && s.enabled);
            compact.sharedMaterial=old.sharedMaterial; compact.sortingLayerID=old.sortingLayerID; compact.sortingOrder=old.sortingOrder;
            // Use actual opaque pixels rather than the 64px padded cell; align the barrel tip to +X.
            compact.flipX=flip;
            var opaque = OpaqueRect(sprite,flip,out float muzzleY);
            float scale = width / opaque.width;
            compact.transform.localScale = Vector3.one * scale;
            compact.transform.localPosition = new Vector3(.42f - opaque.xMax*scale, -muzzleY*scale, 0f);
            compact.enabled=false;
            var comparison=root.AddComponent<LabWeaponVisualComparison>();
            comparison.LegacySprites=view.Sprites.Where(s=>s!=view.Halo).ToArray(); comparison.CompactSprite=compact;
            // Deliberately keep view.Sprites unchanged: contact/hit-test geometry stays exactly legacy.
            return PrefabUtility.SaveAsPrefabAsset(root,Folder + "/LabArtist"+kind+".prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static Rect OpaqueRect(Sprite sprite,bool flip,out float muzzleY)
    {
        var rt=RenderTexture.GetTemporary(sprite.texture.width,sprite.texture.height,0,RenderTextureFormat.ARGB32);
        var previous=RenderTexture.active;
        Texture2D texture=null;
        try
        {
            Graphics.Blit(sprite.texture,rt); RenderTexture.active=rt;
            texture=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
            texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); texture.Apply();
            var rect=sprite.rect; int minX=(int)rect.width,minY=(int)rect.height,maxX=0,maxY=0;
            for(int y=0;y<rect.height;y++) for(int x=0;x<rect.width;x++)
                if(texture.GetPixel((int)rect.x+x,(int)rect.y+y).a>.1f)
                { minX=Math.Min(minX,x); maxX=Math.Max(maxX,x); minY=Math.Min(minY,y); maxY=Math.Max(maxY,y); }
            float muzzleSum=0f,muzzlePixels=0f;
            for(int y=minY;y<=maxY;y++)
                if(texture.GetPixel((int)rect.x+(flip?minX:maxX),(int)rect.y+y).a>.1f)
                { muzzleSum+=y+.5f; muzzlePixels++; }
            muzzleY=(muzzleSum/Mathf.Max(1,muzzlePixels)-sprite.pivot.y)/sprite.pixelsPerUnit;
            var opaque=new Rect((minX-sprite.pivot.x)/sprite.pixelsPerUnit,(minY-sprite.pivot.y)/sprite.pixelsPerUnit,
                (maxX-minX+1)/sprite.pixelsPerUnit,(maxY-minY+1)/sprite.pixelsPerUnit);
            if(flip) opaque.x=-opaque.xMax;
            return opaque;
        }
        finally { if(texture!=null) Object.DestroyImmediate(texture); RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); }
    }
    static GameObject Spider(string name,string artName,string clipName)
    {
        string source="Assets/_Project/art/Sprites/Aseprite/newEnemyes1/"+artName+".aseprite";
        var root=PrefabUtility.LoadPrefabContents("Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab");
        try
        {
            root.name=name;
            var oldFlash=root.GetComponentInChildren<EnemyWhiteFlash>(true);
            string flashSettings=oldFlash!=null?EditorJsonUtility.ToJson(oldFlash):null;
            foreach(var child in root.transform.Cast<Transform>().ToArray())
                if(child.GetComponentInChildren<Collider2D>(true)==null) Object.DestroyImmediate(child.gameObject);
            var graphic=new GameObject("Artist facing").transform; graphic.SetParent(root.transform,false);
            var art=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source),graphic,false);
            art.name=artName; art.transform.localRotation=Quaternion.identity;
            var renderer=art.GetComponentInChildren<SpriteRenderer>(true);
            float scale=1.12f/renderer.sprite.bounds.size.x;
            art.transform.localScale=Vector3.one*scale;
            art.transform.localPosition=-renderer.sprite.bounds.center*scale;
            renderer.sortingLayerName="Enemy"; renderer.sortingOrder=1;
            var animator=art.GetComponentInChildren<Animator>(true);
            if(clipName!=null)
            {
                string path=Folder+"/"+name+".controller";
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
                var machine=controller.layers[0].stateMachine;
                var state=machine.states.Length>0 ? machine.states[0].state : machine.AddState("Walk");
                state.motion=AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>().Single(c=>c.name==clipName);
                machine.defaultState=state;
                if(animator==null) animator=art.AddComponent<Animator>();
                animator.runtimeAnimatorController=controller;
                EditorUtility.SetDirty(controller); AssetDatabase.SaveAssetIfDirty(controller);
            }
            var movement=new SerializedObject(root.GetComponent<EnemyChaseMovement>());
            movement.FindProperty("visualRoot").objectReferenceValue=graphic;
            movement.FindProperty("animator").objectReferenceValue=animator;
            movement.FindProperty("runParameterName").stringValue="";
            movement.FindProperty("movementSpeedParameterName").stringValue="";
            movement.ApplyModifiedPropertiesWithoutUndo();
            var flash=root.GetComponentInChildren<EnemyWhiteFlash>(true) ?? root.AddComponent<EnemyWhiteFlash>();
            if(flashSettings!=null) EditorJsonUtility.FromJsonOverwrite(flashSettings,flash);
            var flashSo=new SerializedObject(flash); flashSo.FindProperty("targetRenderer").objectReferenceValue=renderer; flashSo.ApplyModifiedPropertiesWithoutUndo();
            var healthView=new SerializedObject(root.GetComponent<EnemyHealthPresentation>());
            healthView.FindProperty("whiteFlash").objectReferenceValue=flash; healthView.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static void Audit()
    {
        Directory.CreateDirectory(Output);
        var lines = new List<string>();
        foreach (string path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/_Project/") &&
            (p.Contains("Spider") || p.Contains("Weapons4.png") || p.Contains("miniWeapon") || p.Contains("p_Boss2.prefab"))))
        {
            if (AssetDatabase.IsValidFolder(path)) continue;
            lines.Add(path);
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var sprite in assets.OfType<Sprite>().Take(18))
                lines.Add($"  Sprite {sprite.name}: rect={sprite.rect}, bounds={sprite.bounds}, pivot={sprite.pivot}");
            foreach (var clip in assets.OfType<AnimationClip>()) lines.Add($"  Clip {clip.name}: {clip.length}s");
            foreach (var go in assets.OfType<GameObject>().Take(1))
                lines.Add("  Components: " + string.Join(", ",go.GetComponentsInChildren<Component>(true).Where(c=>c!=null).Select(c=>c.GetType().Name).Distinct()));
            if (path.EndsWith(".aseprite") && path.Contains("Spider"))
            {
                var sprite = assets.OfType<Sprite>().FirstOrDefault();
                if (sprite == null) continue;
                var rt = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0, RenderTextureFormat.ARGB32);
                var previous = RenderTexture.active;
                try
                {
                    Graphics.Blit(sprite.texture, rt); RenderTexture.active = rt;
                    var texture = new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
                    texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); texture.Apply();
                    File.WriteAllBytes(Output + "/" + Path.GetFileNameWithoutExtension(path) + "-atlas.png",texture.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(texture);
                }
                finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); }
            }
        }
        File.WriteAllLines(Output + "/audit.txt",lines);
    }
}
#endif
