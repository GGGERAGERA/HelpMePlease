#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class LiveVisualProductionTests
{
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest]
    public IEnumerator ProductionTypesNewSpawnsParticlesLightsAndProfileRoundTrip()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        // EnterPlayMode must be a top-level yield instruction for the EditMode runner.
        yield return new EnterPlayMode();
        yield return VerifyProductionScenario();
    }

    // Create runtime locals and captured lambdas only after the domain reload.
    private static IEnumerator VerifyProductionScenario()
    {
        string output = Path.Combine("Artifacts/GeneratedQA/LiveVisualStudio",
            "run-" + System.DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        yield return CoreTestSupport.LoadBunker();
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            Object.FindFirstObjectByType<Subject42.Combat.OrbitalStation.OrbitalStationRuntime>() is { IsInitialized: true } &&
            !SceneTransitionOverlay.IsTransitioning);
        Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponent<PlayerHealth>().AddMaxHealth(1000000f);
        yield return new WaitForSecondsRealtime(1.2f);
        var runtime = LiveVisualRuntime.Instance;
        Assert.That(runtime, Is.Not.Null);
        Assert.That(runtime.Preset.LiveOriginalBaseline, Is.Not.Null, "Prepare the authored production catalog first.");
        Assert.That(runtime.Original.BaselineRevision, Does.StartWith("19b6c01"));
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        var camera = Camera.main;
        Assert.That(camera, Is.Not.Null);
        var classic = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab");
        var bomber = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/p_Enemy_Bomber.prefab");
        string[] authoredPaths = runtime.Preset.LiveSources.Select(s => s.AssetPath)
            .Concat(runtime.Preset.LiveHistoricalMaterials.Select(m => m.AssetPath).Where(p => p.StartsWith("Assets/") && !p.Contains("/Fonts/")))
            .Concat(new[] { "Assets/_Project/Data/Orbital/OrbitalPresentationConfig.asset", "Assets/_Project/Materials/World/EnemyBodyReadabilityLit.mat",
                VisualTuningPresetStorage.AssetPath, "Assets/_Project/Shaders/Subject42_EnvironmentTileLit.shader", "Assets/_Project/Scenes/MainBuild/MVP.unity" })
            .Distinct().ToArray();
        var authored = authoredPaths.ToDictionary(p => p, File.ReadAllBytes);
        var one = spawner.SpawnDebugEnemyAt(classic, player.transform.position + new Vector3(3, 1));
        var red = spawner.SpawnDebugEnemyAt(bomber, player.transform.position + new Vector3(5, -2));
        Assert.That(one != null && red != null, Is.True, "Production debug spawns must return live instances.");
        one.AddComponent<CombatFeelTestDummy>();
        red.AddComponent<CombatFeelTestDummy>();
        foreach (var behaviour in one.GetComponents<EnemyMovement>()) behaviour.enabled = false;
        foreach (var behaviour in red.GetComponents<EnemyMovement>()) behaviour.enabled = false;
        runtime.RefreshScene();
        var bodyRenderer = SelectVisibleBody(one, "Enemy2Simple1");
        var redRenderer = SelectVisibleBody(red, "Enemy2Bomber1");
        Assert.That(bodyRenderer.enabled && bodyRenderer.gameObject.activeInHierarchy, Is.True);
        Assert.That(GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), bodyRenderer.bounds), Is.True,
            "The tuned production renderer must be inside the actual gameplay camera.");
        var target = runtime.Targets.Single(t => t.Component == bodyRenderer);
        var redTarget = runtime.Targets.Single(t => t.Component == redRenderer);
        var saturation = target.Properties.Single(p => p.Id == "shader._StudioSaturation");
        var brightness = target.Properties.Single(p => p.Id == "shader._StudioBrightness");
        var contrast = target.Properties.Single(p => p.Id == "shader._StudioContrast");
        var alpha = target.Properties.Single(p => p.Id == "sprite.tint.a");
        var sprite = (SpriteRenderer)target.Component;
        var originalMaterial = sprite.sharedMaterial;
        var collider = one.GetComponent<Collider2D>();
        var colliderBounds = collider != null ? collider.bounds.size : Vector3.zero;
        float redAlpha = ((SpriteRenderer)redTarget.Component).color.a;
        var debugMenu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        typeof(Subject42DebugMenu).GetMethod("ToggleHub", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(debugMenu, null);
        yield return null; yield return null;
        yield return Capture(output, "00-debug-hub.png");
        typeof(Subject42DebugMenu).GetMethod("CloseHub", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(debugMenu, null);
        LiveVisualStudio.Open();
        var studio = Object.FindFirstObjectByType<LiveVisualStudio>();
        var selected = typeof(LiveVisualStudio).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic);
        selected.SetValue(studio, target);
        typeof(LiveVisualStudio).GetMethod("RefreshSelection", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(studio, null);
        yield return null; yield return null;
        yield return Capture(output, "01-current.png");

        runtime.BeginEdit();
        runtime.Edit("Type", target, saturation, 0);
        runtime.Edit("Type", target, brightness, 1.3f);
        runtime.Edit("Type", target, contrast, 1.2f);
        runtime.Edit("Type", target, alpha, .6f);
        yield return null;
        Assert.That(sprite.sharedMaterial, Is.Not.SameAs(originalMaterial), "Only a runtime material clone may carry studio changes.");
        var block = new MaterialPropertyBlock(); sprite.GetPropertyBlock(block);
        Assert.That(block.GetFloat("_StudioSaturation"), Is.Zero);
        Assert.That(sprite.color.a, Is.EqualTo(.6f).Within(.001f));
        Assert.That(((SpriteRenderer)redTarget.Component).color.a, Is.EqualTo(redAlpha), "Bomber must not inherit another type's settings.");
        yield return Capture(output, "02-desaturated.png");

        runtime.BeginEdit(); runtime.Edit("Type", target, saturation, 4);
        yield return Capture(output, "03-saturated.png");
        Assert.That(target.Group, Is.EqualTo("Враги / Обычные"));
        Assert.That(redTarget.Group, Is.Not.EqualTo(target.Group));
        runtime.BeginEdit(); runtime.Edit("Group", target, saturation, 2.5f);
        sprite.GetPropertyBlock(block);
        Assert.That(block.GetFloat("_StudioSaturation"), Is.EqualTo(2.5f), "Group editing must replace the selected narrower Type override immediately.");
        runtime.Undo();
        sprite.GetPropertyBlock(block); Assert.That(block.GetFloat("_StudioSaturation"), Is.EqualTo(4f));
        var newborn = spawner.SpawnDebugEnemyAt(classic, player.transform.position + new Vector3(2, -2));
        newborn.AddComponent<CombatFeelTestDummy>();
        yield return null;
        var newbornRenderer = SelectVisibleBody(newborn, "Enemy2Simple1");
        var newTarget = runtime.Targets.Single(t => t.Component == newbornRenderer);
        Assert.That(newTarget.TypeKey, Is.EqualTo(target.TypeKey));
        var newSprite = (SpriteRenderer)newTarget.Component; newSprite.GetPropertyBlock(block);
        Assert.That(block.GetFloat("_StudioSaturation"), Is.EqualTo(4));
        Assert.That(newSprite.color.a, Is.EqualTo(.6f).Within(.001f));
        newborn.SetActive(false); newborn.SetActive(true); yield return null;
        Assert.That(newSprite.color.a, Is.EqualTo(.6f).Within(.001f), "Reuse must preserve the type override.");
        if (collider != null) Assert.That(collider.bounds.size, Is.EqualTo(colliderBounds), "Visual tuning cannot resize gameplay collision.");

        var global = runtime.Targets.FirstOrDefault(t => t.Component is Light2D l && l.lightType == Light2D.LightType.Global);
        Assert.That(global, Is.Not.Null, "Production Global Light 2D must be discoverable.");
        var intensity = global.Properties.Single(p => p.Id == "light.intensity");
        runtime.BeginEdit(); runtime.Edit("Type", global, intensity, 1.5f);
        Assert.That(((Light2D)global.Component).intensity, Is.EqualTo(1.5f));

        var particles = runtime.Targets.FirstOrDefault(t => t.Component is ParticleSystem ps && ps.gameObject.activeInHierarchy && ps.emission.enabled && ps.emission.rateOverTimeMultiplier > 0);
        Assert.That(particles, Is.Not.Null, "An active production ParticleSystem must be tunable.");
        foreach (var pair in new[] { ("particles.emission", 16f), ("particles.size", .4f), ("particles.lifetime", 1.5f) })
        { runtime.BeginEdit(); runtime.Edit("Type", particles, particles.Properties.Single(p => p.Id == pair.Item1), pair.Item2); }
        var psEdited = (ParticleSystem)particles.Component;
        Assert.That(psEdited.emission.rateOverTimeMultiplier, Is.EqualTo(16f));
        Assert.That(psEdited.main.startSizeMultiplier, Is.EqualTo(.4f));
        Assert.That(psEdited.main.startLifetimeMultiplier, Is.EqualTo(1.5f));
        var particlePrefab = SelectAuthoredPoolPrefab(runtime, output);
        Debug.Log("[LiveVisualStudio QA] Pool prefab=" + particlePrefab.name + "; systems=" +
            string.Join(", ", particlePrefab.GetComponentsInChildren<ParticleSystem>(true).Select(ps => ps.name + ":" + ps.main.stopAction)) +
            "; scripts=" + string.Join(", ", particlePrefab.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b != null).Select(b => b.GetType().Name)));
        var pool = new SimplePrefabPool(runtime, particlePrefab, 1, 4);
        void ConfigureOwnedParticles(PooledGameObject item)
        {
            Assert.That(item != null && item.gameObject != null, Is.True, "The pool must supply a live root before presentation configuration.");
            Debug.Log("[LiveVisualStudio QA] Configure pool root=" + item.name + "; cachedPS=" + (item.PrimaryParticleSystem != null) +
                "; directPS=" + (item.GetComponent<ParticleSystem>() != null));
            Assert.That(item.PrimaryParticleSystem != null, Is.True, "Pool root ParticleSystem was lost before visual configuration: " + item.name);
            var main = item.PrimaryParticleSystem.main;
            main.startSizeMultiplier = .75f;
            main.startLifetime = .14f;
        }
        var pooled = pool.Get(player.transform.position + Vector3.right * 4, Quaternion.identity, configurePresentation: ConfigureOwnedParticles);
        Assert.That(pooled.PrimaryParticleSystem != null, Is.True, "Use an authored pool-safe root ParticleSystem.");
        yield return null;
        Assert.That(pooled.PrimaryParticleSystem != null, Is.True, particlePrefab.name + " destroyed its root ParticleSystem during preparation.");
        var matches = runtime.Targets.Where(t => ReferenceEquals(t.Component, pooled.PrimaryParticleSystem)).ToArray();
        Debug.Log($"[LiveVisualStudio QA] Pool source={particlePrefab.name}; particle={pooled.PrimaryParticleSystem.GetInstanceID()}; registered matches={matches.Length}");
        Assert.That(matches.Length, Is.EqualTo(1), "Pool root ParticleSystem must be registered by its actual component identity.");
        var pooledTarget = matches[0];
        var pooledSize = pooledTarget.Properties.Single(p => p.Id == "particles.size");
        Assert.That(pooledSize.Current, Is.EqualTo(.75f), "Current includes the owner's C# configuration, not just prefab defaults.");
        Assert.That(pooledTarget.Properties.Single(p => p.Id == "particles.lifetime").Current, Is.EqualTo(.14f).Within(.001f));
        runtime.BeginEdit(); runtime.Edit("Type", pooledTarget, pooledSize, .25f);
        Assert.That(pooled.PrimaryParticleSystem.main.startSizeMultiplier, Is.EqualTo(.25f));
        pooled.Release();
        var reused = pool.Get(player.transform.position + Vector3.right * 4, Quaternion.identity, configurePresentation: ConfigureOwnedParticles);
        var emitted = reused.PrimaryParticleSystem;
        emitted.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        emitted.Emit(1);
        var actual = new ParticleSystem.Particle[4];
        Assert.That(emitted.GetParticles(actual), Is.EqualTo(1));
        Assert.That(actual[0].startSize, Is.EqualTo(.25f).Within(.001f), "The first synchronous Emit after pool reuse must use the chosen size before LateUpdate.");
        yield return null;
        Assert.That(reused, Is.SameAs(pooled));
        Assert.That(reused.PrimaryParticleSystem.main.startSizeMultiplier, Is.EqualTo(.25f), "A real pool reuse inherits Type overrides.");
        reused.Release(); pool.Dispose();
        yield return Capture(output, "04-light-and-particles.png");

        string json = runtime.Export("QA round trip");
        File.WriteAllText(Path.Combine(output, "round-trip.json"), json);
        runtime.Reset("Type");
        Assert.That(runtime.Custom.Values, Is.Empty);
        runtime.Load(JsonUtility.FromJson<VisualProfile>(File.ReadAllText(Path.Combine(output, "round-trip.json"))));
        yield return null;
        Assert.That(sprite.color.a, Is.EqualTo(.6f).Within(.001f));
        sprite.GetPropertyBlock(block); Assert.That(block.GetFloat("_StudioSaturation"), Is.EqualTo(4));
        runtime.SetMode(VisualComparisonMode.Original); yield return Capture(output, "05-git-original.png");
        runtime.SetMode(VisualComparisonMode.Current); yield return null;
        Assert.That(sprite.sharedMaterial, Is.SameAs(originalMaterial));
        Assert.That(sprite.color.a, Is.EqualTo(alpha.Current).Within(.001f));
        runtime.SetMode(VisualComparisonMode.Custom); yield return Capture(output, "06-custom-restored.png");
        foreach (var pair in authored) Assert.That(File.ReadAllBytes(pair.Key), Is.EqualTo(pair.Value), pair.Key + " was changed during live tuning.");
        File.WriteAllText(Path.Combine(output, "scenario.txt"), "Runtime checks passed: production startup, two enemy types, saturation/HDR/contrast/alpha, new spawn and reuse, particles, Global Light 2D, JSON round trip, Original/Current/Custom, authored asset byte equality.\nPhysical input and PNG appearance require separate observation.\n");
    }

    private static GameObject SelectAuthoredPoolPrefab(LiveVisualRuntime runtime, string output)
    {
        // Materialize once: C# property patterns do not test Unity's overloaded null.
        var candidates = runtime.Preset.LiveSources.Select(source => new
        {
            source.AssetPath,
            Prefab = source.Prefab,
            Root = source.Prefab != null ? source.Prefab.GetComponent<ParticleSystem>() : null
        }).ToArray();
        string diagnosticPath = Path.Combine(output, "pool-source.txt");
        void Record(string message)
        {
            File.AppendAllText(diagnosticPath, message + "\n");
            Debug.Log("[LiveVisualStudio QA] " + message);
        }
        foreach (var candidate in candidates)
            Record("catalog=" + candidate.AssetPath + "; prefab=" + DescribeSourceObject(candidate.Prefab) +
                "; cachedRoot=" + DescribeSourceObject(candidate.Root));

        var selected = candidates.FirstOrDefault(c => PoolSafeRoot(c.Prefab, c.Root));
        Assert.That(selected, Is.Not.Null, "No authored pool-safe root ParticleSystem; see " + diagnosticPath);
        var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(selected.AssetPath);
        var loadedRoot = loaded != null ? loaded.GetComponent<ParticleSystem>() : null;
        Record("selected=" + selected.AssetPath + "; loaded=" + DescribeSourceObject(loaded) +
            "; loadedRoot=" + DescribeSourceObject(loadedRoot) +
            "; sameManaged=" + ReferenceEquals(selected.Prefab, loaded) + "; sameUnity=" + (selected.Prefab == loaded));
        Assert.That(loaded != null && EditorUtility.IsPersistent(loaded), Is.True,
            "Catalog path must resolve to a persistent authored prefab; see " + diagnosticPath);
        Assert.That(AssetDatabase.GetAssetPath(loaded), Is.EqualTo(selected.AssetPath), diagnosticPath);
        Assert.That(PoolSafeRoot(loaded, loadedRoot), Is.True,
            "The same authored path must satisfy every original pool suitability criterion; see " + diagnosticPath);
        // Only replace a catalog reference when identity/persistence proves it is not the authored asset.
        bool catalogIsAuthored = selected.Prefab == loaded && EditorUtility.IsPersistent(selected.Prefab) &&
            AssetDatabase.GetAssetPath(selected.Prefab) == selected.AssetPath;
        Record("useExplicitAuthoredLoad=" + !catalogIsAuthored);
        var root = catalogIsAuthored ? selected.Root : loadedRoot;
        Assert.That(root != null, Is.True, "Selected cached root is Unity-null; see " + diagnosticPath);
        return catalogIsAuthored ? selected.Prefab : loaded;
    }

    private static bool PoolSafeRoot(GameObject prefab, ParticleSystem root) =>
        prefab != null && root != null && root.main.stopAction == ParticleSystemStopAction.None &&
        !root.main.startSize3D && root.main.startSize.mode == ParticleSystemCurveMode.Constant &&
        prefab.GetComponents<MonoBehaviour>().Length == 0;

    private static string DescribeSourceObject(Object value)
    {
        string identity = "managedNull=" + ReferenceEquals(value, null) + "; unityNull=" + (value == null);
        if (!ReferenceEquals(value, null)) identity += "; id=" + value.GetInstanceID();
        if (value == null) return identity;
        var go = value as GameObject;
        if (go == null && value is Component component) go = component.gameObject;
        return identity + "; asset=" + AssetDatabase.GetAssetPath(value) + "; persistent=" + EditorUtility.IsPersistent(value) +
            (go == null ? "" : "; scene=" + go.scene.path + "; sceneValid=" + go.scene.IsValid() +
                "; rootComponents=" + string.Join(",", go.GetComponents<Component>().Select(c => c == null ? "<missing>" : c.GetType().Name)));
    }

    private static IEnumerator Capture(string output, string name)
    {
        yield return null;
        string path = Path.GetFullPath(Path.Combine(output, name));
        if (File.Exists(path)) File.Delete(path); // Only this scenario's disposable QA capture.
        ScreenCapture.CaptureScreenshot(path);
        float deadline = Time.realtimeSinceStartup + 5;
        while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(File.Exists(path), Is.True, "Actual Game View PNG missing: " + path);
        Assert.That(new FileInfo(path).Length, Is.GreaterThan(1000), "Game View capture is empty.");
    }

    private static SpriteRenderer SelectVisibleBody(GameObject actor, string name)
    {
        Assert.That(actor != null, Is.True, "The production actor was destroyed before visible-body selection.");
        var renderers = actor.GetComponentsInChildren<SpriteRenderer>(true);
        Assert.That(renderers, Is.Not.Null);
        var visible = renderers.Where(s => s != null && s.gameObject != null && s.enabled && s.gameObject.activeInHierarchy).ToArray();
        Debug.Log("[LiveVisualStudio QA] " + actor.name + " visible renderers: " + string.Join(", ", visible.Select(s => s.name)));
        var body = visible.SingleOrDefault(s => s.name == name);
        Assert.That(body != null, Is.True, "Required visible production body absent: " + name);
        return body;
    }
}
#endif
