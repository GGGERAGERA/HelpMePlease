#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit, repeatable authoring. No runtime scene patching.</summary>
[InitializeOnLoad]
public static class BunkerBarkAuthoring
{
    private const string Output = "Artifacts/GeneratedQA/BunkerBarks";
    static BunkerBarkAuthoring()
    {
        EditorApplication.update += () =>
        {
            string request = Output + "/author.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool("Subject42.Verification.AnyRunActive", false)) return;
            File.Delete(request);
            try { Author(); }
            catch (Exception error) { File.WriteAllText(Output + "/author-error.txt", error.ToString()); Debug.LogException(error); }
        };
    }

    [MenuItem("Tools/Subject42/Author Bunker Ambient Barks")]
    public static void Author()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Save scene changes before authoring bunker barks.");
        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
        var components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).ToArray();
        var existing = components.OfType<BunkerAmbientBarks>().FirstOrDefault();
        if (existing != null)
        {
            AuthorZones(scene, existing);
            EditorSceneManager.SaveScene(scene);
            return; // Keep all Inspector edits; only repair this tool's trigger bounds.
        }
        var loadout = components.OfType<BunkerPlayerLoadoutController>().Single();
        var player = (Transform)new SerializedObject(loadout).FindProperty("controlledPlayerRoot").objectReferenceValue;
        var system = new GameObject("Bunker Ambient Barks");
        system.transform.SetParent(scene.GetRootGameObjects().Single(r => r.name == "SYSTEM").transform, false);
        var barks = system.AddComponent<BunkerAmbientBarks>();

        var canvasObject = new GameObject("Bark Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(system.transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        canvas.pixelPerfect = true;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = .5f;
        var bubble = Rect("Pixel Bubble", canvasObject.transform, new Vector2(268, 62));
        bubble.pivot = new Vector2(.5f, 0f);
        var group = bubble.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0;
        group.blocksRaycasts = group.interactable = false;
        Color border = new(.42f, .46f, .40f, 1f);
        Color fill = new(.075f, .105f, .11f, .96f);
        ImageRect("Border", bubble, new Vector2(268, 62), Vector2.zero, border);
        ImageRect("Inset", bubble, new Vector2(264, 58), Vector2.zero, fill);
        ImageRect("Tail Border", bubble, new Vector2(10, 8), new Vector2(0, -33), border);
        ImageRect("Tail", bubble, new Vector2(6, 6), new Vector2(0, -31), fill);
        ImageRect("Tail Tip", bubble, new Vector2(4, 4), new Vector2(0, -38), border);
        var textRect = Rect("Text", bubble, new Vector2(244, 48));
        var label = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Subject42 UI SDF.asset");
        label.fontSize = 15f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(.88f, .9f, .82f);
        label.raycastTarget = false;
        label.richText = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        var view = canvasObject.AddComponent<BunkerBarkView>();
        Set(view, "canvas", canvas); Set(view, "group", group); Set(view, "bubble", bubble); Set(view, "label", label);
        Set(view, "worldCamera", components.OfType<Camera>().Single(c => c.CompareTag("MainCamera")));
        Set(barks, "player", player); Set(barks, "view", view);
        Set(barks, "panels", components.OfType<BunkerPanelManager>().Single());
        Set(barks, "intro", components.OfType<BunkerIntroController>().FirstOrDefault());
        var data = new SerializedObject(barks);
        var games = data.FindProperty("minigames");
        var minigames = components.OfType<BunkerMinigame>().ToArray();
        games.arraySize = minigames.Length;
        for (int i = 0; i < games.arraySize; i++) games.GetArrayElementAtIndex(i).objectReferenceValue = minigames[i];
        var blockers = components.OfType<BunkerEventManager>().Select(c => Read(c, "fullscreenRoot"))
            .Concat(components.OfType<BunkerRunSummaryPresenter>().Select(c => Read(c, "notification") is RectTransform rect ? rect.gameObject : null))
            .Where(c => c != null).ToArray();
        var roots = data.FindProperty("blockingInterfaces"); roots.arraySize = blockers.Length;
        for (int i = 0; i < roots.arraySize; i++) roots.GetArrayElementAtIndex(i).objectReferenceValue = blockers[i];
        var catalogue = new[]
        {
            Line("Здесь хотя бы потолок на месте."),
            Line("Вентиляция гудит. Значит, живём."),
            Line("Тишина тоже бывает хорошим звуком."),
            Line("Надо бы запомнить этот покой."),
            Line("Пара минут без сирены. Уже неплохо."),
            Line("У этих автоматов свои аномалии.", BunkerBarkCategory.Room, "arcade"),
            Line("Здесь проигрывать немного спокойнее.", BunkerBarkCategory.Room, "arcade"),
            Line("Диван всё ещё на моей стороне.", BunkerBarkCategory.Room, "chill"),
            Line("Можно просто посидеть. Без причины.", BunkerBarkCategory.Room, "chill"),
            Line("За стеклом они куда вежливее.", BunkerBarkCategory.Room, "gallery"),
            Line("Лучше изучать их отсюда.", BunkerBarkCategory.Room, "gallery"),
            Line("У каждой царапины здесь своя история.", BunkerBarkCategory.Room, "trophies"),
            Line("Есть что вспомнить. И куда вернуться.", BunkerBarkCategory.Room, "trophies"),
            Line("Дома. Можно наконец выдохнуть.", BunkerBarkCategory.Event, "return_from_run"),
            Line("Ладно. Посмотрим, что дальше.", BunkerBarkCategory.Event, "story_reflection")
        };
        var lines = data.FindProperty("lines"); lines.arraySize = catalogue.Length;
        for (int i = 0; i < catalogue.Length; i++)
        {
            var item = lines.GetArrayElementAtIndex(i); var line = catalogue[i];
            item.FindPropertyRelative("text").stringValue = line.text;
            item.FindPropertyRelative("category").enumValueIndex = (int)line.category;
            item.FindPropertyRelative("key").stringValue = line.key;
            item.FindPropertyRelative("probability").floatValue = line.probability;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        AuthorZones(scene, barks);
        EditorSceneManager.SaveScene(scene);
    }

    private static void AuthorZones(UnityEngine.SceneManagement.Scene scene, BunkerAmbientBarks barks)
    {
        var report = new System.Text.StringBuilder();
        foreach (var room in new[] { ("ARCADE", "arcade"), ("CHILL_ROOM", "chill"), ("ENEMY_GALLERY", "gallery"), ("TROPHIES", "trophies") })
        {
            var root = scene.GetRootGameObjects().Single(r => r.name == room.Item1);
            var floor = root.GetComponentsInChildren<Tilemap>(true).Single(t => t.name.EndsWith(" - Floor", StringComparison.Ordinal));
            // cellBounds can retain erased tiles. Use only occupied floor cells.
            Vector3Int cellMin = new(int.MaxValue, int.MaxValue, 0), cellMax = new(int.MinValue, int.MinValue, 1);
            foreach (var cell in floor.cellBounds.allPositionsWithin)
                if (floor.HasTile(cell)) { cellMin = Vector3Int.Min(cellMin, cell); cellMax = Vector3Int.Max(cellMax, cell + Vector3Int.one); }
            if (cellMin.x == int.MaxValue) throw new InvalidOperationException("Room floor has no tiles: " + room.Item1);
            Vector3 min = floor.CellToWorld(cellMin), max = floor.CellToWorld(cellMax);
            var previous = root.transform.Find("Ambient Entry - " + room.Item2);
            var zoneObject = previous != null ? previous.gameObject : new GameObject("Ambient Entry - " + room.Item2);
            zoneObject.transform.SetParent(root.transform, false);
            zoneObject.transform.position = (min + max) * .5f;
            var zone = zoneObject.GetComponent<BoxCollider2D>() ?? zoneObject.AddComponent<BoxCollider2D>(); zone.isTrigger = true;
            zone.size = new Vector2(Mathf.Max(1f, max.x - min.x - 1f), Mathf.Max(1f, max.y - min.y - 1f));
            var trigger = zoneObject.GetComponent<BunkerBarkRoomTrigger>() ?? zoneObject.AddComponent<BunkerBarkRoomTrigger>();
            Set(trigger, "barks", barks);
            var triggerData = new SerializedObject(trigger); triggerData.FindProperty("roomId").stringValue = room.Item2; triggerData.ApplyModifiedPropertiesWithoutUndo();
            report.AppendLine($"{room.Item1}: centre={zoneObject.transform.position}, size={zone.size}, floor={floor.name}");
        }
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/authored.txt", report.ToString());
    }

    private static BunkerBarkLine Line(string text, BunkerBarkCategory category = BunkerBarkCategory.Random, string key = "") =>
        new() { text = text, category = category, key = key, probability = 1f };
    private static Object Read(Object owner, string property) => new SerializedObject(owner).FindProperty(property).objectReferenceValue;
    private static void Set(Object owner, string property, Object value)
    {
        var data = new SerializedObject(owner); data.FindProperty(property).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = size;
        return rect;
    }
    private static void ImageRect(string name, Transform parent, Vector2 size, Vector2 position, Color colour)
    {
        var rect = Rect(name, parent, size); rect.anchoredPosition = position;
        var image = rect.gameObject.AddComponent<Image>(); image.color = colour; image.raycastTarget = false;
    }
}
#endif
