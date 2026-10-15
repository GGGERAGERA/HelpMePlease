#if UNITY_EDITOR
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class EnemyOrbitalLabAuthoring
{
    private const string LabFolder = "Assets/_Project/Dev/Labs/OrbitalLab/";
    [MenuItem("Tools/Subject42/Dev/OrbitalLab/Prepare artist weapons")]
    public static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != EnemyOrbitalLabController.ScenePath)
            scene = EditorSceneManager.OpenScene(EnemyOrbitalLabController.ScenePath);
        var lab = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyOrbitalLabController>(true)).Single();
        var config = AssetDatabase.LoadAssetAtPath<OrbitalPresentationConfig>("Assets/_Project/Data/Orbital/OrbitalPresentationConfig.asset");
        lab.Shotgun.WeaponVisual = config.ShotgunPrefab;
        lab.Sniper.WeaponVisual = config.SniperPrefab;
        lab.Saw.WeaponVisual = config.SawPrefab;
        lab.PistolProjectileVisual = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Orbital/BlueProjectileVisual.prefab");
        lab.Shotgun.ProjectileVisual = lab.Sniper.ProjectileVisual = lab.PistolProjectileVisual;
        lab.Pistol.ProjectileVisual = lab.PistolProjectileVisual;
        lab.Pistol.WeaponVisual = config.PistolPrefab;
        lab.Shotgun.ProjectileRadius = lab.Sniper.ProjectileRadius = .14f;
        lab.Shotgun.ProjectileVisualScale = lab.Sniper.ProjectileVisualScale = 1f;
        lab.Shotgun.Category = lab.Sniper.Category = OrbitalUpgradeCategory.Shooting;
        lab.Saw.Category = OrbitalUpgradeCategory.Contact;
        lab.Shotgun.BypassShields = false; lab.Sniper.BypassShields = true;
        lab.Shotgun.Color = new Color(.25f, 1f, .4f);
        if (lab.LabUpgradePanel == null)
        {
            var canvasObject = new GameObject("Lab upgrade cards", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(lab.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 31000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var panel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/prefabs/UI/_UpdateUI/MVPUpgradePanel.prefab"), canvasObject.transform);
            lab.LabUpgradePanel = panel.GetComponent<UpgradePanelView>();
            panel.SetActive(false);
        }
        EditorUtility.SetDirty(lab);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }
    private static GameObject WrapArtist(string artistName, string name, OrbitalPresentationConfig config, bool disk)
    {
        var root = new GameObject(name);
        try
        {
            var view = root.AddComponent<OrbitalModuleView>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/miniWeapons/" + artistName + ".prefab");
            var artist = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            // Installation removes the scene-placement offset; all inner art, colors, materials and lights are retained.
            artist.transform.localPosition = Vector3.zero;
            foreach (var collider in artist.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
            view.Body = artist.transform;
            var bodySprites = artist.GetComponentsInChildren<SpriteRenderer>(true);
            if (disk)
            {
                Bounds bounds = bodySprites.First(s => s.enabled).bounds;
                foreach (var sprite in bodySprites.Where(s => s.enabled)) bounds.Encapsulate(sprite.bounds);
                Vector3 localCenter = artist.transform.InverseTransformPoint(bounds.center);
                artist.transform.localScale *= 1.3f / Mathf.Max(bounds.size.x, bounds.size.y);
                artist.transform.localPosition = -Vector3.Scale(localCenter, artist.transform.localScale);
            }
            var halo = Object.Instantiate(config.PistolPrefab.GetComponent<OrbitalModuleView>().Halo, root.transform, false);
            halo.name = "Selection halo"; halo.enabled = false; view.Halo = halo;
            view.Sprites = bodySprites.Concat(new[] { halo }).ToArray();
            view.Particles = artist.GetComponentsInChildren<ParticleSystem>(true);
            view.Icon = bodySprites.First(s => s.enabled).sprite; view.IconUsesOwnColors = true;
            return PrefabUtility.SaveAsPrefabAsset(root, LabFolder + name + ".prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }
}
#endif
