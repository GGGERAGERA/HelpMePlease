#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Explicit prefab authoring, following the other WorldSystemsLab event authoring commands.
[InitializeOnLoad]
public static class RocketStormAuthoring
{
    private const string Root = "Assets/_Project/";
    public const string PrefabPath = Root + "prefabs/Environment/WorldEvents/RocketStormEvent.prefab";
    private const string Request = "Artifacts/GeneratedQA/RocketStormIntegration/author.request";
    static RocketStormAuthoring() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Build(); File.WriteAllText(Request + ".result", "PASS: Rocket Storm authored and registered"); }
        catch (Exception e) { File.WriteAllText(Request + ".result", e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Tools/World Events/Author Rocket Storm")]
    public static void Build()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Orbital/Visual.mat");
        var square = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath("842791f4876552648b2118a0575f7a6d"));
        var beacon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "art/WorldEvents/Corridor/CorridorBeacon.png");
        var rail = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "art/WorldEvents/Corridor/WallRail.png");
        var existing = AssetDatabase.LoadAssetAtPath<RocketStormEvent>(PrefabPath);
        var tuning = existing != null ? (RocketStormEvent.Settings)typeof(RocketStormEvent)
            .GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(existing) : new RocketStormEvent.Settings();
        var root = new GameObject("RocketStormEvent");
        try
        {
            var game = root.AddComponent<RocketStormEvent>();
            Set(game, "settings", tuning.Snapshot());
            var start = root.GetComponent<CircleCollider2D>(); start.radius = 2.5f; start.isTrigger = true;
            Set(game, "startArea", start);
            Set(game, "eventId", "rocket-storm"); Set(game, "eventTag", "survival");
            Set(game, "eventDisplayName", "Ракетный шторм"); Set(game, "eventDescription", "Переживите обстрел. Оставайтесь внутри отмеченной области.");
            Set(game, "allowedInSite", true); Set(game, "availableInProduction", true); Set(game, "promptText", "");
            Set(game, "rocketPrefab", Load<GameObject>("703da712b72d21341ae48974468d51ca"));
            Set(game, "markerPrefab", Load<GameObject>("c941bc18f7e086648bae2a23e2a87208"));
            Set(game, "explosionPrefab", Load<GameObject>("f91913de896646447a357936450e42c5").GetComponent<ParticleSystem>());

            var arena = Child(root.transform, "Survival area"); Set(game, "arenaVisual", arena.transform);
            var floor = Sprite(arena.transform, "Subtle floor", square, material, 0); Set(game, "floor", floor);
            var borders = new SpriteRenderer[4]; var walls = new BoxCollider2D[4];
            for (int i = 0; i < 4; i++)
            {
                borders[i] = Sprite(arena.transform, "Boundary " + i, rail, material, 8);
                borders[i].drawMode = SpriteDrawMode.Tiled;
                walls[i] = borders[i].gameObject.AddComponent<BoxCollider2D>();
                walls[i].enabled = false;
                walls[i].autoTiling = false;
            }
            Set(game, "borders", borders); Set(game, "walls", walls);

            var launch = Child(root.transform, "Small activation area"); Set(game, "launchArea", launch);
            Sprite(launch.transform, "Activation circle", AssetDatabase.LoadAssetAtPath<Sprite>(Root + "art/WorldEvents/Corridor/CorridorStartArea.png"), material, 3);
            Sprite(root.transform, "Launch beacon", beacon, material, 6).transform.localScale = Vector3.one * 1.1f;
            var arrow = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "prefabs/Bunker/PF_InteractionArrow.prefab"), root.transform);
            arrow.name = "Interaction arrow"; arrow.transform.localPosition = new Vector3(0, 2.2f); arrow.transform.localScale = Vector3.one * .6f;
            Set(game, "interactionArrow", arrow);
            var text = Child(root.transform, "Start hint and objective").AddComponent<TextMeshPro>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "Fonts/Subject42 UI SDF.asset");
            text.fontSize = 3.2f; text.color = new Color(.95f, .9f, .65f); text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.rectTransform.sizeDelta = new Vector2(12, 1.5f);
            text.transform.localPosition = new Vector3(0, -3.2f);
            text.GetComponent<MeshRenderer>().sortingLayerName = "Midground"; text.GetComponent<MeshRenderer>().sortingOrder = 20;
            Set(game, "prompt", text);
            if (!game.TryValidateConfiguration(out var error)) throw new InvalidOperationException(error);
            game.TryPreparePlacement(new WorldEventPlacementContext(Vector2.zero, new Rect(-50,-50,100,100), null, 0, null), out _);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }

        string gameplayPath = Root + "prefabs/Bootstrap/GameplayWorld.prefab";
        var gameplay = PrefabUtility.LoadPrefabContents(gameplayPath);
        try
        {
            var spawner = gameplay.GetComponentInChildren<WorldEventSpawner>(true);
            var data = new SerializedObject(spawner); var array = data.FindProperty("eventPrefabs");
            var prefab = AssetDatabase.LoadAssetAtPath<RocketStormEvent>(PrefabPath);
            bool registered = Enumerable.Range(0, array.arraySize).Any(i => array.GetArrayElementAtIndex(i).objectReferenceValue == prefab);
            if (!registered) { array.arraySize++; array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = prefab; data.ApplyModifiedPropertiesWithoutUndo(); }
            PrefabUtility.SaveAsPrefabAsset(gameplay, gameplayPath);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab, out string guid, out long id);
            // Update only the new reference; preserve the laboratory scene and its authored tuning.
            string scene = File.ReadAllText(WorldSystemsLabController.ScenePath);
            if (!scene.Contains("  rocketStormPrefab:"))
            {
                int at = scene.IndexOf("  corridorPrefab:", StringComparison.Ordinal);
                int end = scene.IndexOf('\n', at);
                scene = scene.Insert(end + 1, $"  rocketStormPrefab: {{fileID: {id}, guid: {guid}, type: 3}}\n");
                File.WriteAllText(WorldSystemsLabController.ScenePath, scene);
            }
            foreach (var lab in Resources.FindObjectsOfTypeAll<WorldSystemsLabController>().Where(x => x.gameObject.scene.IsValid()))
                Set(lab, "rocketStormPrefab", prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(gameplay); }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
    }
    private static T Load<T>(string guid) where T : Object => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
    private static GameObject Child(Transform parent, string name)
    { var item = new GameObject(name); item.transform.SetParent(parent, false); return item; }
    private static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, Material material, int order)
    {
        var item = Child(parent, name).AddComponent<SpriteRenderer>(); item.sprite = sprite; item.sharedMaterial = material;
        item.sortingLayerName = "Midground"; item.sortingOrder = order; return item;
    }
    private static void Set(Object target, string name, object value)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field == null) continue;
            field.SetValue(target, value); return;
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }
}
#endif

