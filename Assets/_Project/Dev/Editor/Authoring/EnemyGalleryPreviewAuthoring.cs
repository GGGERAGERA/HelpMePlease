#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Subject42.Bunker.Gallery;
using Object = UnityEngine.Object;

// Editor-only bake: gameplay prefabs are inputs, never runtime gallery instances.
[InitializeOnLoad]
public static class EnemyGalleryPreviewAuthoring
{
    private const string Folder = "Assets/_Project/prefabs/Bunker/EnemyGallery/Previews";
    private const string Request = "Artifacts/GeneratedQA/Phase4/gallery.request";
    static EnemyGalleryPreviewAuthoring() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        File.Delete(Request);
        try { Rebuild(); MigrateSceneBindings(); File.WriteAllText("Artifacts/GeneratedQA/Phase4/gallery.result", "PASS: authored gallery presentation baked"); }
        catch (Exception exception) { File.WriteAllText("Artifacts/GeneratedQA/Phase4/gallery.result", exception.ToString()); Debug.LogException(exception); }
    }

    [MenuItem("Tools/Subject42/Authoring/Rebuild Gallery Presentation")]
    public static void Rebuild()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/prefabs/Bunker/EnemyGallery", "Previews");
        string displayPath = "Assets/_Project/prefabs/Bunker/EnemyGallery/EnemyGalleryDisplay.prefab";
        var display = PrefabUtility.LoadPrefabContents(displayPath);
        try
        {
            var preview = display.GetComponentInChildren<EnemyGalleryPreviewController>(true);
            var settings = new SerializedObject(preview);
            int layer = ((Transform)settings.FindProperty("contentRoot").objectReferenceValue).gameObject.layer;
            string materialPath = "Assets/_Project/art/EnemyGalleryRoom/GallerySprite.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(material, materialPath); }
            settings.FindProperty("spriteMaterial").objectReferenceValue = material;
            settings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(display, displayPath);
            string targetPath = "Assets/_Project/prefabs/Bunker/EnemyGallery/EnemyGalleryTargetButton.prefab";
            var target = PrefabUtility.LoadPrefabContents(targetPath);
            try
            {
                var gallery = target.GetComponent<Subject42.Bunker.Gallery.EnemyGalleryController>();
                var data = new SerializedObject(gallery); var entries = data.FindProperty("enemies");
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    foreach (string field in new[] { "previewPrefab", "warningPrefab", "explosionPrefab", "projectilePrefab", "shockwavePrefab" })
                    {
                        var property = entry.FindPropertyRelative(field);
                        var source = property.objectReferenceValue as GameObject;
                        if (source == null) continue;
                        if (AssetDatabase.GetAssetPath(source).StartsWith(Folder + "/", StringComparison.Ordinal))
                        {
                            string sourceGuid = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source)).userData;
                            source = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(sourceGuid));
                            if (source == null) throw new InvalidOperationException("Preview source GUID is missing.");
                        }
                        property.objectReferenceValue = Bake(source, layer, material);
                    }
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(target, targetPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(target); }
        }
        finally { PrefabUtility.UnloadPrefabContents(display); }
        AssetDatabase.SaveAssets();
    }

    private static void MigrateSceneBindings()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string name in new[] { "MainMenu", "MVP" })
            {
                string path = "Assets/_Project/Scenes/MainBuild/" + name + ".unity";
                var scene = SceneManager.GetSceneByPath(path);
                if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                if (scene.isDirty) throw new InvalidOperationException("Save scene edits before Phase4 binding migration: " + name);
                ProductionSceneCompositionAuthoring.EnsureScene(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }

    private static GameObject Bake(GameObject source, int layer, Material material)
    {
        var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(source));
        try
        {
            root.SetActive(false);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                    PrefabUtility.UnpackPrefabInstance(t.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true).Reverse())
                if (component is not Light2D) Object.DestroyImmediate(component);
            foreach (var component in root.GetComponentsInChildren<Collider2D>(true)) Object.DestroyImmediate(component);
            foreach (var component in root.GetComponentsInChildren<Rigidbody2D>(true)) Object.DestroyImmediate(component);
            foreach (var component in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(component);
            foreach (var component in root.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(component);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) { t.gameObject.layer = layer; t.gameObject.tag = "Untagged"; }
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true)) renderer.sharedMaterial = material;
            foreach (var animator in root.GetComponentsInChildren<Animator>(true)) { animator.fireEvents = false; animator.applyRootMotion = false; }
            foreach (var particles in root.GetComponentsInChildren<ParticleSystem>(true)) { var main = particles.main; main.playOnAwake = false; }
            string path = Folder + "/" + Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(source)) + "_Preview.prefab";
            var preview = PrefabUtility.SaveAsPrefabAsset(root, path);
            var importer = AssetImporter.GetAtPath(path);
            importer.userData = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
            importer.SaveAndReimport();
            return preview;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif