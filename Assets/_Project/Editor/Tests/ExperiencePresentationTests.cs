using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class ExperiencePresentationTests
{
    private const string Output = "Artifacts/GeneratedQA/ExperiencePresentation";
    private readonly Subject42FinalBossFlowTests preferences = new();
    [SetUp] public void Setup() { preferences.PreserveRewardsAndUnlockProgress(); Directory.CreateDirectory(Output); }
    [UnityTearDown] public IEnumerator Cleanup() => preferences.CleanupPlayMode();
    private static object Get(object o, string n) => o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    private static void Set(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, v);
    private static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);
    private static T One<T>() where T : Object => Object.FindFirstObjectByType<T>();
    // These are EditMode tests entering Play Mode: use explicit frame clocks instead
    // of WaitForSeconds, which the editor coroutine runner does not schedule.
    private static IEnumerator Wait(float seconds)
    {
        float until = Time.time + seconds;
        while (Time.time < until) yield return null;
        yield return null;
    }

    [Test]
    public void RebuildPreservesGameplayAndStableSpriteReferences()
    {
        var before = AssetDatabase.LoadAssetAtPath<GameObject>(ExperienceVisualAuthoring.PickupPath);
        string gameplay = EditorJsonUtility.ToJson(before.GetComponent<BoxCollider2D>()) + EditorJsonUtility.ToJson(before.GetComponent<Rigidbody2D>());
        string source = Convert.ToBase64String(File.ReadAllBytes(ExperienceVisualAuthoring.SourcePath));
        ExperienceVisualAuthoring.Build();
        string atlas = ExperienceVisualAuthoring.Root + "/XP_Cores.png";
        string[] Ids() => AssetDatabase.LoadAllAssetsAtPath(atlas).OfType<Sprite>().Select(s =>
        { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string guid, out long id); return s.name + ":" + guid + ":" + id; }).OrderBy(s => s).ToArray();
        var ids = Ids();
        ExperienceVisualAuthoring.Build();
        Assert.That(Ids(), Is.EqualTo(ids));
        Assert.That(ids.Length, Is.EqualTo(7));
        Assert.That(Convert.ToBase64String(File.ReadAllBytes(ExperienceVisualAuthoring.SourcePath)), Is.EqualTo(source));
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExperienceVisualAuthoring.PickupPath);
        Assert.That(EditorJsonUtility.ToJson(prefab.GetComponent<BoxCollider2D>()) + EditorJsonUtility.ToJson(prefab.GetComponent<Rigidbody2D>()), Is.EqualTo(gameplay));
        Assert.That(prefab.GetComponentsInChildren<Animator>(), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<TrailRenderer>(), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<Light2D>(), Is.Empty);
        Assert.That(prefab.GetComponent<ExperiencePickup>(), Is.Not.Null);
        var definitions = (ExperienceVisualPreset[])Get(prefab.GetComponent<ExperiencePickupVisual>(), "presets");
        Assert.That(definitions.Length, Is.EqualTo(6));
        Assert.That(definitions.Count(p => p.productionWeight > 0), Is.EqualTo(5));
        var importer = (TextureImporter)AssetImporter.GetAtPath(atlas);
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(32));
        Assert.That(definitions.All(p => p.sprite.pivot == p.sprite.rect.size * .5f), Is.True);
        var texture = new Texture2D(2, 2); texture.LoadImage(File.ReadAllBytes(atlas));
        Assert.That(texture.GetPixels32().All(p => p.a == 0 || p.a == 255), Is.True);
        Object.DestroyImmediate(texture);
    }

    [UnityTest]
    public IEnumerator EnemyDropsMagnetTelekinesisAndPooledFeedbackInProductionScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseProductionScene();
    }

    private static IEnumerator ExerciseProductionScene()
    {
        var start = typeof(Subject42FinalBossFlowTests).GetMethod("StartRun", BindingFlags.Static | BindingFlags.NonPublic);
        yield return (IEnumerator)start.Invoke(null, new object[] { "Gera" });
        Assert.That(One<CharacterSpawner>(), Is.Not.Null, "Character spawner after run setup");
        var player = One<CharacterSpawner>().SpawnedPlayer;
        Assert.That(player, Is.Not.Null, "Production player");
        Assert.That(player.GetComponent<PlayerHealth>(), Is.Not.Null, "Production health");
        player.GetComponent<PlayerHealth>().SetRuntimeHealth(100000, 100000);
        player.GetComponent<CharacterMovement2D>().enabled = false;
        player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        var spawner = One<EnemySpawner>();
        Assert.That(spawner, Is.Not.Null, "Production enemy spawner");
        spawner.enabled = false;
        var station = One<OrbitalStationRuntime>();
        var tele = station.GetComponent<OrbitalWorldTelekinesisController>();
        tele.enabled = false; // Drive domain operations below; ignore live editor mouse input.
        Vector3 center = player.transform.position;
        var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab");
        Assert.That(enemyPrefab, Is.Not.Null);
        Assert.That(enemyPrefab.GetComponent<EnemyHealth>(), Is.Not.Null);
        var enemies = new EnemyHealth[45];
        for (int i = 0; i < enemies.Length; i++)
        {
            enemies[i] = Object.Instantiate(enemyPrefab, center + new Vector3(5.5f + i % 9 * .42f, -2.7f + i / 9 * .9f), Quaternion.identity).GetComponent<EnemyHealth>();
            Set(enemies[i], "lootAmount", 1);
        }
        yield return null;
        var existingDrops = Object.FindObjectsByType<ExperiencePickup>(FindObjectsSortMode.None);
        var previous = new System.Collections.Generic.HashSet<ExperiencePickup>(existingDrops);
        foreach (var enemy in enemies) if (enemy != null) enemy.TakeDamage(100000, enemy.transform.position);
        yield return null;
        var drops = Object.FindObjectsByType<ExperiencePickup>(FindObjectsSortMode.None).Where(p => !previous.Contains(p)).ToArray();
        Assert.That(drops.Length, Is.InRange(40, 50), "Actual enemy death loot");
        Assert.That(drops.All(p => Vector2.Distance(p.transform.position, player.transform.position) > player.GetComponent<PlayerPickupRadius>().CurrentRadius + .5f), Is.True, "Idle field must be outside the real pickup radius");
        var visuals = drops.Select(p => p.GetComponent<ExperiencePickupVisual>()).ToArray();
        var cores = visuals.Select(v => (SpriteRenderer)Get(v, "core")).ToArray();
        Assert.That(cores.Select(c => c.sprite.name).Distinct().Count(), Is.GreaterThanOrEqualTo(4));
        Assert.That(visuals.Select(v => (float)Get(v, "phase")).Distinct().Count(), Is.EqualTo(visuals.Length));
        var positions = drops.Select(p => p.transform.position).ToArray();
        var initialBob = cores.Select(c => c.transform.localPosition.y).ToArray();
        var bobbed = new System.Collections.Generic.HashSet<int>();
        for (int frame = 0; frame < 12; frame++)
        {
            yield return Wait(.1f);
            for (int i = 0; i < cores.Length; i++)
                if (Mathf.Abs(cores[i].transform.localPosition.y - initialBob[i]) > .01f) bobbed.Add(i);
        }
        for (int i = 0; i < drops.Length; i++)
        {
            var boundPlayer = (Transform)Get(drops[i], "player");
            var boundRadius = (PlayerPickupRadius)Get(drops[i], "playerPickupRadius");
            Assert.That(Vector3.Distance(drops[i].transform.position, positions[i]), Is.LessThan(.001f),
                $"Idle origin: {positions[i]} -> {drops[i].transform.position}; player {player.transform.position}; bound {boundPlayer?.name} {boundPlayer?.position} radius {boundRadius?.CurrentRadius}; external {((AnomalyExternalVelocityStack)Get(drops[i], "anomalyExternalVelocity")).Value}; body {drops[i].GetComponent<Rigidbody2D>().linearVelocity}; held {One<OrbitalWorldTelekinesisController>().IsHolding}");
            Assert.That(cores[i].transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(cores[i].transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(cores[i].transform.position.y * 32, Is.EqualTo(Mathf.Round(cores[i].transform.position.y * 32)).Within(.001f));
            Assert.That(cores[i].sharedMaterial.GetFloat("_Pulse"), Is.EqualTo(.04f).Within(.001f));
        }
        Assert.That(bobbed.Count, Is.GreaterThan(5));
        for (int i = 0; i < 24; i++)
        {
            var enemy = Object.Instantiate(enemyPrefab, center + new Vector3(9.5f + i % 6 * .45f, -2 + i / 6 * 1.1f), Quaternion.identity).GetComponent<EnemyHealth>();
            enemy.SetRuntimeMaxHealth(100000);
        }
        yield return Wait(1f);
        ScreenCapture.CaptureScreenshot(Output + "/01-forty-five-drops.png");
        yield return Wait(.2f);
        // Move the real player/camera while the drop field remains present.
        var body = player.GetComponent<Rigidbody2D>();
        for (int i = 0; i < 20; i++) { body.position += Vector2.up * .025f; yield return null; }
        ScreenCapture.CaptureScreenshot(Output + "/02-camera-motion.png");
        yield return Wait(.2f);
        var held = drops[0];
        var heldPosition = held.transform.position;
        Call(tele, "ApplyHighlight", held);
        Call(tele, "BeginHold", held);
        Assert.That(tele.IsHolding, Is.True);
        Assert.That(station.InputOwner.Mode, Is.EqualTo(OrbitalInteractionMode.WorldTelekinesis));
        yield return Wait(.1f);
        Assert.That(held.transform.position, Is.EqualTo(heldPosition));
        Vector2 desired = center + new Vector3(5.2f, .7f);
        for (int i = 0; i < 45; i++)
        {
            Call(tele, "MoveHeldTowards", desired);
            yield return null;
        }
        Assert.That(Vector2.Distance(held.transform.position, desired), Is.LessThan(.2f), "Actual pull/follow movement");
        Call(tele, "ReleaseHeld");
        Assert.That(tele.IsHolding, Is.False);
        Call(tele, "BeginHold", held); // Re-grab must cancel its previous flight.
        Assert.That(tele.IsHolding, Is.True);
        tele.CancelInteraction();
        Assert.That(station.InputOwner.IsIdle, Is.True);
        var manager = ExperienceManager.Instance;
        manager.RestoreRuntimeExperience(1, 0);
        // Avoid a reward pause during the mass pickup test without changing the curve asset.
        Set(manager, "expToNextLevel", 100000);
        int notifications = 0;
        manager.OnExperienceChanged.AddListener((a, b) => notifications++);
        int before = manager.CurrentExp;
        Call(held, "Collect"); Call(held, "Collect");
        Assert.That(notifications, Is.EqualTo(1));
        int award = manager.CurrentExp - before;
        Assert.That(award, Is.GreaterThan(0));
        yield return null;
        Assert.That(held == null, Is.True, "Gameplay lifetime was delayed for FX");
        foreach (var drop in drops.Skip(1)) drop.transform.position = player.transform.position + Vector3.right * .5f;
        var deadline = Time.realtimeSinceStartup + 3;
        while (drops.Skip(1).Any(p => p != null) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(drops.All(p => p == null), Is.True, "Magnet failed to absorb the field");
        Assert.That(notifications, Is.EqualTo(drops.Length));
        Assert.That(manager.CurrentExp - before, Is.EqualTo(award * drops.Length));
        yield return Wait(.25f);
        Assert.That(Object.FindObjectsByType<ExperiencePickupEffect>(FindObjectsSortMode.None), Is.Empty);
        var pool = (SimplePrefabPool)Get(manager, "pickupEffectPool");
        Assert.That(pool, Is.Not.Null);
        var effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Pickups/XP_PickupFlash.prefab").GetComponent<ExperiencePickupEffect>();
        var pooledIds = manager.GetComponentsInChildren<ExperiencePickupEffect>(true).Select(p => p.GetInstanceID()).OrderBy(id => id).ToArray();
        for (int i = 0; i < 12; i++)
        {
            manager.PlayPickupEffect(effectPrefab, player.transform.position, Color.cyan);
            yield return Wait(.02f);
        }
        yield return Wait(.25f);
        Assert.That(manager.GetComponentsInChildren<ExperiencePickupEffect>(true).Select(p => p.GetInstanceID()).OrderBy(id => id), Is.EqualTo(pooledIds));
        File.WriteAllText(Output + "/runtime.txt", $"PASS: {drops.Length} enemy drops; distinct visuals/phase; root invariant; integer pixel bob; zero rotation; shader pulse; grab/pull/follow/release/regrab; magnet; {notifications} single XP awards; pooled FX reuse. OS mouse input not simulated.\n");
    }
}
