#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class AnomalyFocusAlignmentTests
{
    private const string Output = "Artifacts/GeneratedQA/ZoneEdgeCorrection-20261026";
    private static readonly FieldInfo OverlayField = typeof(LevelAnomalyController)
        .GetField("focusOverlay", BindingFlags.NonPublic | BindingFlags.Instance);

    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        var cleanup = CoreTestSupport.CleanupPlayMode();
        while (cleanup.MoveNext()) yield return cleanup.Current;
    }

    [TestCase("Berserk")]
    [TestCase("Stasis")]
    [TestCase("ExplosiveZone")]
    [TestCase("Gravity")]
    [TestCase("Glitch")]
    public void ProductionZoneKeepsItsAuthoredShader(string kind)
    {
        var data = AssetDatabase.LoadAssetAtPath<LocalAnomalyData>(
            $"Assets/_Project/Data/Anomalies/LocalAnomaly_{kind}.asset");
        var renderers = data.ZonePrefab.GetComponentsInChildren<MeshRenderer>(true);
        Assert.That(renderers, Is.Not.Empty, "The zone's authored shader presentation must remain available.");
        foreach (var renderer in renderers)
        {
            Assert.That(renderer.sharedMaterial, Is.Not.Null);
            Assert.That(renderer.sharedMaterial.shader.name, Does.StartWith("World/"));
        }
    }

    [UnityTest, Timeout(90000)]
    public IEnumerator FocusWindowMatchesWorldZoneAtDifferentHudScales()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return VerifyProductionFocus();
    }

    private static IEnumerator VerifyProductionFocus()
    {
        yield return CoreTestSupport.LoadBunker();
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => ProductionSceneComposition.Active is { IsReady: true } &&
            PlayerRuntimeReference.CachedPlayer != null && !SceneTransitionOverlay.IsTransitioning);
        var player = PlayerRuntimeReference.CachedPlayer;
        player.GetComponent<PlayerHealth>().AddMaxHealth(1000000f);
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        player.GetComponentInChildren<Subject42.Combat.OrbitalStation.OrbitalStationRuntime>().enabled = false;
        foreach (var enemy in EnemyHealth.ActiveInstances.ToArray()) Object.Destroy(enemy.gameObject);
        foreach (var site in ProductionAnomalySite.ActiveSites.ToArray()) site.DisposeSite();
        yield return null;
        var controller = Object.FindFirstObjectByType<LevelAnomalyController>();
        var canvas = controller.GetComponentInParent<Canvas>();
        var scaler = canvas.GetComponent<CanvasScaler>();
        Assert.That(scaler, Is.Not.Null, "Exercise the actual production HUD scaling ancestor.");
        var originalReference = scaler.referenceResolution;
        var config = AssetDatabase.LoadAssetAtPath<ExplorationSectorConfig>(
            "Assets/_Project/Data/World/ExplorationSectorConfig.asset");
        var data = AssetDatabase.LoadAssetAtPath<LocalAnomalyData>(
            "Assets/_Project/Data/Anomalies/LocalAnomaly_Stasis.asset");
        Vector2 center = GameplayAreaService.Instance.PlayableArea.bounds.center;
        var owner = new GameObject("Focus edge alignment regression").AddComponent<ProductionAnomalySite>();
        owner.InitializeNormal(center, new Vector2(18f, 14f), data, null, null,
            controller, center + Vector2.one * 100f, 2.5f, config.AnomalyTerritoryFill);
        var movement = player.GetComponent<CharacterMovement2D>();
        movement.MovementIntent = () => Vector2.zero;
        var body = player.GetComponent<Rigidbody2D>();
        body.position = center;
        player.transform.position = center;
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        Directory.CreateDirectory(Output);
        try
        {
            foreach (float scale in new[] { .75f, 1f, 1.25f })
            {
                scaler.referenceResolution = new Vector2(Screen.width, Screen.height) / scale;
                yield return CoreTestSupport.Await(() => Mathf.Abs(canvas.scaleFactor - scale) < .005f &&
                    controller.GetZoneFocusAmount(owner.AnomalyZone) > .99f);
                yield return WaitRealtime(.2f);
                var overlay = (SpriteRenderer)OverlayField.GetValue(controller);
                Assert.That(overlay != null && overlay.enabled && overlay.gameObject.activeInHierarchy, Is.True);
                var properties = new MaterialPropertyBlock();
                overlay.GetPropertyBlock(properties);
                var ratio = properties.GetVector("_ClearRatio");
                Vector2 actualClearSize = Vector2.Scale(overlay.bounds.size, new Vector2(ratio.x, ratio.y));
                Bounds area = owner.AnomalyZone.FocusArea.bounds;
                string name = "focus-hud-" + scale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                File.WriteAllText(Path.Combine(Output, name + ".txt"),
                    $"canvasScale={canvas.scaleFactor:R}; parentScale={overlay.transform.parent.lossyScale}; " +
                    $"overlayWorldSize={overlay.bounds.size}; clearWorldSize={actualClearSize}; areaWorldSize={area.size}");
                yield return Capture(name + "-a");
                if (scale == .75f)
                {
                    yield return WaitRealtime(.3f);
                    yield return Capture(name + "-b");
                }
                Assert.That(actualClearSize.x, Is.EqualTo(area.size.x).Within(.01f),
                    "A HUD scale must not inset the world-space focus window inside the zone border.");
                Assert.That(actualClearSize.y, Is.EqualTo(area.size.y).Within(.01f));
                Assert.That(Vector2.Distance(overlay.bounds.center, area.center), Is.LessThan(.01f));
                Assert.That(owner.AnomalyZone.GetComponentsInChildren<MeshRenderer>(true), Is.Not.Empty,
                    "Correcting the mask must preserve the zone shader.");
            }
        }
        finally
        {
            scaler.referenceResolution = originalReference;
            movement.MovementIntent = null;
            owner.DisposeSite();
        }
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until) yield return null;
    }

    private static IEnumerator Capture(string name)
    {
        string path = Path.Combine(Output, name + ".png");
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        yield return CoreTestSupport.Await(() => File.Exists(path));
    }
}
#endif
