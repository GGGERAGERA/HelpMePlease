using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class BunkerCatAuthoring
{
    public const string CatPath = "Assets/_Project/prefabs/Characters/Cat.prefab";
    public const string HeartPath = "Assets/_Project/prefabs/Bunker/PF_CatHeartFX.prefab";
    private const string HeartSpritePath = "Assets/_Project/art/Sprites/CatHeart.png";

    [MenuItem("Tools/Subject42/Dev/Authoring/Bunker/Configure Cat")]
    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/MainBuild/MainMenu.unity")
            throw new InvalidOperationException("Open production MainMenu in Edit Mode.");
        if (scene.isDirty) throw new InvalidOperationException("Save existing scene edits before configuring the cat.");
        BuildHeart();
        var root = PrefabUtility.LoadPrefabContents(CatPath);
        try
        {
            root.tag = "Untagged";
            // This art prefab originally cloned the player. Keep only its physical footprint and sorting.
            foreach (var component in root.GetComponents<Component>())
                if (!(component is Transform) && !(component is Rigidbody2D) && !(component is CircleCollider2D) &&
                    !(component is SortingGroup) && !(component is CatWanderController) && !(component is CatPetInteractable))
                    UnityEngine.Object.DestroyImmediate(component);
            foreach (var particles in root.GetComponentsInChildren<ParticleSystem>(true)) particles.gameObject.SetActive(false);
            var body = root.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            body.gravityScale = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var circle = root.GetComponent<CircleCollider2D>();
            circle.isTrigger = false;
            circle.excludeLayers = 0;
            var wander = root.GetComponent<CatWanderController>() ?? root.AddComponent<CatWanderController>();
            var graphic = root.GetComponentsInChildren<SpriteRenderer>(true).Single(s => s.name == "Cat1P1");
            var so = new SerializedObject(wander);
            so.FindProperty("animator").objectReferenceValue = root.GetComponentInChildren<Animator>();
            so.FindProperty("graphic").objectReferenceValue = graphic;
            so.ApplyModifiedPropertiesWithoutUndo();
            var anchor = root.transform.Find("HeartAnchor");
            if (anchor == null) { anchor = new GameObject("HeartAnchor").transform; anchor.SetParent(root.transform, false); }
            anchor.localPosition = new Vector3(0, .9f, 0);
            var pet = root.GetComponent<CatPetInteractable>() ?? root.AddComponent<CatPetInteractable>();
            so = new SerializedObject(pet);
            so.FindProperty("promptText").stringValue = "bunker.cat.pet";
            so.FindProperty("wander").objectReferenceValue = wander;
            so.FindProperty("heartAnchor").objectReferenceValue = anchor;
            so.FindProperty("heartPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(HeartPath).GetComponent<CatHeartFx>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, CatPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var cat = all.Single(t => t.name == "Cat" && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == CatPath);
        var floors = all.Where(t => t.gameObject.activeInHierarchy && t.GetComponent<Tilemap>() != null &&
            (HasFloorAncestor(t) || t.name == "Tilemap2" || t.name.StartsWith("TilemapFloor", StringComparison.Ordinal))).Select(t => t.GetComponent<Tilemap>()).ToArray();
        if (floors.Length == 0) throw new InvalidOperationException("No existing bunker floor tilemaps found.");
        var catSo = new SerializedObject(cat.GetComponent<CatWanderController>());
        var array = catSo.FindProperty("walkableFloors"); array.arraySize = floors.Length;
        for (int i = 0; i < floors.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = floors[i];
        catSo.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(cat.GetComponent<CatWanderController>());

        // Reuse the shared HUD prompt artwork and component on the bunker's existing Canvas.
        var prompt = all.Select(t => t.GetComponent<InteractionPromptUI>()).FirstOrDefault(p => p != null);
        if (prompt == null)
        {
            var canvas = all.Single(t => t.name == "BunkerUI" && t.GetComponent<Canvas>() != null);
            var system = all.Single(t => t.name == "BunkerInteractionSystem");
            var hud = PrefabUtility.LoadPrefabContents("Assets/_Project/prefabs/UI/HUD/GameplayHUD.prefab");
            try
            {
                var source = hud.GetComponentInChildren<InteractionPromptUI>(true);
                var sourceSo = new SerializedObject(source);
                var sourcePanel = (GameObject)sourceSo.FindProperty("promptPanel").objectReferenceValue;
                var panel = UnityEngine.Object.Instantiate(sourcePanel, canvas, false);
                panel.name = "InteractionPrompt";
                prompt = system.gameObject.AddComponent<InteractionPromptUI>();
                var so = new SerializedObject(prompt);
                so.FindProperty("promptPanel").objectReferenceValue = panel;
                so.FindProperty("promptText").objectReferenceValue = panel.GetComponentInChildren<TextMeshProUGUI>(true);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            finally { PrefabUtility.UnloadPrefabContents(hud); }
        }
        var loadout = all.Select(t => t.GetComponent<BunkerPlayerLoadoutController>()).Single(c => c != null && c.gameObject.activeInHierarchy);
        var controlled = (Transform)new SerializedObject(loadout).FindProperty("controlledPlayerRoot").objectReferenceValue;
        var promptSo = new SerializedObject(prompt);
        promptSo.FindProperty("playerInteractor").objectReferenceValue = controlled.GetComponent<PlayerInteractor>();
        promptSo.ApplyModifiedPropertiesWithoutUndo();
        ((GameObject)promptSo.FindProperty("promptPanel").objectReferenceValue).SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[BunkerCat] Cat configured with shared interaction prompt and " + floors.Length + " existing floor tilemaps.");
    }

    private static bool HasFloorAncestor(Transform t)
    {
        for (var parent = t.parent; parent != null; parent = parent.parent)
            if (parent.name == "Floor") return true;
        return false;
    }

    private static void BuildHeart()
    {
        if (!File.Exists(HeartSpritePath))
        {
            // An authored 9x8 pixel sprite asset. No runtime texture or renderer creation.
            string[] pixels = { ".##...##.", "####.####", "#########", "#########", ".#######.", "..#####..", "...###...", "....#...." };
            var texture = new Texture2D(9, 8, TextureFormat.RGBA32, false);
            for (int y = 0; y < 8; y++) for (int x = 0; x < 9; x++)
                texture.SetPixel(x, 7-y, pixels[y][x] == '#' ? new Color32(255, 105, 142, 255) : new Color32(0, 0, 0, 0));
            texture.Apply();
            File.WriteAllBytes(HeartSpritePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(HeartSpritePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(HeartSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 20;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(HeartPath) != null) return;
        var heart = new GameObject("PF_CatHeartFX", typeof(SpriteRenderer), typeof(CatHeartFx));
        try
        {
            var renderer = heart.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HeartSpritePath);
            renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            renderer.sortingLayerName = "Player";
            renderer.sortingOrder = 5;
            PrefabUtility.SaveAsPrefabAsset(heart, HeartPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(heart); }
    }
}
