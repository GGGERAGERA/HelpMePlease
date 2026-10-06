#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// One-shot authoring tool. Existing assets are only rebuilt by an explicit menu/request.
[InitializeOnLoad]
public static class CorridorKitAuthoring
{
    public const string Folder = "Assets/_Project/prefabs/Environment/WorldEvents/Corridor";
    private const string Request = "Artifacts/GeneratedQA/CorridorV2/build-kit.request";
    private static Sprite pixel, front, pylon, emitter, field, rail;
    static CorridorKitAuthoring() => EditorApplication.update += CheckRequest;
    private static void CheckRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        File.Delete(Request);
        try { Build(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    [MenuItem("Tools/World Systems Lab/Author Corridor V2 Kit")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        // Static pixel art is authored in Art/*.svg and exported to PNG, never sampled/generated here.
        pixel = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/art/Orbital/Pixel.png").OfType<Sprite>().First();
        front = ImportAuthoredSprite("CollapseFront");
        pylon = ImportAuthoredSprite("GatePylon");
        emitter = ImportAuthoredSprite("GateEmitter");
        field = ImportAuthoredSprite("GateField");
        rail = ImportAuthoredSprite("WallRail");
        var kit = AssetDatabase.LoadAssetAtPath<CorridorKit>("Assets/_Project/Data/WorldEvents/Corridor/CorridorKit.asset");
        if (kit == null) { kit = ScriptableObject.CreateInstance<CorridorKit>(); AssetDatabase.CreateAsset(kit, "Assets/_Project/Data/WorldEvents/Corridor/CorridorKit.asset"); }
        kit.straight = Save(Straight(), "PF_CorridorSegment_Straight");
        kit.corner = Save(Corner(), "PF_CorridorSegment_Corner");
        var cap = Root("End cap"); Wall(cap.transform, "Wall", Vector2.zero, new Vector2(.32f, 8));
        kit.cap = Save(cap, "PF_CorridorSegment_Cap");
        kit.gate = Save(Node(false), "PF_CorridorGate");
        kit.exit = Save(Node(true), "PF_CorridorExit");
        kit.collapse = Save(Collapse(), "PF_CorridorCollapseFront");
        var reclaimed = Root("Reclaimed trail"); Sprite(reclaimed.transform, "Reclaimed floor", Vector2.zero, new Vector2(1, 8), new Color(.92f, .045f, .2f, .27f), -3);
        kit.reclaimed = Save(reclaimed, "PF_CorridorReclaimed");
        kit.hud = Save(Hud(), "PF_CorridorHUD");
        EditorUtility.SetDirty(kit);
        AssetDatabase.SaveAssets();
        Debug.Log("Corridor V2 authored kit ready: " + AssetDatabase.AssetPathToGUID("Assets/_Project/Data/WorldEvents/Corridor/CorridorKit.asset"));
    }

    private static GameObject Root(string name) => new GameObject(name);
    private static GameObject Child(Transform parent, string name)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go;
    }
    private static GameObject Save(GameObject root, string name)
    {
        root.name = name;
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(root); return prefab;
    }
    private static Sprite ImportAuthoredSprite(string name)
    {
        string path = "Assets/_Project/art/WorldEvents/Corridor/" + name + ".png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static SpriteRenderer Sprite(Transform parent, string name, Vector2 position, Vector2 size, Color color, int order = 10, Sprite source = null)
    {
        var go = Child(parent, name); go.transform.localPosition = position;
        var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = source != null ? source : pixel;
        var bounds = renderer.sprite.bounds.size;
        go.transform.localScale = new Vector3(size.x / bounds.x, size.y / bounds.y, 1);
        renderer.color = color; renderer.sortingLayerName = "Midground"; renderer.sortingOrder = order;
        return renderer;
    }
    private static void Wall(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var go = Child(parent, name); go.transform.localPosition = position;
        var collider = go.AddComponent<BoxCollider2D>(); collider.size = size;
        var renderer = Sprite(go.transform, "Armored energy rail", Vector2.zero, Vector2.one, Color.white, 8, rail);
        renderer.transform.localScale = Vector3.one;
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.size = size.x > size.y ? size : new Vector2(size.y, size.x);
        if (size.y > size.x) renderer.transform.localRotation = Quaternion.Euler(0, 0, 90);
    }
    private static GameObject Straight()
    {
        var root = Root("Straight");
        Wall(root.transform, "Lower wall", new Vector2(0, -4), new Vector2(1, .32f));
        Wall(root.transform, "Upper wall", new Vector2(0, 4), new Vector2(1, .32f));
        Child(root.transform, "Center anchor"); return root;
    }
    private static GameObject Corner()
    {
        var root = Root("Corner RIGHT to UP");
        Wall(root.transform, "Bottom outer wall", new Vector2(0, -4), new Vector2(8.32f, .32f));
        Wall(root.transform, "Right outer wall", new Vector2(4, 0), new Vector2(.32f, 8.32f));
        var incoming = Child(root.transform, "Incoming anchor"); incoming.transform.localPosition = new Vector3(-4, 0, 0);
        var outgoing = Child(root.transform, "Outgoing anchor"); outgoing.transform.localPosition = new Vector3(0, 4, 0);
        return root;
    }
    private static GameObject Node(bool exit)
    {
        var root = Root(exit ? "Exit" : "Checkpoint");
        var gameplay = Child(root.transform, "Gameplay");
        var crossing = Child(gameplay.transform, "Trigger");
        // The volume documents the same physical plane/opening sampled by CorridorRoute.
        // Route order and forward-crossing validation remain authoritative, without a second trigger event.
        var trigger = crossing.AddComponent<BoxCollider2D>(); trigger.isTrigger = true; trigger.size = new Vector2(.5f, 5.2f);
        Child(gameplay.transform, "Crossing plane");
        var direction = Child(gameplay.transform, "Forward +X"); direction.transform.localPosition = Vector3.right;
        var visual = Child(root.transform, "Presentation");
        var left = Sprite(visual.transform, "LeftPylon", new Vector2(0, 3.3f), new Vector2(2.4f, 1.4f), Color.white, 11, pylon);
        left.flipY = true;
        Sprite(visual.transform, "RightPylon", new Vector2(0, -3.3f), new Vector2(2.4f, 1.4f), Color.white, 11, pylon);
        var energy = Child(visual.transform, "EnergyField");
        var colors = new[] { exit ? new Color(1f, .25f, .3f) : new Color(.35f, .48f, .56f),
            exit ? new Color(1f, .72f, .22f) : new Color(.35f, .94f, 1f), new Color(.4f, .82f, .52f) };
        var states = new GameObject[3];
        for (int state = 0; state < 3; state++)
        {
            var group = Child(energy.transform, state == 0 ? (exit ? "LockedFX" : "InactiveFX") : state == 1 ? (exit ? "FinalPushFX" : "ActiveFX") : (exit ? "OpenFX" : "CompletedFX"));
            states[state] = group;
            for (int side = -1; side <= 1; side += 2)
            {
                var core = Sprite(group.transform, side > 0 ? "Left induction fork" : "Right induction fork",
                    new Vector2(0, side * 3.3f), new Vector2(2.4f, 1.4f), colors[state], 12, emitter);
                core.flipY = side > 0;
            }
            if (state > 0 || exit)
            {
                var color = colors[state]; color.a = state == 2 && !exit ? .16f : state == 0 ? .45f : 1f;
                Sprite(group.transform, "Energy membrane + forward flow", Vector2.zero, new Vector2(.85f, 5.2f), color, 10, field);
            }
            group.SetActive(state == 0);
        }
        var view = root.AddComponent<CorridorNodeView>();
        Bind(view, "inactive", states[0]); Bind(view, "active", states[1]); Bind(view, "completed", states[2]); Bind(view, "pulseVisual", energy.transform);
        return root;
    }
    private static GameObject Collapse()
    {
        var root = Root("Collapse"); var visual = Child(root.transform, "Collapse graphics");
        Sprite(visual.transform, "Ruptured energy membrane", Vector2.zero, new Vector2(.75f, 8), new Color(1f, .22f, .42f, .95f), 20, front);
        var view = root.AddComponent<CorridorNodeView>(); Bind(view, "pulseVisual", visual.transform);
        return root;
    }
    private static void Bind(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static GameObject Hud()
    {
        var root = new GameObject("Corridor HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var panel = new GameObject("Compact top-right panel", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(root.transform, false);
        var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one; rect.anchoredPosition = new Vector2(-28, -100); rect.sizeDelta = new Vector2(300, 78);
        panel.GetComponent<Image>().color = new Color(.025f, .075f, .105f, .88f); panel.GetComponent<Image>().raycastTarget = false;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Subject42 UI SDF.asset");
        TMP_Text title = HudText(panel.transform, "Title", "CORRIDOR", new Vector2(16, -11), 20, new Color(.36f, .94f, 1f), font);
        TMP_Text progress = HudText(panel.transform, "Progress", "CHECKPOINT 0 / 3", new Vector2(16, -43), 15, new Color(.8f, .9f, .96f), font);
        var view = root.AddComponent<CorridorHudView>(); Bind(view, "title", title); Bind(view, "progress", progress); Bind(view, "canvas", canvas);
        return root;
    }
    private static TMP_Text HudText(Transform parent, string name, string text, Vector2 position, float size, Color color, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = new Vector2(270, 27);
        var label = go.GetComponent<TextMeshProUGUI>(); label.font = font; label.text = text; label.fontSize = size; label.color = color; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }
}
#endif
