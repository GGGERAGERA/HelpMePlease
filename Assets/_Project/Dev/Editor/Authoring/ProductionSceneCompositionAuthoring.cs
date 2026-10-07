#if UNITY_EDITOR
using System.Linq;
using System;

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ProductionSceneCompositionAuthoring
{
    public const string PrefabPath = "Assets/_Project/prefabs/Bootstrap/ProductionSceneComposition.prefab";

    public static ProductionSceneComposition EnsureScene(Scene scene)
    {
        var composition = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ProductionSceneComposition>(true)).FirstOrDefault();
        if (composition == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new System.InvalidOperationException("Authored production scene composition prefab is missing.");
            composition = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<ProductionSceneComposition>();
        }
        var serialized = new SerializedObject(composition);
        BindLocal<LocalizationService>("localization");
        BindLocal<AudioService>("audio");
        BindLocal<UnlockProgressService>("unlocks");
        BindLocal<BunkerStationProgressionService>("bunkerProgression");
        BindLocal<SceneTransitionOverlay>("transition");
        serialized.FindProperty("role").enumValueIndex = scene.name switch
        {
            "StartScreen" => (int)ProductionSceneComposition.SceneRole.StartScreen,
            "MainMenu" => (int)ProductionSceneComposition.SceneRole.Bunker,
            "MVP" => (int)ProductionSceneComposition.SceneRole.Gameplay,
            _ => (int)ProductionSceneComposition.SceneRole.ServicesOnly
        };
        BindLocal<CharacterSpawner>("characters");
        BindLocal<EnemySpawner>("enemies");
        BindLocal<GameplayAreaService>("gameplayArea");
        BindLocal<LevelModifiersApplier>("levelModifiers");
        BindLocal<HUDManager>("hud");
        BindLocal<CameraFollow>("cameraFollow");
        BindLocal<BunkerPlayerLoadoutController>("bunkerLoadout");
        BindLocal<BunkerSelectionSourceHub>("bunkerSelections");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return composition;

        void BindLocal<T>(string field) where T : Component
        {
            var local = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault(component => component.gameObject.activeInHierarchy &&
                    (component is not Behaviour behaviour || behaviour.enabled));
            if (local != null || field is "characters" or "enemies" or "gameplayArea" or "levelModifiers" or
                "hud" or "cameraFollow" or "bunkerLoadout" or "bunkerSelections")
                serialized.FindProperty(field).objectReferenceValue = local;
        }
    }

    private const string Subsystems = "Assets/_Project/prefabs/Bootstrap/";

    [MenuItem("Tools/Subject42/Author Production Scene Composition")]
    public static void AssembleProductionScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author scenes in Edit Mode.");
        if (Enumerable.Range(0, SceneManager.sceneCount).Any(index => SceneManager.GetSceneAt(index).isDirty))
            throw new InvalidOperationException("Save unsaved scene edits before authoring production composition.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var bunker = EditorSceneManager.OpenScene(ScenePath("MainMenu"));
            SaveService<CurrencyManager>(bunker, "CurrencyManager");
            SaveService<RunSelectionManager>(bunker, "RunSelectionManager");
            SaveService<MetaProgressionManager>(bunker, "MetaProgressionManager");
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Set(prefab.GetComponent<ProductionSceneComposition>(), "currency", Load<CurrencyManager>("CurrencyManager"));
                Set(prefab.GetComponent<ProductionSceneComposition>(), "selection", Load<RunSelectionManager>("RunSelectionManager"));
                Set(prefab.GetComponent<ProductionSceneComposition>(), "metaProgression", Load<MetaProgressionManager>("MetaProgressionManager"));
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            var system = Root(bunker, "SYSTEM");
            var player = Child(system, "Player"); var camera = Child(system, "Camera");
            var interaction = Child(system, "Interaction"); var ui = Child(system, "UI");
            Find<BunkerPlayerLoadoutController>(bunker).transform.SetParent(player, true);
            var controlledPlayer = (Transform)new SerializedObject(Find<BunkerPlayerLoadoutController>(bunker))
                .FindProperty("controlledPlayerRoot").objectReferenceValue;
            foreach (var visual in All<Transform>(bunker))
                if (visual.CompareTag("Player") && visual != controlledPlayer) visual.tag = "Untagged";
            // Existing controlled character / cat prefab roots keep their world transforms and GUIDs.
            foreach (var child in system.Cast<Transform>().ToArray())
                if (child.CompareTag("Player") || child.GetComponent<CatPetInteractable>() != null ||
                    child.GetComponentInChildren<CatWanderController>(true) != null) child.SetParent(player, true);
            var follow = Find<CameraFollow>(bunker);
            follow.transform.SetParent(camera, true);
            if (follow.ControlledCamera.GetComponent<AudioListener>() == null)
                follow.ControlledCamera.gameObject.AddComponent<AudioListener>();
            Find<BunkerCursorInteractor>(bunker).transform.SetParent(interaction, true);
            Find<BunkerContext>(bunker).transform.SetParent(interaction, true);
            foreach (var canvas in All<Canvas>(bunker).Where(value => value.transform.parent == system).ToArray())
                canvas.transform.SetParent(ui, true);
            Find<UnityEngine.EventSystems.EventSystem>(bunker).transform.SetParent(ui, true);
            Find<BunkerRunSummaryPresenter>(bunker).transform.SetParent(ui, true);
            var hub = Root(bunker, "HUB");
            foreach (var child in system.Cast<Transform>().Where(value => value.name is "Grid" or "OUTERZONE" or "Global Light 2D").ToArray())
                child.SetParent(hub, true);
            RemoveLocalServices(bunker);
            EnsureScene(bunker).transform.SetParent(Child(system, "Composition"), true);
            EditorSceneManager.SaveScene(bunker);

            var start = EditorSceneManager.OpenScene(ScenePath("StartScreen"));
            system = Root(start, "SYSTEM");
            Find<StartScreenController>(start).transform.SetParent(Child(system, "UI"), true);
            Find<Camera>(start).transform.SetParent(Child(system, "Camera"), true);
            Find<UnityEngine.EventSystems.EventSystem>(start).transform.SetParent(system.Find("UI"), true);
            foreach (var settings in All<AudioSettingsService>(start).ToArray()) UnityEngine.Object.DestroyImmediate(settings.gameObject);
            EnsureScene(start).transform.SetParent(Child(system, "Composition"), true);
            EditorSceneManager.SaveScene(start);

            var gameplay = EditorSceneManager.OpenScene(ScenePath("MVP"));
            system = Root(gameplay, "SYSTEM");
            var run = Find<RunFlowController>(gameplay).transform;
            run.name = "Run"; run.SetParent(system, true);
            Find<RunEndService>(gameplay).transform.SetParent(run, true);
            Find<GameOverManager>(gameplay).transform.SetParent(run, true);
            Find<LevelChoiceManager>(gameplay).transform.SetParent(run, true);
            var oldRunRoot = gameplay.GetRootGameObjects().FirstOrDefault(value => value.name == "RunSystems");
            if (oldRunRoot != null && oldRunRoot.transform.childCount == 0) UnityEngine.Object.DestroyImmediate(oldRunRoot);
            Find<CharacterSpawner>(gameplay).transform.SetParent(system, true);
            Find<UpgradeManager>(gameplay).transform.SetParent(system, true);
            var world = Find<WorldEventSpawner>(gameplay).transform; world.SetParent(system, true);
            var modifiers = Find<LevelModifiersApplier>(gameplay);
            modifiers.name = "Sector"; modifiers.transform.SetParent(world, true);
            var exploration = modifiers.GetComponent<ProductionExplorationSectorController>();
            if (exploration == null) exploration = modifiers.gameObject.AddComponent<ProductionExplorationSectorController>();
            Set(modifiers, "exploration", exploration);
            var enemies = Find<RunBossSpawner>(gameplay).transform;
            enemies.SetParent(system, true);
            var pipeline = enemies.GetComponent<EnemySpawner>();
            if (pipeline == null)
            {
                pipeline = enemies.gameObject.AddComponent<EnemySpawner>();
                EditorUtility.CopySerialized(Load<EnemySpawner>("GameplayEnemies"), pipeline);
            }
            Set(modifiers, "enemySpawner", pipeline);
            Set(Find<WorldAccelerationRule>(gameplay), "enemySpawner", pipeline);
            Set(pipeline, "gameplayArea", Find<GameplayAreaService>(gameplay));
            foreach (var anomaly in All<LevelAnomalyController>(gameplay)) Set(anomaly, "gameplayArea", Find<GameplayAreaService>(gameplay));
            Find<CameraFollow>(gameplay).transform.SetParent(Child(system, "Camera"), true);
            ui = Child(system, "UI");
            Find<HUDManager>(gameplay).transform.SetParent(ui, true);
            Find<WorldLootRewardReel>(gameplay).transform.SetParent(ui, true);
            Find<UnityEngine.EventSystems.EventSystem>(gameplay).transform.SetParent(ui, true);
            RemoveLocalServices(gameplay);
            EnsureScene(gameplay).transform.SetParent(Child(system, "Composition"), true);
            var links = SceneLinks(gameplay);
            foreach (string name in new[] { "Run", "Player", "Rewards", "World", "Enemies" })
            {
                var owner = system.Find(name);
                if (!PrefabUtility.IsPartOfPrefabInstance(owner.gameObject))
                    PrefabUtility.SaveAsPrefabAssetAndConnect(owner.gameObject, Subsystems + "Gameplay" + name + ".prefab", InteractionMode.AutomatedAction);
            }
            foreach (var link in links) Set(link.owner, link.path, link.value);
            EnsureScene(gameplay);
            EditorSceneManager.SaveScene(gameplay);

            foreach (string character in new[] { "Gera", "DiMag", "Vika" })
            {
                string path = "Assets/_Project/prefabs/Characters/" + character + ".prefab";
                var actor = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var spawner in actor.GetComponents<EnemySpawner>()) UnityEngine.Object.DestroyImmediate(spawner);
                    PrefabUtility.SaveAsPrefabAsset(actor, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(actor); }
            }
            AssetDatabase.SaveAssets();
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }

    private static string ScenePath(string name) => $"Assets/_Project/Scenes/MainBuild/{name}.unity";
    private static T Load<T>(string name) where T : Component => AssetDatabase.LoadAssetAtPath<GameObject>(Subsystems + name + ".prefab").GetComponent<T>();
    private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    private static T Find<T>(Scene scene) where T : Component => All<T>(scene).First(value => value.gameObject.activeInHierarchy);
    private static Transform Root(Scene scene, string name)
    {
        var root = scene.GetRootGameObjects().FirstOrDefault(value => value.name == name);
        if (root == null) { root = new GameObject(name); SceneManager.MoveGameObjectToScene(root, scene); }
        return root.transform;
    }
    private static Transform Child(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
        return child;
    }
    private static void SaveService<T>(Scene scene, string name) where T : Component
    {
        string path = Subsystems + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) PrefabUtility.SaveAsPrefabAsset(Find<T>(scene).gameObject, path);
    }
    private static void RemoveLocalServices(Scene scene)
    {
        foreach (var component in All<Component>(scene))
            if (component is CurrencyManager or RunSelectionManager or MetaProgressionManager)
                UnityEngine.Object.DestroyImmediate(component.gameObject);
    }
    private static void Set(UnityEngine.Object owner, string field, UnityEngine.Object value)
    {
        var data = new SerializedObject(owner);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(owner)) PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
    }
    private static List<(Component owner, string path, UnityEngine.Object value)> SceneLinks(Scene scene)
    {
        var links = new List<(Component, string, UnityEngine.Object)>();
        foreach (var component in All<Component>(scene))
        {
            if (component == null) continue;
            var property = new SerializedObject(component).GetIterator();
            while (property.NextVisible(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null &&
                    !EditorUtility.IsPersistent(property.objectReferenceValue) && property.name != "m_GameObject")
                    links.Add((component, property.propertyPath, property.objectReferenceValue));
        }
        return links;
    }
}
#endif
