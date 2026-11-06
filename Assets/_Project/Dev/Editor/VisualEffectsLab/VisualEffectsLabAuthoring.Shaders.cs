#if UNITY_EDITOR
using System;
using System.Linq;
using Subject42.Dev.VisualEffects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public static partial class VisualEffectsLabAuthoring
{
    private static void BuildShaderScene()
    {
        CreateStage("SUBJECT #42 / SHADER EXPERIMENTS", 8.5f);
        EnsureFolder(EffectLabCatalog.Folder + "/Materials");
        var spriteShader = RequiredShader("Subject42/Dev/SpriteExperiments");
        var fieldShader = RequiredShader("Subject42/Dev/FieldExperiments");
        var environmentShader = RequiredShader("Subject42/Dev/EnvironmentExperiments");
        var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/art/Sprites/Enemy1.png").OfType<Sprite>().First();
        const string floorPath = "Assets/_Project/art/Sprites/newLowBunker/BunkerLowTilemap/RoomFloors/EnemyGallery_Floor_18.asset";
        var floor = AssetDatabase.LoadAssetAtPath<Tile>(floorPath);
        if (floor == null || floor.sprite == null) throw new InvalidOperationException("Missing authored floor Tile: " + floorPath);
        var pageData = new System.Collections.Generic.List<ShaderLabController.Page>();

        var spritePage = new GameObject("Sprite and field experiments");
        string[] names = { "ORIGINAL", "OUTLINE", "DISSOLVE", "HOLOGRAM", "FROST", "OVERHEAT", "GLITCH", "GHOST", "SHIELD PULSE", "SHOCKWAVE", "VORTEX", "ELECTRIC GRID" };
        string[] notes = { "Original project sprite", "Readable silhouette", "Spawn / death experiment", "Portal / scan experiment", "Status visual experiment", "Charge / danger experiment", "Interference experiment", "Phase / stealth experiment", "Defence experiment", "Impact experiment", "Gravity experiment", "Area / linkage experiment" };
        var basic = new Renderer[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            bool field = i >= 8;
            Vector3 position = new(-8.4f + i % 4 * 5.6f, 4.1f - i / 4 * 4.1f, 0);
            var material = LabMaterial(names[i], field ? fieldShader : spriteShader, field ? i - 8 : i);
            if (!field) ConfigureSprite(material, sprite, .1f);
            SaveLabMaterial(material);
            basic[i] = ShaderSample(names[i], notes[i], material, position, Vector2.one * 2.8f, spritePage.transform, 1.6f, 1.65f, 5.5f);
        }
        AddPage("Спрайты / поля", spritePage, basic);

        var environmentPage = new GameObject("Rift and floor experiments");
        var environment = new Renderer[2];
        for (int i = 0; i < environment.Length; i++)
        {
            string name = i == 0 ? "SPATIAL RIFT" : "FLOOR IMPULSE";
            string note = i == 0 ? "Local background warp / jagged energy edge" : "Travelling wave / seams / short floor kick";
            var material = LabMaterial(name, environmentShader, i);
            ConfigureEnvironment(material, floor, sprite); SaveLabMaterial(material);
            environment[i] = ShaderSample(name, note, material, new Vector3(i == 0 ? -5.2f : 5.2f, 0.1f, 0),
                Vector2.one * 9f, environmentPage.transform, 5.4f, 5.05f, 10f);
        }
        Label("PROJECT FLOOR + SPRITE  /  INTENSITY 0 = BASELINE", new Vector3(0, -6.05f, 0), 22, 2.15f,
            new Color(.55f, .65f, .75f)).transform.SetParent(environmentPage.transform, true);
        AddPage("Разлом / импульс", environmentPage, environment);

        var creaturePage = new GameObject("Creature and armour studies");
        string[] creatureNames = { "ADAPTIVE SHELL", "DIAGNOSTIC SCAN", "BIO INFECTION", "NANO ASSEMBLY", "PHASE FOLD", "CHARGED ARMOR" };
        string[] creatureNotes = { "Defence / directional impact", "Scanner / silhouette readout", "Mutation / spreading veins", "Spawn / rebuilding blocks", "Teleport / compressed silhouette", "Armour / moving specular" };
        var creatures = new Renderer[creatureNames.Length];
        for (int i = 0; i < creatures.Length; i++)
        {
            var material = LabMaterial(creatureNames[i], spriteShader, 8 + i);
            ConfigureSprite(material, sprite, .18f); SaveLabMaterial(material);
            creatures[i] = ShaderSample(creatureNames[i], creatureNotes[i], material,
                new Vector3(-7.2f + i % 3 * 7.2f, 3 - i / 3 * 6, 0), Vector2.one * 3.7f,
                creaturePage.transform, 2.2f, 2.25f, 6.8f);
        }
        AddPage("Существа / броня", creaturePage, creatures);

        var zonesPage = new GameObject("Zone and weather studies");
        string[] zoneNames = { "STASIS POCKET", "GRAVITY WELL", "RELAY LINKS", "SUPPRESSION", "BIO FILM", "WET FLOOR" };
        string[] zoneNotes = { "Slow zone / interrupted flow", "Spatial pull / floor deformation", "Nodes / travelling charge", "Jammed zone / broken flow", "Biological anomaly / surface film", "Weather / ripples and reflections" };
        var zones = new Renderer[zoneNames.Length];
        for (int i = 0; i < zones.Length; i++)
        {
            var material = LabMaterial(zoneNames[i], environmentShader, 2 + i);
            ConfigureEnvironment(material, floor, sprite); SaveLabMaterial(material);
            zones[i] = ShaderSample(zoneNames[i], zoneNotes[i], material,
                new Vector3(-7.2f + i % 3 * 7.2f, 3 - i / 3 * 6, 0), Vector2.one * 4.6f,
                zonesPage.transform, 2.65f, 2.7f, 6.8f);
        }
        AddPage("Зоны / окружение", zonesPage, zones);

        var controller = new GameObject("Shader preview controls", typeof(ShaderLabController)).GetComponent<ShaderLabController>();
        using var data = new SerializedObject(controller);
        var pages = data.FindProperty("pages"); pages.arraySize = pageData.Count;
        for (int i = 0; i < pageData.Count; i++)
        {
            var page = pages.GetArrayElementAtIndex(i);
            page.FindPropertyRelative("Title").stringValue = pageData[i].Title;
            page.FindPropertyRelative("Root").objectReferenceValue = pageData[i].Root;
            var samples = page.FindPropertyRelative("Samples"); samples.arraySize = pageData[i].Samples.Length;
            for (int j = 0; j < samples.arraySize; j++)
                samples.GetArrayElementAtIndex(j).objectReferenceValue = pageData[i].Samples[j];
            pageData[i].Root.SetActive(i == 2);
        }
        data.FindProperty("initialPage").intValue = 2;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), EffectLabCatalog.ShaderScenePath);

        void AddPage(string title, GameObject root, Renderer[] samples) =>
            pageData.Add(new ShaderLabController.Page { Title = title + " (" + samples.Length + ")", Root = root, Samples = samples });
    }

    private static void ConfigureSprite(Material material, Sprite sprite, float padding)
    {
        material.SetTexture("_MainTex", sprite.texture); material.SetVector("_SpriteRect", AtlasRect(sprite));
        material.SetFloat("_SpritePadding", padding);
    }

    private static void ConfigureEnvironment(Material material, Tile floor, Sprite actor)
    {
        material.SetTexture("_FloorTex", floor.sprite.texture); material.SetVector("_FloorRect", AtlasRect(floor.sprite));
        material.SetColor("_FloorTint", floor.color);
        material.SetTexture("_ActorTex", actor.texture); material.SetVector("_ActorRect", AtlasRect(actor));
    }

    private static Shader RequiredShader(string name)
    {
        var shader = Shader.Find(name);
        if (shader == null) throw new InvalidOperationException("Import lab shader first: " + name);
        return shader;
    }

    private static Vector4 AtlasRect(Sprite sprite)
    {
        Rect rect = sprite.textureRect;
        return new Vector4(rect.x / sprite.texture.width, rect.y / sprite.texture.height,
            rect.width / sprite.texture.width, rect.height / sprite.texture.height);
    }

    private static Material LabMaterial(string name, Shader shader, int mode)
    {
        string path = EffectLabCatalog.Folder + "/Materials/" + name.Replace(' ', '_') + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader; material.SetFloat("_Mode", mode);
        material.SetFloat("_Intensity", 1); material.SetFloat("_LabTime", 0);
        return material;
    }

    private static void SaveLabMaterial(Material material)
    { EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); }

    private static Renderer ShaderSample(string name, string note, Material material, Vector3 position,
        Vector2 size, Transform page, float headingOffset, float noteOffset, float labelWidth)
    {
        var sample = GameObject.CreatePrimitive(PrimitiveType.Quad); sample.name = name;
        Object.DestroyImmediate(sample.GetComponent<Collider>());
        sample.transform.SetParent(page, false); sample.transform.position = position;
        sample.transform.localScale = new Vector3(size.x, size.y, 1);
        var renderer = sample.GetComponent<Renderer>(); renderer.sharedMaterial = material;
        Label(name, position + Vector3.up * headingOffset, labelWidth, 2.7f,
            new Color(.65f, 1, .93f)).transform.SetParent(page, true);
        Label(note, position + Vector3.down * noteOffset, labelWidth,
            1.8f, new Color(.55f, .65f, .75f)).transform.SetParent(page, true);
        return renderer;
    }
}
#endif
