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

    [UnityTest, Timeout(90000)]
    public IEnumerator SandboxBasePowersKeepMovementDamageAndReactivation()
    {
        BunkerDebugTools.SetGameView1600();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return VerifySandboxBasePowers();
    }

    private static IEnumerator VerifySandboxBasePowers()
    {
        yield return CoreTestSupport.LoadBunker();
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MVP" &&
            PlayerRuntimeReference.CachedPlayer != null &&
            ProductionSceneComposition.Active is { IsReady: true } && !SceneTransitionOverlay.IsTransitioning);
        var player = PlayerRuntimeReference.CachedPlayer;
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        spawner.StopSpawning();
        player.GetComponent<PlayerHealth>().DebugInvulnerable = true;
        player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        player.GetComponentInChildren<Subject42.Combat.OrbitalStation.OrbitalStationRuntime>().enabled = false;
        foreach (var existing in EnemyHealth.ActiveInstances.ToArray()) Object.Destroy(existing.gameObject);
        foreach (var site in ProductionAnomalySite.ActiveSites.ToArray()) site.DisposeSite();
        yield return null;
        const string folder = "Artifacts/GeneratedQA/RemainingCleanup";
        Directory.CreateDirectory(folder);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab");
        foreach (var power in new[] { AnomalyPowerType.GravityOrb, AnomalyPowerType.ArcNode, AnomalyPowerType.RedBeam })
        {
            var enemy = spawner.SpawnDebugEnemyAt(prefab, player.transform.position + Vector3.right * 3f).GetComponent<EnemyHealth>();
            enemy.GetComponent<EnemyMovement>().enabled = false;
            enemy.GetComponent<EnemyCollisionHandler>().enabled = false;
            var body = enemy.GetComponent<Rigidbody2D>();
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            foreach (int level in new[] { 1, 3 })
            {
                enemy.SetRuntimeMaxHealth(10000f);
                AnomalyPowerRuntime.EnsurePower(player, power, level);
                var runtime = player.GetComponents<MonoBehaviour>().Single(value => value is IAnomalyPowerRuntime item && item.Type == power);
                string visualName = power == AnomalyPowerType.GravityOrb ? "Gravity Construct Orb" :
                    power == AnomalyPowerType.ArcNode ? "Arc Construct" : "Beam Construct";
                var visual = player.transform.Find(visualName);
                Assert.That(visual, Is.Not.Null);
                Assert.That(visual.gameObject.activeInHierarchy && runtime.enabled, Is.True);
                Vector3 start = visual.position;
                float deadline = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < deadline &&
                    (enemy.CurrentHealth >= 10000f || Vector3.Distance(start, visual.position) < .05f))
                {
                    if (power == AnomalyPowerType.GravityOrb)
                    {
                        body.position = visual.position;
                        body.transform.position = body.position;
                        Physics2D.SyncTransforms();
                    }
                    yield return null;
                }
                Assert.That(enemy.CurrentHealth, Is.LessThan(10000f), power + " level " + level + " base damage");
                Assert.That(Vector3.Distance(start, visual.position), Is.GreaterThan(.05f), power + " movement");
                if (level == 3)
                {
                    ScreenCapture.CaptureScreenshot(folder + "/" + power + "-first.png");
                    yield return WaitRealtime(.2f);
                    ScreenCapture.CaptureScreenshot(folder + "/" + power + "-second.png");
                    yield return null;
                }
                Assert.That(AnomalyPowerRuntime.DeactivatePower(player, power), Is.True);
                Assert.That(runtime.enabled || visual.gameObject.activeInHierarchy, Is.False);
            }
            Object.Destroy(enemy.gameObject);
            yield return null;
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
