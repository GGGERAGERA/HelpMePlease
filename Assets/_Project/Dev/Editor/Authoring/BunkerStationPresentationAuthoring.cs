#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using Subject42.Combat.OrbitalStation;
using Object = UnityEngine.Object;
using UnityEngine;

[InitializeOnLoad]
public static class BunkerStationPresentationAuthoring
{
    public const string Output = "Artifacts/GeneratedQA/BunkerStations";
    static BunkerStationPresentationAuthoring() => EditorApplication.update += Poll;
    private static void Poll()
    {
        string path = Output + "/author.request";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(path)) return;
        string stamp = File.GetLastWriteTimeUtc(path).Ticks.ToString();
        if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetString("BunkerStations.AuthorStamp", "") != stamp)
        {
            SessionState.SetString("BunkerStations.AuthorStamp", stamp);
            SessionState.SetFloat("BunkerStations.RefreshAt", (float)EditorApplication.timeSinceStartup);
            AssetDatabase.Refresh();
            return;
        }
        if (!EditorApplication.isPlayingOrWillChangePlaymode &&
            EditorApplication.timeSinceStartup - SessionState.GetFloat("BunkerStations.RefreshAt", 0) < 3) return;
        string command = File.ReadAllText(path).Trim();
        File.Delete(path);
        Directory.CreateDirectory(Output);
        try
        {
            if (command == "stop") { EditorApplication.isPlaying = false; return; }
            if (command == "refresh") return;
            if (command == "audit")
            {
                var config = AssetDatabase.LoadAssetAtPath<OrbitalPresentationConfig>("Assets/_Project/Data/Orbital/OrbitalPresentationConfig.asset");
                File.WriteAllText(Output + "/art-audit.txt", string.Join("\n", Enum.GetValues(typeof(OrbitalModuleKind)).Cast<OrbitalModuleKind>()
                    .Where(k => config.GetPrefab(k) != null).Select(k => k + ":\n" + string.Join("\n", config.GetPrefab(k)
                        .GetComponentsInChildren<SpriteRenderer>(true).Where(s => s.sprite != null)
                        .Select(s => s.name + " => " + s.sprite.name + " " + s.sprite.rect + " " + AssetDatabase.GetAssetPath(s.sprite))))));
                return;
            }
            if (command == "migrate" && !EditorApplication.isPlayingOrWillChangePlaymode) { Migrate(); return; }
            File.WriteAllText(Output + "/state.txt", "Playing=" + EditorApplication.isPlaying + "\n" +
                string.Join("\n", Resources.FindObjectsOfTypeAll<BunkerSelectionWindow>()
                    .Select(w => w.name + " active=" + w.gameObject.activeInHierarchy)));
        }
        catch (Exception ex) { File.WriteAllText(Output + "/author.error", ex.ToString()); Debug.LogException(ex); }
    }

    private const string PrefabPath = "Assets/_Project/prefabs/UI/Stations/CharacterSelectionWindow.prefab";
    [MenuItem("Tools/Subject42/Migrate shared bunker station presentation")]
    public static void Migrate()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var button = root.transform.Find("StationProgressPanel/UpgradeStationButton");
            var lamp = button.Find("PaintedUpgradeLamp");
            if (lamp != null) Object.DestroyImmediate(lamp.gameObject);
            var label = (RectTransform)button.Find("Label");
            label.anchoredPosition = Vector2.zero;
            label.sizeDelta = new Vector2(-24, -8);
            var shell = root.GetComponent<StationUIShell>();
            var info = shell.InfoPanel;
            if (info.Find("ItemDetail") == null) BuildItemDetail(root, info, button.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        PopulateCatalog();
        File.WriteAllText(Output + "/migration.result", "PASS: shared approved prefab updated; original GUID/component references retained.");
    }

    private static void BuildItemDetail(GameObject window, RectTransform info, GameObject buttonTemplate)
    {
        var font = window.GetComponentsInChildren<TextMeshProUGUI>(true).First().font;
        var content = Rect("ItemDetail", info);
        Stretch(content);
        var view = content.gameObject.AddComponent<BunkerStationItemDetail>();
        var icon = Rect("ItemPreview", content).gameObject.AddComponent<Image>();
        Place(icon.rectTransform, 0, 0, 270, 270, new Vector2(.22f, .55f));
        icon.raycastTarget = false;
        var accent = Rect("ItemAccent", content).gameObject.AddComponent<Image>();
        Place(accent.rectTransform, 0, 0, 92, 3, new Vector2(.22f, .2f));
        accent.raycastTarget = false;
        var title = Text("ItemName", content, font, 20);
        Place(title.rectTransform, 0, -44, 0, 50, new Vector2(.5f, 1)); RightColumn(title.rectTransform, 32);
        var description = Text("ItemEffect", content, font, 14);
        Place(description.rectTransform, 0, -104, 0, 56, new Vector2(.5f, 1)); RightColumn(description.rectTransform, 32);
        description.color = StationPixelVisuals.MutedText;
        var stats = Text("ItemStats", content, font, 13);
        stats.lineSpacing = 12f;
        Place(stats.rectTransform, 0, -176, 0, 70, new Vector2(.5f, 1)); RightColumn(stats.rectTransform, 32);
        Set(view, "preview", icon); Set(view, "accent", accent); Set(view, "title", title);
        Set(view, "description", description); Set(view, "stats", stats);

        var progression = Rect("ItemProgression", content);
        progression.anchorMin = new Vector2(.44f, 0); progression.anchorMax = new Vector2(1, 0);
        progression.pivot = new Vector2(.5f, 0); progression.anchoredPosition = new Vector2(0, 12);
        progression.sizeDelta = new Vector2(-32, 172);
        var progressView = progression.gameObject.AddComponent<BunkerProgressionView>();
        var level = Text("Level", progression, font, 13);
        Place(level.rectTransform, 0, -12, 0, 24, new Vector2(.5f, 1)); Width(level.rectTransform, 0);
        var bonus = Text("Comparison", progression, font, 15);
        Place(bonus.rectTransform, 0, -43, 0, 32, new Vector2(.5f, 1)); Width(bonus.rectTransform, 0);
        bonus.color = StationPixelVisuals.Cyan;
        var state = Text("State", progression, font, 12);
        Place(state.rectTransform, 0, -78, 0, 28, new Vector2(.5f, 1)); Width(state.rectTransform, 0);
        var progressText = Text("Investment", progression, font, 11);
        Place(progressText.rectTransform, 0, -103, 0, 22, new Vector2(.5f, 1)); Width(progressText.rectTransform, 0);
        progressText.color = StationPixelVisuals.MutedText;
        var upgrade = Object.Instantiate(buttonTemplate, progression, false);
        upgrade.name = "UpgradeItemButton";
        var layout = upgrade.GetComponent<LayoutElement>(); if (layout != null) layout.ignoreLayout = true;
        var upgradeRect = (RectTransform)upgrade.transform;
        Place(upgradeRect, 0, 0, 0, 44, new Vector2(.5f, 0)); Width(upgradeRect, 20);
        upgrade.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();
        Set(progressView, "levelText", level); Set(progressView, "bonusText", bonus);
        Set(progressView, "stateText", state); Set(progressView, "progressText", progressText);
        Set(progressView, "upgradeButton", upgrade.GetComponent<Button>());
        Set(progressView, "upgradeButtonText", upgrade.GetComponentInChildren<TextMeshProUGUI>(true));
        var serialized = new SerializedObject(progressView);
        serialized.FindProperty("colorStateFeedback").boolValue = true; serialized.ApplyModifiedPropertiesWithoutUndo();
        Set(window.GetComponent<BunkerSelectionWindow>(), "itemProgression", progressView);
        Set(info.GetComponent<BunkerSelectionDetailView>(), "itemDetail", view);
        content.gameObject.SetActive(false);
    }

    private static void PopulateCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<BunkerSelectionCatalog>("Assets/_Project/Data/SurfaceMap/BunkerSelectionCatalog.asset");
        var config = AssetDatabase.LoadAssetAtPath<OrbitalPresentationConfig>("Assets/_Project/Data/Orbital/OrbitalPresentationConfig.asset");
        var kinds = new[] { OrbitalModuleKind.Pistol, OrbitalModuleKind.Sniper, OrbitalModuleKind.Shotgun, OrbitalModuleKind.Saw };
        catalog.modules = kinds.Select(kind =>
        {
            var view = config.GetPrefab(kind).GetComponent<OrbitalModuleView>();
            return new BunkerSelectionCatalog.ModulePresentation
            {
                Kind = kind, NameKey = "station.module." + kind.ToString().ToLowerInvariant(),
                CategoryKey = "bunker.modules", DescriptionKey = "station.effect." + kind.ToString().ToLowerInvariant(),
                Icon = RenderModuleIcon(view, kind),
                IconColor = Color.white,
                AccentColor = kind == OrbitalModuleKind.Sniper ? new Color(.95f,.25f,.2f) :
                    kind == OrbitalModuleKind.Shotgun ? new Color(.25f,.9f,.35f) :
                    kind == OrbitalModuleKind.Saw ? new Color(.65f,.3f,1f) : StationPixelVisuals.Cyan
            };
        }).ToArray();
        foreach (var upgrade in catalog.upgrades)
        {
            string token = upgrade.Type switch { MetaUpgradeType.Hp => "HP", MetaUpgradeType.MoveSpeed => "Run",
                MetaUpgradeType.XpGain => "XP", MetaUpgradeType.GoldGain => "Gold", _ => "Container" };
            var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/art/UI/HudIcons/" + token + ".png").OfType<Sprite>().FirstOrDefault();
            if (sprite != null) upgrade.Icon = upgrade.PreviewIcon = sprite;
        }
        catalog.anomalyIcon = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/art/UI/HudIcons/Anomaly.png").OfType<Sprite>().FirstOrDefault();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
    }

    private static Sprite RenderModuleIcon(OrbitalModuleView view, OrbitalModuleKind kind)
    {
        const string folder = "Assets/_Project/art/UI/StationModules";
        Directory.CreateDirectory(folder);
        string path = folder + "/" + kind + ".png";
        var scene = EditorSceneManager.NewPreviewScene();
        var host = new GameObject("Orbital module portrait"); SceneManager.MoveGameObjectToScene(host, scene);
        var material = new Material(Shader.Find("Sprites/Default"));
        var target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        try
        {
            Bounds bounds = default; bool first = true;
            foreach (var source in view.Body.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (source.sprite == null || source == view.Halo) continue;
                var go = new GameObject(source.name); go.layer = 31; go.transform.SetParent(host.transform, false);
                go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                go.transform.localScale = source.transform.lossyScale;
                var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = source.sprite;
                renderer.color = source.color; renderer.sharedMaterial = material;
                renderer.sortingOrder = source.sortingOrder;
                if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
            }
            var cameraObject = new GameObject("Portrait camera"); cameraObject.transform.SetParent(host.transform);
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
            camera.scene = scene;
            camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.14f;
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z - 10);
            camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear; camera.targetTexture = target;
            camera.Render(); RenderTexture.active = target;
            var texture = new Texture2D(128,128,TextureFormat.RGBA32,false);
            texture.ReadPixels(new Rect(0,0,128,128),0,0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
        }
        finally
        {
            RenderTexture.active = previous; Object.DestroyImmediate(host); target.Release();
            Object.DestroyImmediate(target); Object.DestroyImmediate(material); EditorSceneManager.ClosePreviewScene(scene);
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single; importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        go.transform.SetParent(parent, false); return (RectTransform)go.transform;
    }
    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void Place(RectTransform r, float x, float y, float w, float h, Vector2 anchor)
    { r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = new Vector2(x,y); r.sizeDelta = new Vector2(w,h); }
    private static void Width(RectTransform r, float inset)
    { r.anchorMin = new Vector2(0, r.anchorMin.y); r.anchorMax = new Vector2(1,r.anchorMax.y); r.sizeDelta = new Vector2(-inset,r.sizeDelta.y); }
    private static void RightColumn(RectTransform r, float inset)
    { r.anchorMin = new Vector2(.44f, r.anchorMin.y); r.anchorMax = new Vector2(1,r.anchorMax.y); r.sizeDelta = new Vector2(-inset,r.sizeDelta.y); }
    private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size)
    {
        var t = Rect(name,parent).gameObject.AddComponent<TextMeshProUGUI>(); t.font = font;
        t.fontSize = size; t.color = StationPixelVisuals.Text; t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }
    private static void Set(Object target, string property, Object value)
    { var so = new SerializedObject(target); so.FindProperty(property).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
}
#endif
