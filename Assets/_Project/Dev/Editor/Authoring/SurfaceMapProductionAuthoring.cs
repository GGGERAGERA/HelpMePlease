#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>One-time, explicit authoring. No scene copying or asset discovery occurs at runtime.</summary>
[InitializeOnLoad]
public static class SurfaceMapProductionAuthoring
{
    public const string MapPath = "Assets/_Project/Data/SurfaceMap/SurfaceMap_MVP.asset";
    public const string BunkerPath = "Assets/_Project/Scenes/MainBuild/MainMenu.unity";
    private const string Data = "Assets/_Project/Data/SurfaceMap/";
    private const string Prefabs = "Assets/_Project/prefabs/UI/SurfaceMap/";
    static SurfaceMapProductionAuthoring() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        const string request = "Artifacts/SurfaceMap/author.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { Author(); File.WriteAllText("Artifacts/SurfaceMap/author-result.txt", "SUCCESS"); }
        catch (Exception e) { File.WriteAllText("Artifacts/SurfaceMap/author-result.txt", e.ToString()); Debug.LogException(e); }
    }
    private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    private static T One<T>(Scene scene) where T : Component => All<T>(scene).Single();
    private static T Read<T>(Object value, string field) => (T)value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(value);
    private static void Set(Object value, string field, Object reference)
    {
        var so = new SerializedObject(value); var property = so.FindProperty(field);
        if (property == null) throw new InvalidOperationException(value.GetType().Name + "." + field + " missing");
        property.objectReferenceValue = reference; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(value); if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
    }
    private static void Field(Object value, string field, object data)
    { value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(value, data); EditorUtility.SetDirty(value); if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value); }
    private static T Asset<T>(string path, Action<T> configure) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset; // Re-running authoring preserves designer edits.
        asset = ScriptableObject.CreateInstance<T>(); configure(asset); AssetDatabase.CreateAsset(asset, path); return asset;
    }
    [MenuItem("Tools/Subject42/Surface Map/Author MVP and Production Bunker")]
    public static void Author()
    {
        Directory.CreateDirectory(Data); Directory.CreateDirectory(Prefabs); AssetDatabase.Refresh();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        Scene reference = default;
        try
        {
            reference = EditorSceneManager.OpenScene("Assets/_Project/Dev/Labs/BunkerSimple/OLD/MainMenu.unity", OpenSceneMode.Additive);
            var bunker = SceneManager.GetSceneByPath(BunkerPath);
            if (!bunker.IsValid() || !bunker.isLoaded) bunker = EditorSceneManager.OpenScene(BunkerPath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(bunker);
            var starter = One<BunkerRunStarter>(bunker);
            var panels = One<BunkerPanelManager>(bunker);
            var selection = One<BunkerSelectionSourceHub>(bunker);
            var catalog = AssetDatabase.LoadAssetAtPath<BunkerSelectionCatalog>("Assets/_Project/Data/SurfaceMap/BunkerSelectionCatalog.asset");
            if (catalog == null) throw new InvalidOperationException("Production station catalog is not authored.");
            Set(selection, "catalog", catalog);

            var map = CreateMap(starter);
            var compositionAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ProductionSceneCompositionAuthoring.PrefabPath);
            Set(compositionAsset.GetComponent<ProductionSceneComposition>(), "surfaceMap", map);
            PrefabUtility.SavePrefabAsset(compositionAsset);
            var composition = ProductionSceneCompositionAuthoring.EnsureScene(bunker);
            Set(composition, "surfaceMap", map);

            var canvas = Read<SelectionPanelController>(panels, "selectionPanelController").GetComponentInParent<Canvas>();
            if (canvas == null) canvas = All<Canvas>(bunker).First(c => c.renderMode == RenderMode.ScreenSpaceOverlay);
            var font = All<TMP_Text>(reference).First(t => t.font != null).font;
            string mapPrefab = Prefabs + "PF_SurfaceMap.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(mapPrefab) == null) BuildMapPrefab(mapPrefab, font);
            var view = All<SurfaceMapView>(bunker).SingleOrDefault();
            if (view == null) view = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(mapPrefab), canvas.transform)).GetComponent<SurfaceMapView>();
            Set(panels, "surfaceMapPanel", view); view.gameObject.SetActive(false);

            var notifications = All<BunkerNotificationManager>(bunker).SingleOrDefault();
            string notificationsPrefab = Prefabs + "PF_BunkerNotifications.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(notificationsPrefab) == null)
                PrefabUtility.SaveAsPrefabAsset(One<BunkerNotificationManager>(reference).gameObject, notificationsPrefab);
            if (notifications == null) notifications = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(notificationsPrefab), canvas.transform)).GetComponent<BunkerNotificationManager>();

            var events = All<BunkerEventManager>(bunker).SingleOrDefault();
            if (events == null)
            {
                string path = Prefabs + "PF_BunkerEvents.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    var original = One<BunkerEventManager>(reference);
                    var root = new GameObject("BunkerEvents", typeof(RectTransform));
                    var imageRoot = Object.Instantiate(Read<GameObject>(original, "fullscreenRoot"), root.transform);
                    var component = root.AddComponent<BunkerEventManager>();
                    Set(component, "fullscreenRoot", imageRoot); Set(component, "fullscreenImage", imageRoot.GetComponentInChildren<Image>(true));
                    PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
                }
                events = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), canvas.transform)).GetComponent<BunkerEventManager>();
                var rect = events.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            }

            // Use the composition's prefab service reference: do not instantiate a second progression service in this scene.
            var context = All<BunkerContext>(bunker).SingleOrDefault();
            if (context == null) context = new GameObject("BunkerContext").AddComponent<BunkerContext>();
            SceneManager.MoveGameObjectToScene(context.gameObject, bunker);
            Set(context, "<Panels>k__BackingField", panels); Set(context, "<Notifications>k__BackingField", notifications);
            Set(context, "<Events>k__BackingField", events); Set(context, "<RunStarter>k__BackingField", starter);
            Set(context, "<StationProgression>k__BackingField", Read<BunkerStationProgressionService>(composition, "bunkerProgression"));
            Set(context, "playerLoadout", One<BunkerPlayerLoadoutController>(bunker));

            EnsureStation(bunker, BunkerStationType.Upgrade, "Assets/_Project/prefabs/Bunker/_newItems/p_bunkerUpgrade1.prefab", new Vector3(-5f, -3f));
            EnsureStation(bunker, BunkerStationType.AnomalyStabilizer, "Assets/_Project/prefabs/Bunker/p_AnomalyTable1 Variant 1.prefab", new Vector3(5f, -3f));
            string escapePrefab = "Assets/_Project/prefabs/Bunker/PF_EscapeProtocolStation.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(escapePrefab) == null)
            {
                var originalStation = All<BunkerStation>(reference).First(s => Read<BunkerStationType>(s,"stationType") == BunkerStationType.EscapeProtocol);
                var clone = Object.Instantiate(originalStation.gameObject);
                foreach (var station in clone.GetComponentsInChildren<BunkerStation>(true)) Set(station,"panelManager",panels);
                PrefabUtility.SaveAsPrefabAsset(clone,escapePrefab); Object.DestroyImmediate(clone);
            }
            EnsureStation(bunker, BunkerStationType.EscapeProtocol, escapePrefab, new Vector3(0f, -4f));
            EnsureOrbitalSlot(bunker, panels, canvas.transform);
            EnsureSummary(bunker, reference, canvas, font);
            ProductionSceneCompositionAuthoring.EnsureScene(bunker);
            var football = One<FootballMinigame>(bunker);
            Set(football, "cameraFollow", One<CameraFollow>(bunker));
            if (Read<BunkerGateVisual>(football, "entranceDoor") == null)
            {
                var gate = All<BunkerGateVisual>(bunker).Where(g => g != Read<BunkerGateVisual>(starter, "runGate"))
                    .OrderBy(g => Vector3.Distance(g.transform.position, football.PlayerStart.position)).FirstOrDefault();
                if (gate != null) Set(football, "entranceDoor", gate);
            }
            EditorSceneManager.MarkSceneDirty(bunker); EditorSceneManager.SaveScene(bunker);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (reference.IsValid() && reference.isLoaded) EditorSceneManager.CloseScene(reference, true);
            if (setup.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }
    private static SurfaceMapDefinition CreateMap(BunkerRunStarter starter)
    {
        var layout = AssetDatabase.LoadAssetAtPath<ExplorationSectorConfig>("Assets/_Project/Data/World/ExplorationSectorConfig.asset");
        var layouts = new ExplorationSectorConfig[3];
        for (int i = 0; i < 3; i++)
        {
            string path = Data + "Layout_" + "ACD"[i] + ".asset";
            layouts[i] = AssetDatabase.LoadAssetAtPath<ExplorationSectorConfig>(path);
            if (layouts[i] == null) { layouts[i] = Object.Instantiate(layout); Field(layouts[i], "targetAnomalyCoverage", new[] { .86f, .89f, .94f }[i]); AssetDatabase.CreateAsset(layouts[i], path); }
        }
        var bomberPrefabs = AssetDatabase.FindAssets("t:EnemySpawnProfile").Select(g => AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(AssetDatabase.GUIDToAssetPath(g)))
            .SelectMany(p => p.Phases).SelectMany(p => p.enemies).Where(e => e.enemyPrefab != null && e.enemyPrefab.GetComponent<EnemyBomberMovement>() != null)
            .Select(e => e.enemyPrefab).Distinct().ToArray();
        var eventPrefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/prefabs" }).Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Select(p => p.GetComponent<WorldEvent>()).Where(p => p != null && p.AllowedInSite).ToArray();
        var sectors = new SurfaceSectorDefinition[6];
        string[] ids = { "A1", "A2", "C1", "C2", "D1", "D2" };
        Vector2[] positions = { new(0,110),new(0,220),new(150,0),new(300,0),new(0,-110),new(150,-110) };
        for (int i = 0; i < sectors.Length; i++)
        {
            int index = i, branch = i / 2;
            sectors[i] = Asset<SurfaceSectorDefinition>(Data + "Sector_" + ids[i] + ".asset", sector =>
            {
                Field(sector, "id", ids[index]); Field(sector, "displayName", ids[index]); Field(sector, "mapPosition", positions[index]);
                Field(sector, "neighbours", new[] { ids[index % 2 == 0 ? index + 1 : index - 1] });
                Field(sector, "description", new[] { "Safer route / experience", "Risky route / Bombers / gold", "Events / anomaly territories" }[branch]);
                var parameters = new RunConfigParameters { layoutProfile = layouts[branch], threatProfile = layout.ThreatConfig,
                    worldRule = branch == 1 ? AssetDatabase.LoadAssetAtPath<WorldRuleData>("Assets/_Project/Data/World/Rules/WorldRule_Golden.asset") : Read<WorldRuleData>(starter,"startingWorldRule"),
                    localAnomaly = Read<LocalAnomalyData>(starter,"startingLocalAnomaly"), initialThreat = branch == 1 ? 15 + (index % 2) * 5 : branch == 2 ? 5 : 0,
                    threatGrowth = branch == 0 ? .75f : branch == 1 ? 1.3f : 1f, spawnPressure = branch == 0 ? .8f : branch == 1 ? 1.25f : 1f,
                    enemyHealth = branch == 0 ? .9f : branch == 1 ? 1.15f : 1f, experience = branch == 0 ? 1.35f : branch == 2 ? 1.1f : 1f,
                    gold = branch == 1 ? 1.5f : 1f, eventFrequency = branch == 2 ? 1.5f : 1f };
                if (branch == 1) parameters.enemyWeights = bomberPrefabs.Select(p => new RunPrefabWeight { prefab = p, multiplier = 2.5f }).ToArray();
                if (branch == 2)
                {
                    parameters.eventWeights = eventPrefabs.Select(p => new RunPrefabWeight { prefab = p, multiplier = p is FalseSignalEvent ? 3f : p is CorridorEvent ? 2f : .75f }).ToArray();
                    parameters.anomalyWeights = layout.NormalAnomalies.Select(p => new RunPrefabWeight { prefab = p, multiplier = p == layout.NormalAnomalies[0] ? 3f : 1f }).ToArray();
                }
                Field(sector, "parameters", parameters);
            });
        }
        return Asset<SurfaceMapDefinition>(MapPath, map => { Field(map,"id","surface_mvp"); Field(map,"sectors",sectors); Field(map,"startingSectors",new[]{"A1","C1","D1"}); });
    }
    private static void EnsureStation(Scene scene, BunkerStationType kind, string path, Vector3 offset)
    {
        if (All<BunkerStation>(scene).Any(s => Read<BunkerStationType>(s,"stationType") == kind)) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Station prefab missing: " + path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        // Place functional stations in the main accessible hub; later art placement can move these authored instances.
        instance.transform.position = Read<Transform>(One<BunkerPlayerLoadoutController>(scene),"controlledPlayerRoot").position + offset;
        var station = instance.GetComponentInChildren<BunkerStation>(true);
        if (station == null) throw new InvalidOperationException(path + " has no production station");
        Field(station,"stationType",kind);
    }
    private static void EnsureOrbitalSlot(Scene scene, BunkerPanelManager panels, Transform parent)
    {
        var slot = All<BunkerOrbitalSlotPanel>(scene).SingleOrDefault();
        if (slot == null) slot = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/Stations/OrbitalSlotPanel.prefab"),parent)).GetComponent<BunkerOrbitalSlotPanel>();
        Set(panels,"orbitalSlotPanel",slot); slot.gameObject.SetActive(false);
        if (!scene.GetRootGameObjects().Any(r => r.GetComponentsInChildren<Transform>(true).Any(t => t.name == "SlotMachine")))
        {
            var machine = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/art/SlotMachine.prefab"),scene);
            machine.transform.position = Read<Transform>(One<BunkerPlayerLoadoutController>(scene),"controlledPlayerRoot").position + new Vector3(8,-4);
        }
    }
    private static void EnsureSummary(Scene scene, Scene reference, Canvas canvas, TMP_FontAsset font)
    {
        if (All<BunkerRunSummaryPresenter>(scene).Length > 0) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/SurfaceMap/PF_BunkerRunSummary.prefab");
        PrefabUtility.InstantiatePrefab(prefab, scene);
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    private static TMP_Text Text(string name, Transform parent, Vector2 position, Vector2 size, string text, TMP_FontAsset font, float fontSize = 20)
    {
        var label = Rect(name,parent,position,size).gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = fontSize;
        label.text = text; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; label.color = Color.white; return label;
    }
    private static Button Button(string name,Transform parent,Vector2 position,Vector2 size,string text,TMP_FontAsset font)
    {
        var rect = Rect(name,parent,position,size); var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.2f,.27f,.32f);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; Text("Label",rect,Vector2.zero,size,text,font); return button;
    }
    private static void BuildMapPrefab(string path,TMP_FontAsset font)
    {
        var root = Rect("Surface Map",null,Vector2.zero,new Vector2(920,640)); root.gameObject.AddComponent<Image>().color = new Color(.055f,.07f,.09f,.98f);
        var view = root.gameObject.AddComponent<SurfaceMapView>(); Text("Title",root,new Vector2(0,285),new Vector2(700,50),"SURFACE MAP",font,28);
        var graph = Rect("Graph",root,new Vector2(-210,0),new Vector2(600,480));
        var node = Button("Node Template",graph,Vector2.zero,new Vector2(100,60),"Sector",font);
        var line = Rect("Line Template",graph,Vector2.zero,new Vector2(100,2)).gameObject.AddComponent<Image>(); line.color = new Color(.45f,.55f,.6f); line.raycastTarget = false;
        var details = Text("Details",root,new Vector2(285,0),new Vector2(255,440),"Select a sector",font,19); details.alignment = TextAlignmentOptions.TopLeft;
        var start = Button("Start",root,new Vector2(245,-275),new Vector2(230,50),"START RUN",font);
        var close = Button("Close",root,new Vector2(405,285),new Vector2(70,45),"X",font);
        Set(view,"graph",graph); Set(view,"nodeTemplate",node); Set(view,"lineTemplate",line); Set(view,"details",details); Set(view,"startButton",start); Set(view,"closeButton",close);
        node.gameObject.SetActive(false); line.gameObject.SetActive(false);
        Phase5PresentationAuthoring.AuthorMap(root.gameObject);
        PrefabUtility.SaveAsPrefabAsset(root.gameObject,path); Object.DestroyImmediate(root.gameObject);
    }
}
#endif
