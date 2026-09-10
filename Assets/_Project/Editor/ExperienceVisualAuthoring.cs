using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Non-destructive, repeatable import of the labelled XP reference artwork.</summary>
public static class ExperienceVisualAuthoring
{
    public const string Root = "Assets/_Project/art/XP";
    public const string PickupPath = "Assets/_Project/prefabs/Pickups/p_Hex1.prefab";
    public const string SourcePath = "Assets/_Project/art/hex_sprites.png";
    // Individually inspected object bounds, top-left image coordinates; labels lie outside.
    // Each image has a different extent and painted block lattice (not a sheet-wide grid).
    private static readonly RectInt[] Bounds = {
        new(180, 173, 196, 220), new(575, 132, 297, 261),
        new(1051, 173, 224, 221), new(160, 540, 236, 263),
        new(581, 524, 286, 294), new(999, 530, 332, 280)
    };
    private static readonly Vector2Int[] Sizes = {
        new(10, 11), new(15, 13), new(11, 11),
        new(12, 13), new(14, 15), new(17, 14)
    };
    public static readonly string[] Names = {
        "XP_HexCore", "XP_BlueCorona", "XP_SplitCore",
        "XP_VioletLayers", "XP_OrbitalCore", "XP_UnstableCore"
    };

    [MenuItem("Tools/Subject42/XP/Rebuild visual assets")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before rebuilding XP assets.");
        Directory.CreateDirectory(Root);
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        source.LoadImage(File.ReadAllBytes(SourcePath));
        var atlas = new Texture2D(192, 32, TextureFormat.RGBA32, false);
        atlas.SetPixels32(new Color32[192 * 32]);
        var rects = new SpriteRect[7];
        for (int i = 0; i < 6; i++)
        {
            RectInt b = Bounds[i];
            Vector2Int size = Sizes[i];
            int ox = i * 32 + (32 - size.x) / 2;
            int oy = (32 - size.y) / 2;
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                // Sample the interior of each painted block, never interpolate its soft edge.
                int sx = b.x + Mathf.FloorToInt((x + 0.5f) * b.width / size.x);
                int sy = source.height - 1 - b.y - Mathf.FloorToInt((y + 0.5f) * b.height / size.y);
                Color color = source.GetPixel(sx, sy);
                // The reference has an opaque near-black background and baked halo.
                // Discard background/halo; retain original RGB and strictly binary coverage.
                color.a = Mathf.Max(color.r, color.g, color.b) >= 0.23f ? 1 : 0;
                atlas.SetPixel(ox + x, oy + size.y - 1 - y, color);
            }
            rects[i] = Rect(Names[i], new Rect(ox, oy, size.x, size.y), i);
        }
        atlas.SetPixel(0, 0, Color.white);
        rects[6] = Rect("XP_Pixel", new Rect(0, 0, 1, 1), 6);
        string atlasPath = Root + "/XP_Cores.png";
        File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(atlas);
        AssetDatabase.Refresh();
        var importer = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 32;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(atlasPath).OfType<Sprite>().ToDictionary(s => s.name);
        Color[] colors = { Color.cyan, new(.2f, .6f, 1), new(.2f, 1, .55f),
            new(.6f, .3f, 1), new(.35f, 1, .7f), new(.7f, .35f, 1) };
        int[] weights = { 4, 3, 4, 1, 4, 0 };
        var presets = new ExperienceVisualPreset[6];
        for (int i = 0; i < 6; i++)
        {
            presets[i] = Asset<ExperienceVisualPreset>(Root + "/" + Names[i] + ".asset");
            presets[i].sprite = sprites[Names[i]];
            presets[i].sparkColor = colors[i];
            presets[i].productionWeight = weights[i];
            EditorUtility.SetDirty(presets[i]);
        }
        string materialPath = Root + "/XP_Pixel.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Subject42/XP Pixel"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.SetFloat("_Pulse", 0.04f);
        EditorUtility.SetDirty(material);
        var effect = BuildEffect(sprites["XP_Pixel"], material);
        var root = PrefabUtility.LoadPrefabContents(PickupPath);
        try
        {
            // Retain the existing renderer/transform IDs for prefab variants and scene references.
            var core = root.transform.Find("hex1")?.GetComponent<SpriteRenderer>() ??
                root.transform.Find("Visual").GetComponent<SpriteRenderer>();
            foreach (var animator in root.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
            foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true)) Object.DestroyImmediate(trail.gameObject);
            foreach (var light in root.GetComponentsInChildren<Light2D>(true)) Object.DestroyImmediate(light.gameObject);
            core.name = "Visual";
            core.transform.localPosition = new Vector3(0, 0.125f, 0);
            core.transform.localRotation = Quaternion.identity;
            core.transform.localScale = Vector3.one;
            core.sprite = presets[0].sprite;
            core.color = Color.white;
            core.sharedMaterial = material;
            var sparkTransform = root.transform.Find("Pixel Spark");
            var spark = sparkTransform != null ? sparkTransform.GetComponent<SpriteRenderer>() :
                NewRenderer("Pixel Spark", root.transform, sprites["XP_Pixel"], material);
            spark.enabled = false;
            spark.sortingOrder = 1;
            var visual = root.GetComponent<ExperiencePickupVisual>() ?? root.AddComponent<ExperiencePickupVisual>();
            var data = new SerializedObject(visual);
            data.FindProperty("core").objectReferenceValue = core;
            data.FindProperty("spark").objectReferenceValue = spark;
            data.FindProperty("pickupEffect").objectReferenceValue = effect;
            var array = data.FindProperty("presets");
            array.arraySize = presets.Length;
            for (int i = 0; i < presets.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = presets[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            var pickup = new SerializedObject(root.GetComponent<ExperiencePickup>());
            pickup.FindProperty("visual").objectReferenceValue = visual;
            pickup.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PickupPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        // Old variants only differed by multiplying the blue source tint. Remove those overrides.
        foreach (string variantPath in new[] { "p_Hex1 Variant.prefab", "p_Hex1 Variant 1.prefab" })
        {
            var variant = PrefabUtility.LoadPrefabContents("Assets/_Project/prefabs/Pickups/" + variantPath);
            try
            {
                var modifications = PrefabUtility.GetPropertyModifications(variant);
                PrefabUtility.SetPropertyModifications(variant,
                    modifications.Where(p => !p.propertyPath.StartsWith("m_Color.")).ToArray());
                PrefabUtility.SaveAsPrefabAsset(variant, "Assets/_Project/prefabs/Pickups/" + variantPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(variant); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("XP visual assets rebuilt: six presets, five production shapes, Point/32 PPU, no animator/trail/light.");
    }

    private static SpriteRect Rect(string name, Rect rect, int index) => new()
    {
        name = name, rect = rect, alignment = SpriteAlignment.Center, pivot = Vector2.one * 0.5f,
        spriteID = new GUID("420000000000000000000000000000" + index.ToString("D2"))
    };

    private static T Asset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static SpriteRenderer NewRenderer(string name, Transform parent, Sprite sprite, Material material)
    {
        var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(parent, false);
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        return renderer;
    }

    private static ExperiencePickupEffect BuildEffect(Sprite pixel, Material material)
    {
        string path = "Assets/_Project/prefabs/Pickups/XP_PickupFlash.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing.GetComponent<ExperiencePickupEffect>();
        var root = new GameObject("XP_PickupFlash");
        var group = root.AddComponent<SortingGroup>();
        group.sortingLayerID = -1183170747;
        group.sortingOrder = 1;
        var effect = root.AddComponent<ExperiencePickupEffect>();
        var data = new SerializedObject(effect);
        foreach (string field in new[] { "flash", "left", "right" })
            data.FindProperty(field).objectReferenceValue = NewRenderer(field, root.transform, pixel, material);
        data.ApplyModifiedPropertiesWithoutUndo();
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<ExperiencePickupEffect>();
    }
}
