#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Editor-only authoring. Runtime instantiates the serialized production prefab unchanged.
[InitializeOnLoad]
public static class OrbitalRelayPresentationAuthoring
{
    private const string Art = "Assets/_Project/art/FX/OrbitalRelay/Presentation/";
    private static readonly Color Cyan = new(.08f, .82f, .86f, 1);
    private static readonly Color Blue = new(.239216f, .552941f, 1, 1);
    private static readonly Color Background = new(.018f, .031f, .043f, .96f);
    static OrbitalRelayPresentationAuthoring()
    {
        EditorApplication.update += () =>
        {
            const string request = "Artifacts/OrbitalRelay/Presentation/author.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { UpdateProductionPrefab(); File.WriteAllText("Artifacts/OrbitalRelay/Presentation/author-result.txt", "SUCCESS"); }
            catch (Exception e) { File.WriteAllText("Artifacts/OrbitalRelay/Presentation/author-result.txt", e.ToString()); Debug.LogException(e); }
        };
    }
    [MenuItem("Tools/Subject42/Dev/Orbital Relay/Author Pixel Presentation")]
    public static void UpdateProductionPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(OrbitalRelayAuthoring.PrefabPath);
        try { Apply(root); PrefabUtility.SaveAsPrefabAsset(root, OrbitalRelayAuthoring.PrefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets(); OrbitalRelayAuthoring.ValidateProductionAssets();
    }
    private static void Set(UnityEngine.Object target, string field, object value) => OrbitalRelayAuthoring.Set(target, field, value);
    private static GameObject Child(Transform parent, string name, bool ui = false)
    {
        var child = ui ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
        child.transform.SetParent(parent, false); return child;
    }
    private static void Remove(Transform root, string name)
    {
        var child = root.Find(name); if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
    }
    private static Sprite Texture(string name, int width, int height, Func<int, int, bool> pixel, float ppu)
    {
        Directory.CreateDirectory(Art);
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var colors = new Color32[width * height];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            colors[y * width + x] = pixel(x, y) ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
        texture.SetPixels32(colors); texture.Apply();
        string path = Art + name + ".png"; File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static Sprite Ring(string name, int pixels, float thickness, int segments)
    {
        float center = (pixels - 1) * .5f, radius = pixels * .5f - .5f;
        return Texture(name, pixels, pixels, (x, y) =>
        {
            float dx = x - center, dy = y - center, r = Mathf.Sqrt(dx * dx + dy * dy);
            float angle = (Mathf.Atan2(dy, dx) + Mathf.PI * 2) / (Mathf.PI * 2);
            return r <= radius && r >= radius - thickness && angle * segments % 1 < .7f;
        }, pixels * .5f);
    }
    private static bool PanelPixel(int x, int y, int inset = 0)
    {
        int edge = Math.Min(y, 127 - y);
        int cut = edge < 4 ? 8 : edge < 8 ? 4 : 0;
        return y >= inset && y < 128 - inset && x >= cut + inset && x < 170 - cut - inset;
    }
    private static SpriteRenderer WorldSprite(Transform parent, string name, Sprite sprite, int order)
    {
        var renderer = Child(parent, name).AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
        renderer.sortingLayerName = "Midground"; renderer.sortingOrder = order;
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/art/FX/OrbitalRelay/RelayGlow.mat");
        return renderer;
    }
    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)Child(parent, name, true).transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    private static Image Image(Transform parent, string name, Vector2 position, Vector2 size, Color tint, Sprite sprite = null)
    {
        var image = Rect(parent, name, position, size).gameObject.AddComponent<Image>();
        image.sprite = sprite; image.color = tint; image.raycastTarget = false; return image;
    }
    private static TextMeshProUGUI Text(Transform parent, string name, Vector2 position, Vector2 size, TMP_FontAsset font, float fontSize, bool right = false)
    {
        var text = Rect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = fontSize; text.raycastTarget = false;
        text.alignment = right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        text.color = right ? new Color(.88f, .96f, 1, 1) : new Color(.45f, .65f, .7f, 1);
        text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
    private static GameObject Row(Transform panel, string name, float y, TMP_FontAsset font, float labelSize, float valueSize,
        out TextMeshProUGUI caption, out TextMeshProUGUI value)
    {
        var row = Rect(panel, name, new Vector2(24, y), new Vector2(292, 40));
        caption = Text(row, "Caption", Vector2.zero, new Vector2(202, 40), font, labelSize);
        value = Text(row, "Value", new Vector2(200, 0), new Vector2(92, 40), font, valueSize, true);
        return row.gameObject;
    }
    public static void Apply(GameObject root)
    {
        var relay = root.GetComponent<OrbitalRelayEvent>(); var view = root.GetComponent<OrbitalRelayPresentation>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Subject42 UI SDF.asset");
        var arenaSprite = Ring("ArenaDashed", 512, 3, 64);
        var spawnSprite = Ring("SpawnDashed", 512, 2, 48);
        var pulseSprite = Ring("NodeSpawnPulse", 32, 2, 8);
        var panelFill = Texture("HudPlate", 170, 128, (x, y) => PanelPixel(x, y), 1);
        var panelFrame = Texture("HudFrame", 170, 128, (x, y) => PanelPixel(x, y) && !PanelPixel(x, y, 2), 1);
        Remove(root.transform, "ArenaVisual"); Remove(root.transform, "PresentationCanvas"); Remove(root.transform, "TransitionFX");
        var oldAnimator = root.GetComponent<Animator>(); if (oldAnimator != null) UnityEngine.Object.DestroyImmediate(oldAnimator);
        var arenaRoot = Child(root.transform, "ArenaVisual"); var arena = arenaRoot.AddComponent<OrbitalRelayArenaVisual>();
        var border = WorldSprite(arenaRoot.transform, "ArenaBorder", arenaSprite, 2);
        var inner = WorldSprite(arenaRoot.transform, "SpawnInner", spawnSprite, 1);
        var outer = WorldSprite(arenaRoot.transform, "SpawnOuter", spawnSprite, 1);
        Set(arena, "source", relay); Set(arena, "arenaBorder", border); Set(arena, "spawnInner", inner); Set(arena, "spawnOuter", outer);
        arena.Show(true); arena.Show(false);
        var nodes = root.GetComponentsInChildren<OrbitalRelayNode>(true);
        var feedback = new OrbitalRelaySpawnFeedback[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
        {
            Remove(nodes[i].transform, "SpawnFeedback");
            var fx = Child(nodes[i].transform, "SpawnFeedback"); feedback[i] = fx.AddComponent<OrbitalRelaySpawnFeedback>();
            var marker = WorldSprite(fx.transform, "Marker", pulseSprite, 10); marker.enabled = false;
            Set(feedback[i], "marker", marker);
        }
        var canvasRoot = Child(root.transform, "PresentationCanvas", true);
        var canvas = canvasRoot.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90; canvas.pixelPerfect = true;
        var scaler = canvasRoot.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        // Leave the authored production tactical map visible above the event HUD.
        var panel = Rect(canvasRoot.transform, "RelayPanel", new Vector2(-28, -268), new Vector2(340, 256));
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1);
        var hud = panel.gameObject.AddComponent<OrbitalRelayHud>();
        Image(panel, "ColdPlate", Vector2.zero, panel.sizeDelta, Background, panelFill);
        var frame = Image(panel, "PixelFrame", Vector2.zero, panel.sizeDelta, Blue, panelFrame);
        var phaseStrip = Image(panel, "PhaseAccent", new Vector2(24, -50), new Vector2(292, 2), Blue);
        var phase = Text(panel, "Phase", new Vector2(24, -12), new Vector2(292, 36), font, 21); phase.text = "STABILIZE";
        Row(panel, "Time", -56, font, 18, 34, out var timeCaption, out var timeValue);
        Row(panel, "Activations", -112, font, 18, 24, out var activationCaption, out var activationValue);
        var goldRow = Row(panel, "Gold", -156, font, 18, 24, out var goldCaption, out var goldValue);
        var comboRow = Row(panel, "Combo", -200, font, 16, 20, out var comboCaption, out var comboValue);
        var popup = Text(panel, "GoldGain", new Vector2(180, -165), new Vector2(50, 24), font, 16, true); popup.gameObject.SetActive(false);
        var wipe = Image(panel, "TransitionWipe", new Vector2(24, -12), new Vector2(292, 36), Color.clear); wipe.enabled = false;
        Set(hud, "phase", phase); Set(hud, "timeCaption", timeCaption); Set(hud, "timeValue", timeValue);
        Set(hud, "activationsCaption", activationCaption); Set(hud, "activationsValue", activationValue);
        Set(hud, "goldCaption", goldCaption); Set(hud, "goldValue", goldValue); Set(hud, "comboCaption", comboCaption); Set(hud, "comboValue", comboValue);
        Set(hud, "goldPopup", popup); Set(hud, "goldRow", goldRow); Set(hud, "comboRow", comboRow);
        Set(hud, "border", frame); Set(hud, "phaseStrip", phaseStrip); Set(hud, "transitionWipe", wipe);
        Set(view, "presentationCanvas", canvasRoot); Set(view, "hud", hud); Set(view, "arenaVisual", arena); Set(view, "spawnFeedback", feedback);
        view.Clear();
        root.transform.Find("StartZoneVisual").gameObject.SetActive(true);
    }
}
#endif
