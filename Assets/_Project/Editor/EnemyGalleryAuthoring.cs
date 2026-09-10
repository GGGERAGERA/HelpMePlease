using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Subject42.Combat.OrbitalStation;
using Object = UnityEngine.Object;

public static class EnemyGalleryAuthoring
{
    public const string ScenePath = "Assets/_Project/Scenes/EnemyGallery.unity";
    private const string ProductionScene = "Assets/_Project/Scenes/MVP.unity";
    public const string PreparedVariantsPath = "Assets/_Project/prefabs/Enemies/PreparedVariants";

    // Current content, including future entries in the existing production data types.
    public static GameObject[] FindEnemies()
    {
        var found = new HashSet<GameObject>();
        if (AssetDatabase.IsValidFolder(PreparedVariantsPath))
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PreparedVariantsPath }))
                found.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
        foreach (string guid in AssetDatabase.FindAssets("t:EnemySpawnProfile"))
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var phase in profile.Phases)
                foreach (var entry in phase.enemies)
                    if (entry.enemyPrefab != null) found.Add(entry.enemyPrefab);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:StageProfileData"))
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageProfileData>(AssetDatabase.GUIDToAssetPath(guid));
            if (stage.BossPrefab != null) found.Add(stage.BossPrefab);
        }
        var roots = EditorBuildSettings.scenes.Where(s => s.enabled && s.path != ScenePath &&
            !s.path.Contains("Lab")).Select(s => s.path).ToArray();
        foreach (string path in AssetDatabase.GetDependencies(roots, true).Where(p => p.EndsWith(".prefab")))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var health = prefab.GetComponent<EnemyHealth>();
            if (health != null && health.enabled &&
                (prefab.GetComponents<EnemyMovement>().Any(m => m.enabled) ||
                 prefab.TryGetComponent<TurretEnemyBehaviour>(out var turret) && turret.enabled))
                found.Add(prefab);
        }
        if (found.Count == 0) throw new InvalidOperationException("No production enemies found.");
        foreach (var prefab in found)
            if (prefab.GetComponent<EnemyHealth>() == null)
                throw new InvalidOperationException("Spawn data contains a non-enemy: " + AssetDatabase.GetAssetPath(prefab));
        return found.OrderBy(p => p.name, StringComparer.Ordinal).ToArray();
    }

    [MenuItem("Tools/Subject42/Refresh Enemy Gallery")]
    public static void Refresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Refresh Enemy Gallery in Edit Mode.");
        var prefabs = FindEnemies();
        foreach (var prefab in prefabs)
            if (prefab.GetComponent<TurretEnemyBehaviour>() == null) ChooseAnimation(prefab.GetComponentInChildren<Animator>());
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            scene = System.IO.File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var gallery = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyGalleryController>(true)).SingleOrDefault();
        bool created = gallery == null;
        if (created) gallery = new GameObject("Enemy Gallery").AddComponent<EnemyGalleryController>();
        var old = gallery.Exhibits.ToList();
        var next = new List<EnemyGalleryController.Exhibit>();
        var occupied = old.Where(e => e.instance != null).Select(e => e.instance.transform.position).ToList();
        foreach (var prefab in prefabs)
        {
            var entry = old.FirstOrDefault(e => e.instance != null &&
                PrefabUtility.GetCorrespondingObjectFromSource(e.instance) == prefab);
            if (entry == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(gallery.transform, false);
                int slot = 0;
                Vector3 position;
                do { position = new Vector3((slot % 4 - 1.5f) * 8f, slot / 4 * 9f, 0); slot++; }
                while (occupied.Any(p => Vector3.Distance(p, position) < 4f));
                occupied.Add(position);
                instance.transform.position = position;
                entry = new EnemyGalleryController.Exhibit { instance = instance };
            }
            ConfigureEnemy(entry.instance);
            entry.animator = entry.instance.GetComponentInChildren<Animator>();
            entry.animationState = entry.animator != null ? ChooseAnimation(entry.animator) : "";
            if (prefab.TryGetComponent<TurretEnemyBehaviour>(out var productionTurret))
            {
                var pivot = new SerializedObject(productionTurret).FindProperty("aimPivot").objectReferenceValue;
                entry.motionPivot = entry.instance.GetComponentsInChildren<Transform>(true)
                    .Single(t => PrefabUtility.GetCorrespondingObjectFromSource(t) == pivot);
            }
            if (entry.label == null)
            {
                var bounds = VisualBounds(entry.instance);
                entry.label = Label(prefab.name, gallery.transform,
                    new Vector3(entry.instance.transform.position.x, bounds.min.y - .65f, 0), .26f);
            }
            next.Add(entry);
        }
        foreach (var entry in old.Except(next))
        {
            if (entry.instance != null) Undo.DestroyObjectImmediate(entry.instance);
            if (entry.label != null) Undo.DestroyObjectImmediate(entry.label);
        }
        var serialized = new SerializedObject(gallery);
        var entries = serialized.FindProperty("exhibits"); entries.arraySize = next.Count;
        for (int i = 0; i < next.Count; i++)
        {
            var value = entries.GetArrayElementAtIndex(i);
            value.FindPropertyRelative("instance").objectReferenceValue = next[i].instance;
            value.FindPropertyRelative("animator").objectReferenceValue = next[i].animator;
            value.FindPropertyRelative("animationState").stringValue = next[i].animationState;
            value.FindPropertyRelative("label").objectReferenceValue = next[i].label;
            value.FindPropertyRelative("motionPivot").objectReferenceValue = next[i].motionPivot;
        }
        if (created)
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Scriptable Objects/Characters/01_Gera.asset");
            var prefab = OrbitalPresentationConfig.Active.GetPlayerPrefab(character.characterPrefab);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.position = new Vector3(0, -5, 0);
            ConfigurePlayer(player);
            serialized.FindProperty("player").objectReferenceValue = player.GetComponent<CharacterMovement2D>();
            CreateEnvironment(player.transform, scene);
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = gallery.gameObject;
        Debug.Log($"Enemy Gallery refreshed: {next.Count} production prefab instances.");
    }

    private static void ConfigureEnemy(GameObject instance)
    {
        // Removal overrides are scene-local. Disabled collision scripts still receive Unity callbacks.
        foreach (var component in instance.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component is EnemyMovement || component is TurretEnemyBehaviour ||
                component is EnemyCollisionHandler || component is EnemyHealth)
                Object.DestroyImmediate(component);
            else if (component is not EnemyWhiteFlash && component is not EnemyIdentity)
                throw new InvalidOperationException($"Review new enemy component before exhibiting: {component?.GetType()} on {instance.name}");
        }
        foreach (var body in instance.GetComponentsInChildren<Rigidbody2D>(true))
        { body.simulated = false; Record(body); }
        foreach (var collider in instance.GetComponentsInChildren<Collider2D>(true))
        { collider.enabled = false; Record(collider); }
        foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
        {
            animator.enabled = true; animator.applyRootMotion = false; animator.fireEvents = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; Record(animator);
        }
    }

    private static void ConfigurePlayer(GameObject player)
    {
        // Use CharacterSpawner's real ORBITAL production variant; hide its entire combat subtree locally.
        foreach (var station in player.GetComponentsInChildren<OrbitalStationRuntime>(true))
        {
            if (station.gameObject == player) throw new InvalidOperationException("Station ownership changed; review Gallery isolation.");
            station.gameObject.SetActive(false); Record(station.gameObject);
        }
        foreach (var c in player.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!c.gameObject.activeInHierarchy) continue;
            if (c is EnemySpawner || c is PlayerHealth || c is PlayerPickupRadius ||
                c is PlayerCombatModifiers || c is PlayerInteractor || c is PlayerHitSound)
                Object.DestroyImmediate(c);
            else if (c is not CharacterMovement2D && c is not PlayerWhiteFlash && c is not Light2D)
                throw new InvalidOperationException($"Review new player component before exhibiting: {c?.GetType()}");
        }
    }

    private static string ChooseAnimation(Animator animator)
    {
        var overrides = animator != null ? animator.runtimeAnimatorController as AnimatorOverrideController : null;
        var controller = (overrides != null ? overrides.runtimeAnimatorController : animator != null ? animator.runtimeAnimatorController : null) as AnimatorController;
        if (controller == null)
            throw new InvalidOperationException("Review missing/override animation controller: " + (animator != null ? animator.transform.root.name + " / " + animator.runtimeAnimatorController : "no Animator"));
        var machine = controller.layers[0].stateMachine;
        var states = machine.states.Select(s => s.state).ToArray();
        bool Animated(AnimatorState s)
        {
            if (s.motion is not AnimationClip clip) return false;
            if (overrides != null) clip = overrides[clip];
            return AnimationUtility.GetObjectReferenceCurveBindings(clip).Any(b =>
            {
                var target = string.IsNullOrEmpty(b.path) ? animator.transform : animator.transform.Find(b.path);
                // A changing curve on an inactive alternate is not a visible animation.
                if (target == null) return false;
                var renderer = target.GetComponent<SpriteRenderer>();
                if (renderer == null || !renderer.enabled) return false;
                for (var t = target; t != animator.transform; t = t.parent) if (!t.gameObject.activeSelf) return false;
                return AnimationUtility.GetObjectReferenceCurve(clip, b).Select(k => k.value).Distinct().Count() > 1;
            });
        }
        var state = states.FirstOrDefault(s => s.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0 && Animated(s))
            ?? states.FirstOrDefault(s => (s.name.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.name.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0) && Animated(s))
            ?? machine.defaultState;
        if (!Animated(state)) throw new InvalidOperationException("No changing sprite animation: " + animator.name);
        return controller.layers[0].name + "." + state.name;
    }

    private static void CreateEnvironment(Transform player, Scene galleryScene)
    {
        // Copy actual gameplay camera/follow settings in Editor, with explicit player ownership.
        var production = SceneManager.GetSceneByPath(ProductionScene);
        bool opened = !production.IsValid() || !production.isLoaded;
        if (opened) production = EditorSceneManager.OpenScene(ProductionScene, OpenSceneMode.Additive);
        try
        {
            var source = production.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CameraFollow>(true)).Single();
            SceneManager.SetActiveScene(galleryScene);
            var rig = new GameObject("Gameplay Camera");
            var camera = rig.AddComponent<Camera>(); EditorUtility.CopySerialized(source.ControlledCamera, camera);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .18f, .19f);
            var follow = rig.AddComponent<CameraFollow>(); EditorUtility.CopySerialized(source, follow);
            follow.target = player; follow.offset = new Vector3(0, 0, -10);
            var so = new SerializedObject(follow); so.FindProperty("controlledCamera").objectReferenceValue = camera; so.ApplyModifiedPropertiesWithoutUndo();
            rig.tag = "MainCamera"; rig.transform.position = player.position + follow.offset;
            rig.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        }
        finally { if (opened) EditorSceneManager.CloseScene(production, true); }
        SceneManager.SetActiveScene(galleryScene);
        var light = new GameObject("Neutral Global Light").AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global; light.intensity = 1f;
        // Camera clear colour is an uninterrupted neutral floor; no textures compete with silhouettes.
        var room = new GameObject("Room Boundary");
        Wall(room.transform, new Vector2(-20, 4), new Vector2(1, 34));
        Wall(room.transform, new Vector2(20, 4), new Vector2(1, 34));
        Wall(room.transform, new Vector2(0, -13), new Vector2(41, 1));
        Wall(room.transform, new Vector2(0, 21), new Vector2(41, 1));
        Label("ENEMY GALLERY", room.transform, new Vector3(0, -8, 0), .45f);
        Label("WASD / arrows: move     Wheel: zoom     F1: names", room.transform, new Vector3(0, -9, 0), .25f);
    }

    private static void Wall(Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject("Boundary"); go.transform.SetParent(parent, false); go.transform.position = position;
        go.AddComponent<BoxCollider2D>().size = size;
    }
    private static GameObject Label(string text, Transform parent, Vector3 position, float size)
    {
        var go = new GameObject(text + " Label"); go.transform.SetParent(parent, false); go.transform.position = position;
        var label = go.AddComponent<TextMeshPro>(); label.text = text; label.fontSize = size * 10f;
        label.alignment = TextAlignmentOptions.Center; label.color = new Color(.72f, .76f, .77f);
        label.rectTransform.sizeDelta = new Vector2(7.5f, 1f);
        label.GetComponent<MeshRenderer>().sortingOrder = 100;
        return go;
    }
    private static Bounds VisualBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled).ToArray();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
    private static void Record(Object component) => PrefabUtility.RecordPrefabInstancePropertyModifications(component);
}
