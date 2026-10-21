#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BunkerInteractionAuthoring
{
    public const string ScenePath="Assets/_Project/Scenes/MainBuild/MainMenu.unity";
    public const string Output="Artifacts/GeneratedQA/BunkerInteraction";
    static BunkerInteractionAuthoring()
    {
        EditorApplication.update += Poll;
        EditorApplication.hierarchyChanged += QueueArrowSync;
        EditorSceneManager.sceneOpened += (_,__) => QueueArrowSync();
        EditorApplication.playModeStateChanged += state => { if(state==PlayModeStateChange.EnteredEditMode) QueueArrowSync(); };
        QueueArrowSync();
    }
    private static void QueueArrowSync()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.delayCall -= SyncEditorArrows;
        EditorApplication.delayCall += SyncEditorArrows;
    }
    private static void SyncEditorArrows()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        for(int i=0;i<SceneManager.sceneCount;i++)
        {
            var scene=SceneManager.GetSceneAt(i);
            if(!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) continue;
            foreach(var manager in All<BunkerInteractionIndicators>(scene)) manager.RefreshBindings();
        }
    }
    private static void Poll()
    {
        string livePath=Output+"/live.request";
        if(File.Exists(livePath) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            File.Delete(livePath);
            var report=new StringBuilder();
            report.AppendLine("playing="+EditorApplication.isPlaying+" paused="+EditorApplication.isPaused+" player="+PlayerRuntimeReference.PlayerTransform+
                " qaSessionActive="+SessionState.GetBool("Subject42.Verification.AnyRunActive",false)+" testRunnerActive="+TestRunnerActive());
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var loaded=SceneManager.GetSceneAt(i);
                report.AppendLine("SCENE "+loaded.path+" dirty="+loaded.isDirty);
                foreach(var manager in All<BunkerInteractionIndicators>(loaded)) report.AppendLine("manager="+PathOf(manager.transform)+" blocked="+manager.IsBlocked+" enabled="+manager.isActiveAndEnabled);
                foreach(var arrow in All<BunkerInteractionArrow>(loaded)) report.AppendLine("arrow="+arrow.name+" source="+arrow.Source+" visible="+arrow.IsVisible+" position="+arrow.transform.position);
            }
            Directory.CreateDirectory(Output); File.WriteAllText(Output+"/live-state.txt",report.ToString());
        }
        string path=Output+"/author.request";
        if (!File.Exists(path)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode) return;
        string stamp=File.GetLastWriteTimeUtc(path).Ticks.ToString();
        if(SessionState.GetString("BunkerInteraction.AuthorStamp","")!=stamp)
        { SessionState.SetString("BunkerInteraction.AuthorStamp",stamp); SessionState.SetFloat("BunkerInteraction.RefreshAt",(float)EditorApplication.timeSinceStartup); AssetDatabase.Refresh(); return; }
        if(EditorApplication.timeSinceStartup-SessionState.GetFloat("BunkerInteraction.RefreshAt",0)<3) return;
        string command=File.ReadAllText(path).Trim(); File.Delete(path); Directory.CreateDirectory(Output);
        try { if(command=="audit") Audit(); else if(command=="migrate") Migrate(); else if(command=="repair-arrows") RepairArrows(); else if(command=="restore-original") RestoreOriginal(); else if(command=="reset-idle-qa") { if(TestRunnerActive()) throw new InvalidOperationException("A Unity test is still active."); SessionState.EraseBool("Subject42.Verification.AnyRunActive"); SessionState.EraseBool("TutorialVerification.Running"); } File.WriteAllText(Output+"/author-result.txt",command+" SUCCESS"); }
        catch(Exception ex) { File.WriteAllText(Output+"/author.error",ex.ToString()); Debug.LogException(ex); }
    }
    public static string PathOf(Transform t) => t.parent!=null ? PathOf(t.parent)+"/"+t.name : t.name;
    private static bool TestRunnerActive() => (bool)typeof(UnityEditor.TestTools.TestRunner.Api.TestRunnerApi)
        .GetMethod("IsRunActive",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,null);
    private static void Set(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field) ?? throw new InvalidOperationException(field);
        if(value is bool flag) p.boolValue=flag;
        else if(value is float number) p.floatValue=number;
        else if(value is string text) p.stringValue=text;
        else p.objectReferenceValue=value as Object;
        so.ApplyModifiedPropertiesWithoutUndo();
        if(PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
    private static T[] All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
    [MenuItem("Tools/Subject42/Bunker/Restore Original Animated Arrows")]
    public static void RestoreOriginal()
    {
        var scene=SceneManager.GetSceneByPath(ScenePath);
        if(!scene.IsValid() || !scene.isLoaded) scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var manager=All<BunkerInteractionIndicators>(scene).Single();
        var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Bunker/PF_InteractionArrow.prefab");
        Set(manager,"arrowPrefab",original);
        // Remove the failed static replacement, never the authored original.
        foreach(var arrow in All<BunkerInteractionArrow>(scene))
            if(arrow.GetComponent<Animator>() == null) Object.DestroyImmediate(arrow.gameObject);
        manager.RefreshBindings();
        foreach(var arrow in All<BunkerInteractionArrow>(scene))
        {
            // This room's status/name plates sit immediately above the selector.
            // Place the original arrow over the button face, below those plates.
            if(arrow.Source is Subject42.Bunker.Gallery.EnemyGalleryController)
                arrow.transform.position = arrow.Source.transform.position + Vector3.up*1.3f;
            EditorUtility.SetDirty(arrow);
            PrefabUtility.RecordPrefabInstancePropertyModifications(arrow);
            PrefabUtility.RecordPrefabInstancePropertyModifications(arrow.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(arrow.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(arrow.GetComponent<BunkerInteractableCollider>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(arrow.GetComponent<BoxCollider2D>());
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.DeleteAsset("Assets/_Project/prefabs/Bunker/PF_BunkerInteractionArrow.prefab");
        SceneView.RepaintAll();
    }
    public static void RepairArrows()
    {
        var scene=SceneManager.GetSceneByPath(ScenePath);
        if(!scene.IsValid() || !scene.isLoaded) scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var manager=All<BunkerInteractionIndicators>(scene).Single();
        Set(manager,"interactionDistance",5.5f);
        manager.RefreshBindings();
        foreach(var arrow in All<BunkerInteractionArrow>(scene))
        {
            EditorUtility.SetDirty(arrow);
            PrefabUtility.RecordPrefabInstancePropertyModifications(arrow);
            PrefabUtility.RecordPrefabInstancePropertyModifications(arrow.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(arrow.Visual);
            PrefabUtility.RecordPrefabInstancePropertyModifications(arrow.GetComponent<BoxCollider2D>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(arrow.GetComponent<BunkerInteractableCollider>());
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        SceneView.RepaintAll();
    }
    [MenuItem("Tools/Subject42/Bunker/Author Shared Interaction Arrows")]
    public static void Migrate()
    {
        var legacy=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Bunker/PF_InteractionArrow.prefab");

        var scene=SceneManager.GetSceneByPath(ScenePath);
        if(!scene.IsValid() || !scene.isLoaded) scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        var cursor=All<BunkerCursorInteractor>(scene).Single();
        Set(cursor,"arrowsOnly",true);
        var manager=cursor.GetComponent<BunkerInteractionIndicators>() ?? cursor.gameObject.AddComponent<BunkerInteractionIndicators>();
        Set(manager,"arrowPrefab",legacy);
        Set(manager,"panelManager",All<BunkerPanelManager>(scene).Single());
        foreach(var prompt in All<InteractionPromptUI>(scene)) Set(prompt,"catOnly",true);
        foreach(var level in All<BunkerStationLevelView>(scene)) Set(level,"displayWorldLabels",false);
        foreach(var station in All<BunkerStation>(scene))
        {
            var kind=new SerializedObject(station).FindProperty("stationType").enumValueIndex;
            if(kind==(int)BunkerStationType.Upgrade || kind==(int)BunkerStationType.AnomalyStabilizer) station.gameObject.SetActive(true);
        }
        foreach(var mission in All<MissionProvider>(scene))
        {
            Set(mission,"m_Enabled",true);
            Set(mission,"interactionArrow",null);
            foreach(var proxy in mission.GetComponentsInChildren<BunkerInteractableCollider>(true)) Set(proxy,"sourceRoot",mission.gameObject);
        }
        RestoreOriginal();
        foreach(var label in All<TMP_Text>(scene))
            if(label.name=="ArcadeLabel" || label.name=="Operator label" ||
                label.GetComponentInParent<Canvas>() is { renderMode:RenderMode.WorldSpace }) label.gameObject.SetActive(false);
        foreach(var root in scene.GetRootGameObjects().Where(r=>r.name=="FOOTBALL" || r.name=="ENEMY_GALLERY"))
            if(root.transform.Find("Labels") is Transform labels) labels.gameObject.SetActive(false);

        var football=All<FootballMinigame>(scene).Single(); football.enabled=false;
        var arena=football.transform.parent;
        foreach(Transform child in arena)
            if(child.name!="Pixel Field" && child.name!="Field Markings") child.gameObject.SetActive(false);
        foreach(Transform child in arena.parent)
            if(child!=arena) child.gameObject.SetActive(false);
        foreach(var station in All<FootballStartStation>(scene)) { station.enabled=false; station.gameObject.SetActive(false); }
        foreach(var hud in All<FootballMinigameHUD>(scene)) hud.gameObject.SetActive(false);
        var gate=All<BunkerGateVisual>(scene).Single(g=>g.name=="Door_Football");
        // This doorway is also the only route to the arcade. Close the field itself,
        // leaving the existing southern transit strip available to those cabinets.
        Set(gate,"startOpen",false); Set(gate,"openOnPlayerProximity",true); gate.Close();
        if(!All<Transform>(scene).Any(t=>t.name=="FootballComingSoon")) BuildSign(scene);
        All<Transform>(scene).Single(t=>t.name=="FootballComingSoon").position=new Vector3(-35f,3.4f,-1f);
        BuildFieldClosure(scene);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Audit();
    }

    private static void BuildFieldClosure(Scene scene)
    {
        var walls=All<Tilemap>(scene).Single(t=>t.name=="FOOTBALL - Walls");
        Sprite tile=null; Color tint=walls.color; float nearest=float.PositiveInfinity;
        foreach(var cell in walls.cellBounds.allPositionsWithin)
        {
            var sprite=walls.GetSprite(cell); if(sprite==null) continue;
            float distance=(walls.GetCellCenterWorld(cell)-new Vector3(-35f,26f)).sqrMagnitude;
            if(distance<nearest) { tile=sprite; tint=walls.color*walls.GetColor(cell); nearest=distance; }
        }
        if(tile==null) throw new InvalidOperationException("Football closure requires the room's existing wall art.");
        var existing=All<Transform>(scene).SingleOrDefault(t=>t.name=="FootballMaintenanceBarrier");
        var root=existing!=null ? existing.gameObject : new GameObject("FootballMaintenanceBarrier");
        root.transform.SetParent(walls.transform.parent,false); root.transform.position=new Vector3(-35f,2f,0);
        var renderer=root.GetComponent<SpriteRenderer>() ?? root.AddComponent<SpriteRenderer>(); renderer.sprite=tile;
        renderer.sharedMaterial=walls.GetComponent<TilemapRenderer>().sharedMaterial; renderer.color=tint;
        renderer.sortingLayerName="Foreground"; renderer.sortingOrder=20;
        renderer.drawMode=SpriteDrawMode.Tiled; renderer.size=new Vector2(30f,1f);
        var collider=root.GetComponent<BoxCollider2D>() ?? root.AddComponent<BoxCollider2D>(); collider.size=new Vector2(30f,1f);
    }

    private static void BuildSign(Scene scene)
    {
        const string platePath="Assets/_Project/art/UI/StationModules/FootballComingSoonPlate.png";
        var texture=new Texture2D(72,22,TextureFormat.RGBA32,false);
        var pixels=new Color32[72*22];
        for(int y=0;y<22;y++) for(int x=0;x<72;x++)
        {
            bool corner=(x<2 || x>69)&&(y<2 || y>19);
            bool edge=x<2 || x>69 || y<2 || y>19;
            pixels[y*72+x]=corner?new Color32(0,0,0,0):edge?new Color32(68,85,91,255):new Color32(17,29,35,255);
            if((x==4 || x==67)&&(y==4 || y==17)) pixels[y*72+x]=new Color32(147,170,173,255);
            if(x>=7&&x<=64&&y==3) pixels[y*72+x]=new Color32(224,172,65,255);
        }
        texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(platePath,texture.EncodeToPNG()); Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(platePath);
        var importer=(TextureImporter)AssetImporter.GetAtPath(platePath);
        importer.textureType=TextureImporterType.Sprite; importer.spritePixelsPerUnit=16; importer.filterMode=FilterMode.Point; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        var sign=new GameObject("FootballComingSoon"); SceneManager.MoveGameObjectToScene(sign,scene);
        sign.transform.SetParent(scene.GetRootGameObjects().Single(r=>r.name=="FOOTBALL").transform,false);
        sign.transform.position=new Vector3(-18f,3.1f,-1f);
        var plate=sign.AddComponent<SpriteRenderer>(); plate.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(platePath);
        plate.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        plate.sortingLayerName="UI"; plate.sortingOrder=75;
        var label=new GameObject("Inscription",typeof(TextMeshPro)); label.transform.SetParent(sign.transform,false);
        var text=label.GetComponent<TextMeshPro>();
        text.font=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/Stations/CharacterSelectionWindow.prefab").GetComponentInChildren<TMP_Text>(true).font;
        text.fontSize=3.1f; text.alignment=TextAlignmentOptions.Center; text.color=new Color(1f,.79f,.35f); text.text="В РАЗРАБОТКЕ";
        text.rectTransform.sizeDelta=new Vector2(4.05f,.8f); text.rectTransform.localPosition=new Vector3(0,.08f,-.01f);
        text.GetComponent<MeshRenderer>().sortingLayerName="UI"; text.GetComponent<MeshRenderer>().sortingOrder=76;
        Set(label.AddComponent<LocalizedText>(),"localizationKey","bunker.football_coming_soon");
    }
    public static void Audit()
    {
        var scene=SceneManager.GetSceneByPath(ScenePath);
        bool preview=!scene.IsValid() || !scene.isLoaded;
        if(preview) scene=EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var components=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToArray();
            var result=new StringBuilder();
            foreach(var c in components.Where(c=>c is IBunkerInteractable || c is Subject42.Bunker.Gallery.EnemyGalleryController || c is FootballMinigame || c is FootballMinigameHUD || c is BunkerRoomAccess || c is BunkerCursorInteractor))
            {
                result.AppendLine(c.GetType().Name+" "+PathOf(c.transform)+" pos="+c.transform.position+" active="+c.gameObject.activeInHierarchy);
                foreach(var col in c.GetComponentsInChildren<Collider2D>(true).Take(12)) result.AppendLine("  collider "+PathOf(col.transform)+" "+col.GetType().Name+" bounds="+col.bounds+" trigger="+col.isTrigger);
                foreach(var renderer in c.GetComponentsInChildren<SpriteRenderer>(true).Where(s=>s.sprite!=null).Take(8)) result.AppendLine("  sprite "+renderer.name+" "+renderer.sprite.name+" bounds="+renderer.bounds+" material="+AssetDatabase.GetAssetPath(renderer.sharedMaterial));
            }
            result.AppendLine("--- WORLD TEXT / ARROWS ---");
            foreach(var c in components.Where(c=>c is TMP_Text || c is TextMesh || c is SpriteRenderer && c.name.IndexOf("arrow",StringComparison.OrdinalIgnoreCase)>=0))
                if(c is not TMP_Text tmp || tmp.GetComponentInParent<Canvas>()==null)
                    result.AppendLine(c.GetType().Name+" "+PathOf(c.transform)+" active="+c.gameObject.activeInHierarchy+" pos="+c.transform.position+" text="+(c is TMP_Text text?text.text:c is TextMesh mesh?mesh.text:""));
            result.AppendLine("--- FOOTBALL TREE / GATES ---");
            foreach(var t in components.OfType<Transform>().Where(t=>t.root.name=="FOOTBALL" && PathOf(t).Count(c=>c=='/')<4))
                result.AppendLine(PathOf(t)+" active="+t.gameObject.activeSelf+" pos="+t.position);
            foreach(var gate in components.OfType<BunkerGateVisual>())
                result.AppendLine("GATE "+PathOf(gate.transform)+" pos="+gate.transform.position);
            File.WriteAllText(Output+"/inventory.txt",result.ToString());
        }
        finally { if(preview) EditorSceneManager.ClosePreviewScene(scene); }
    }
}
#endif
