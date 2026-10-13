#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class OrbitalLabsAuthoring
{
    private const string Production = "Assets/_Project/Scenes/MainBuild/MVP.unity";
    [MenuItem("Tools/Subject42/Dev/OrbitalLab/Open OrbitalRewardLab")]
    public static void OpenRewards() => Open(OrbitalRewardLabController.ScenePath);
    [MenuItem("Tools/Subject42/Dev/OrbitalLab/Open EnemyOrbitalLab")]
    public static void OpenEnemies() => Open(EnemyOrbitalLabController.ScenePath);

    [MenuItem("Tools/Subject42/Dev/OrbitalLab/Prepare three lab guns")]
    public static void PrepareLabGuns()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene();
        if (scene.path != EnemyOrbitalLabController.ScenePath) scene = EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath);
        var lab = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyOrbitalLabController>(true)).Single();
        lab.Needle.ProjectileVisual = EnsureLabShot(lab.PistolProjectileVisual, "NeedleProjectile", new Vector3(.95f, .28f, 1f), lab.Needle.Color, .09f);
        lab.Shotgun.ProjectileVisual = EnsureLabShot(lab.PistolProjectileVisual, "ShotgunPellet", Vector3.one * .45f, lab.Shotgun.Color, .07f);
        lab.Spear.ProjectileVisual = EnsureLabShot(lab.PistolProjectileVisual, "EnergySpear", new Vector3(2.5f, .7f, 1f), lab.Spear.Color, .14f);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }

    private static GameObject EnsureLabShot(GameObject source, string name, Vector3 scale, Color color, float trailTime)
    {
        string path = "Assets/_Project/Dev/Labs/OrbitalLab/" + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var shot = existing != null ? PrefabUtility.LoadPrefabContents(path) : (GameObject)PrefabUtility.InstantiatePrefab(source);
        shot.name = name; shot.transform.localScale = scale;
        foreach (var sprite in shot.GetComponentsInChildren<SpriteRenderer>(true)) sprite.color = Color.Lerp(color, Color.white, .45f);
        foreach (var particles in shot.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main; main.startColor = color; main.startLifetime = .08f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            particles.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local;
        }
        foreach (var light in shot.GetComponentsInChildren<Light2D>(true)) { light.color = color; light.intensity = .6f; }
        foreach (var trail in shot.GetComponentsInChildren<TrailRenderer>(true))
        {
            trail.time = trailTime; trail.widthMultiplier = name == "EnergySpear" ? .16f : .045f;
            trail.startColor = color; trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }
        var result = PrefabUtility.SaveAsPrefabAsset(shot, path);
        if (existing != null) PrefabUtility.UnloadPrefabContents(shot); else UnityEngine.Object.DestroyImmediate(shot);
        return result;
    }

    [MenuItem("Tools/Subject42/Dev/OrbitalLab/Prepare shield prototype in EnemyOrbitalLab")]
    public static void PrepareShieldPrototype()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath);
        var lab = Find<EnemyOrbitalLabController>(scene);
        lab.PistolProjectileVisual = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Dev/Labs/OrbitalLab/LegacyPistolProjectileVisual.prefab");
        lab.PursuerPrefab = lab.EnemyPrefabs.First(p => p.GetComponent<EnemyChaseMovement>() != null);
        var config = AssetDatabase.LoadAssetAtPath<Subject42.Combat.OrbitalStation.OrbitalPresentationConfig>(
            "Assets/_Project/Data/Orbital/OrbitalPresentationConfig.asset");
        const string prefabPath = "Assets/_Project/Dev/Labs/OrbitalLab/ShieldBearer.prefab";
        lab.ShieldBearerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (lab.ShieldBearerPrefab == null)
        {
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(lab.PursuerPrefab, scene);
            PrefabUtility.UnpackPrefabInstance(enemy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            enemy.name = "Lab Shield Bearer";
            enemy.GetComponent<EnemyHealth>().maxHealth = 100f;
            var movement = new SerializedObject(enemy.GetComponent<EnemyChaseMovement>());
            movement.FindProperty("normalSpeed").floatValue = .85f;
            movement.FindProperty("aggroSpeed").floatValue = .85f;
            movement.FindProperty("flipVisual").boolValue = false;
            movement.ApplyModifiedPropertiesWithoutUndo();
            var shield = enemy.AddComponent<EnemyOrbitalLabShield>();
            shield.ShieldPivot = new GameObject("Shield facing (player body)").transform;
            shield.ShieldPivot.SetParent(enemy.transform, false);
            shield.ShieldArc = CreateLine(shield.ShieldPivot, "Frontal shield 120 degrees", config.VisualMaterial,
                new Color(.2f, .85f, 1f), .13f, 25, false);
            shield.ShieldArc.useWorldSpace = false;
            var sparks = new GameObject("Blocked hit sparks");
            sparks.transform.SetParent(enemy.transform, false);
            shield.BlockSparks = sparks.AddComponent<ParticleSystem>();
            var main = shield.BlockSparks.main;
            main.playOnAwake = false; main.loop = false; main.duration = .2f;
            main.startLifetime = .16f; main.startSpeed = 2.6f; main.startSize = .07f;
            main.startColor = new Color(1f, .85f, .25f); main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = shield.BlockSparks.emission; emission.enabled = false;
            var shape = shield.BlockSparks.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .035f;
            var renderer = sparks.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = config.VisualMaterial;
            renderer.sortingOrder = 100;
            lab.ShieldBearerPrefab = PrefabUtility.SaveAsPrefabAsset(enemy, prefabPath);
            UnityEngine.Object.DestroyImmediate(enemy);
        }
        UpgradeShieldPrefab(prefabPath, config);
        const string shooterPath = "Assets/_Project/Dev/Labs/OrbitalLab/ShieldShooter.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(shooterPath) == null)
        {
            var source = lab.EnemyPrefabs.First(p => p.GetComponent<EnemyShooterMovement>() != null);
            var shooter = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            PrefabUtility.UnpackPrefabInstance(shooter, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            shooter.name = "Lab Shield Shooter"; shooter.transform.position = Vector3.zero;
            var shield = shooter.AddComponent<EnemyOrbitalLabShield>();
            shield.LocalShieldCenter = new Vector2(0f, .78f);
            shield.ShieldDistance = 1.12f;
            shield.ShieldPivot = new GameObject("Shield facing (player body)").transform;
            shield.ShieldPivot.SetParent(shooter.transform, false);
            shield.ShieldArc = CreateLine(shield.ShieldPivot, "Frontal shield", config.VisualMaterial, EnemyOrbitalLabShield.Blue, .13f, 25, false);
            shield.ShieldArc.useWorldSpace = false;
            var sparks = new GameObject("Blocked hit sparks"); sparks.transform.SetParent(shooter.transform, false);
            shield.BlockSparks = sparks.AddComponent<ParticleSystem>();
            var main = shield.BlockSparks.main; main.playOnAwake = false; main.loop = false;
            main.startLifetime = .16f; main.startSpeed = 2.6f; main.startSize = .07f; main.startColor = new Color(1f, .85f, .25f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = shield.BlockSparks.emission; emission.enabled = false;
            var shape = shield.BlockSparks.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .035f;
            sparks.GetComponent<ParticleSystemRenderer>().sharedMaterial = config.VisualMaterial;
            PrefabUtility.SaveAsPrefabAsset(shooter, shooterPath); UnityEngine.Object.DestroyImmediate(shooter);
        }
        UpgradeShieldPrefab(shooterPath, config);
        lab.ShieldShooterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(shooterPath);
        lab.BreakableShieldBearerPrefab = EnsureBreakableVariant(prefabPath, "Assets/_Project/Dev/Labs/OrbitalLab/BreakableShieldBearer.prefab", scene);
        lab.BreakableShieldShooterPrefab = EnsureBreakableVariant(shooterPath, "Assets/_Project/Dev/Labs/OrbitalLab/BreakableShieldShooter.prefab", scene);
        if (lab.CenterRange == null)
            lab.CenterRange = CreateLine(lab.transform, "Station center maximum distance", config.VisualMaterial,
                new Color(.7f, .4f, .9f, .35f), .025f, 64, true);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static GameObject EnsureBreakableVariant(string source, string path, Scene scene)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source), scene);
        instance.name = Path.GetFileNameWithoutExtension(path);
        var shield = instance.GetComponent<EnemyOrbitalLabShield>();
        shield.Kind = EnemyOrbitalLabShield.ShieldKind.Breakable; shield.MaximumBlockedHits = 4;
        shield.ResetForSpawn();
        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
        UnityEngine.Object.DestroyImmediate(instance);
        return prefab;
    }

    private static void UpgradeShieldPrefab(string path, Subject42.Combat.OrbitalStation.OrbitalPresentationConfig config)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var shield = root.GetComponent<EnemyOrbitalLabShield>();
            var movement = new SerializedObject(root.GetComponent<EnemyMovement>());
            var visual = (Transform)movement.FindProperty("visualRoot").objectReferenceValue;
            shield.BodySorting = visual.GetComponentInChildren<UnityEngine.Rendering.SortingGroup>(true);
            shield.BodySorting.enabled = true;
            var sorting = new SerializedObject(shield.BodySorting);
            sorting.FindProperty("m_SortAtRoot").boolValue = false;
            sorting.ApplyModifiedPropertiesWithoutUndo();
            shield.ShieldArc.useWorldSpace = false;
            shield.ShieldArc.numCapVertices = shield.ShieldArc.numCornerVertices = 3;
            if (shield.ShieldCollider == null) shield.ShieldCollider = shield.ShieldArc.gameObject.AddComponent<EdgeCollider2D>();
            shield.ShieldCollider.isTrigger = true;
            if (shield.DamageCracks == null || shield.DamageCracks.Length == 0)
            {
                shield.DamageCracks = new LineRenderer[3];
                for (int i = 0; i < shield.DamageCracks.Length; i++)
                {
                    shield.DamageCracks[i] = CreateLine(shield.ShieldPivot, "Damage crack " + (i + 1), config.VisualMaterial, new Color(.025f, .04f, .07f), .025f, 4, false);
                    shield.DamageCracks[i].useWorldSpace = false;
                }
            }
            if (shield.BreakShards == null)
            {
                var shards = new GameObject("Shield break fragments"); shards.transform.SetParent(root.transform, false);
                shield.BreakShards = shards.AddComponent<ParticleSystem>();
                var main = shield.BreakShards.main; main.playOnAwake = false; main.loop = false; main.duration = .4f;
                main.startLifetime = .35f; main.startSpeed = 0f; main.startSize3D = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var emission = shield.BreakShards.emission; emission.enabled = false;
                var shape = shield.BreakShards.shape; shape.enabled = false;
                shards.GetComponent<ParticleSystemRenderer>().sharedMaterial = config.VisualMaterial;
            }
            if (shield.FeedbackSource == null)
            {
                var feedback = new GameObject("Shield sound"); feedback.transform.SetParent(root.transform, false);
                shield.FeedbackSource = feedback.AddComponent<AudioSource>();
                shield.FeedbackSource.playOnAwake = false; shield.FeedbackSource.volume = .5f; shield.FeedbackSource.dopplerLevel = 0;
                var audio = AssetDatabase.LoadAssetAtPath<AudioService>("Assets/_Project/prefabs/Bootstrap/AudioService.prefab");
                shield.FeedbackSource.outputAudioMixerGroup = (UnityEngine.Audio.AudioMixerGroup)new SerializedObject(audio).FindProperty("sfxMixerGroup").objectReferenceValue;
            }
            if (shield.BreakSound == null)
                shield.BreakSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/AudioEffects/packs/kenney_impact-sounds/impactGlass_medium_000.mp3");
            shield.ResetForSpawn();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static LineRenderer CreateLine(Transform parent, string name, Material material, Color color,
        float width, int points, bool loop)
    {
        var root = new GameObject(name); root.transform.SetParent(parent, false);
        var line = root.AddComponent<LineRenderer>(); line.sharedMaterial = material;
        line.startColor = line.endColor = color; line.widthMultiplier = width;
        line.positionCount = points; line.loop = loop; line.useWorldSpace = true;
        line.sortingOrder = 90;
        return line;
    }
    private static void Open(string path)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(path)) CreateMissingScenes();
        EditorSceneManager.OpenScene(path);
    }

    public static GameObject[] FindProductionEnemies()
    {
        // Restrict to actual production dependencies, excluding unused gallery/prepared variants and bosses.
        return AssetDatabase.GetDependencies(Production, true).Where(p => p.EndsWith(".prefab"))
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(p => p != null && p.TryGetComponent<EnemyHealth>(out var health) && health.enabled && !health.IsBoss &&
                (p.GetComponents<EnemyMovement>().Any(m => m.enabled) ||
                 p.TryGetComponent<TurretEnemyBehaviour>(out var turret) && turret.enabled))
            .Distinct().OrderBy(p => p.name, StringComparer.Ordinal).ToArray();
    }

    [MenuItem("Tools/Subject42/Dev/OrbitalLab/Refresh production enemy list")]
    public static void RefreshEnemies()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var previous = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(EnemyOrbitalLabController.ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath, OpenSceneMode.Additive);
        var lab = Find<EnemyOrbitalLabController>(scene);
        lab.EnemyPrefabs = FindProductionEnemies();
        EditorUtility.SetDirty(lab);
        ProductionSceneCompositionAuthoring.EnsureScene(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        if (previous.IsValid()) SceneManager.SetActiveScene(previous);
    }

    [MenuItem("Tools/Subject42/Dev/OrbitalLab/Create missing scenes")]
    public static void CreateMissingScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var previous = SceneManager.GetActiveScene();
        var lights = UnityEngine.Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None)
            .Where(l => l.enabled && l.lightType == Light2D.LightType.Global).ToArray();
        foreach (var light in lights) light.enabled = false;
        var production = EditorSceneManager.OpenPreviewScene(Production);
        foreach (var light in production.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light2D>(true)))
            light.enabled = false;
        try
        {
            Create<OrbitalRewardLabController>(OrbitalRewardLabController.ScenePath, production);
            Create<EnemyOrbitalLabController>(EnemyOrbitalLabController.ScenePath, production);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(production);
            foreach (var light in lights) if (light != null) light.enabled = true;
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
        AssetDatabase.Refresh();
    }

    private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<T>(true)).First();

    private static void Create<T>(string path, Scene production) where T : OrbitalLabSession
    {
        if (File.Exists(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var lab = new GameObject(typeof(T).Name).AddComponent<T>();
            lab.Character = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset");
            var sourceRig = Find<CameraFollow>(production);
            var rig = new GameObject("Production Camera");
            var camera = rig.AddComponent<Camera>(); EditorUtility.CopySerialized(sourceRig.ControlledCamera, camera);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .18f, .19f);
            var follow = rig.AddComponent<CameraFollow>(); EditorUtility.CopySerialized(sourceRig, follow);
            follow.target = null; follow.offset = new Vector3(0, 0, -10);
            Set(follow, "controlledCamera", camera);
            rig.tag = "MainCamera"; rig.transform.position = follow.offset;
            rig.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            lab.CameraRig = follow;
            var light = new GameObject("Neutral arena light").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            var bounds = new GameObject("Arena bounds (60 x 60)");
            foreach (var wall in new[] { new Vector4(-30, 0, 1, 61), new Vector4(30, 0, 1, 61), new Vector4(0, -30, 61, 1), new Vector4(0, 30, 61, 1) })
            {
                var go = new GameObject("Wall"); go.transform.SetParent(bounds.transform);
                go.transform.position = new Vector3(wall.x, wall.y, 0);
                go.AddComponent<BoxCollider2D>().size = new Vector2(wall.z, wall.w);
            }
            if (lab is OrbitalRewardLabController)
            {
                var sourceManager = Find<UpgradeManager>(production);
                var sourcePanel = (UpgradePanelView)new SerializedObject(sourceManager).FindProperty("upgradePanelView").objectReferenceValue;
                var canvas = new GameObject("Production Reward Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                EditorUtility.CopySerialized(sourcePanel.GetComponentInParent<Canvas>(), canvas.GetComponent<Canvas>());
                EditorUtility.CopySerialized(sourcePanel.GetComponentInParent<CanvasScaler>(), canvas.GetComponent<CanvasScaler>());
                canvas.GetComponent<Canvas>().worldCamera = camera;
                var panel = UnityEngine.Object.Instantiate(sourcePanel, canvas.transform, false);
                panel.name = "Production Upgrade Panel";
                var logic = new GameObject("Production Reward Logic");
                var applier = logic.AddComponent<UpgradeApplier>();
                var manager = logic.AddComponent<UpgradeManager>();
                EditorUtility.CopySerialized(sourceManager, manager);
                Set(manager, "upgradePanelView", panel); Set(manager, "upgradeApplier", applier);
                lab.Rewards = manager;
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
            if (lab is EnemyOrbitalLabController enemyLab)
            {
                enemyLab.EnemyPrefabs = FindProductionEnemies();
                if (enemyLab.EnemyPrefabs.Length == 0) throw new InvalidOperationException("No production enemy references found.");
            }
            ProductionSceneCompositionAuthoring.EnsureScene(scene);
            EditorSceneManager.SaveScene(scene, path);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }
    private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
    { var so = new SerializedObject(target); so.FindProperty(property).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
}
#endif
