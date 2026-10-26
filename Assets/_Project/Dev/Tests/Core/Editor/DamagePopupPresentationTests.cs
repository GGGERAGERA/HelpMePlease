#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class DamagePopupPresentationTests
{
    private const string PrefabPath = "Assets/_Project/prefabs/UI/DamagePopup.prefab";
    private const string Output = "Artifacts/GeneratedQA/ZoneContourDamage-20261026";

    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();

    [UnityTest]
    public IEnumerator DamageAnimatesWithoutLabAndReturnsToExistingPool()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        Assert.That(PhysicalCombatFeedbackRuntime.LabAvailable, Is.False);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var owner = new GameObject("Popup pool test owner").AddComponent<EnemyWhiteFlash>();
        var pool = new SimplePrefabPool(owner, prefab, 1, 1);
        var item = pool.Get(Vector3.zero, Quaternion.identity);
        var popup = item.DamagePopup;
        var text = popup.GetComponent<TextMeshPro>();
        Vector3 authoredScale = prefab.transform.localScale;
        popup.SetDamage(42);
        Assert.That(text.text, Is.EqualTo("42"));
        Assert.That(popup.transform.localScale.y, Is.GreaterThan(authoredScale.y),
            "The ordinary production path needs an immediate, brief impact accent without F1 Lab.");
        float initialScale = popup.transform.localScale.y;
        float initialAlpha = text.color.a;
        Material damageMaterial = text.fontSharedMaterial;
        // This fixture is driven by EditModeRunner even after EnterPlayMode;
        // YieldInstruction waits are not interpreted by that editor scheduler.
        float hitAt = Time.time;
        while (Time.time < hitAt + .4f) yield return null;
        Assert.That(item.gameObject.activeSelf, Is.True);
        Assert.That(popup.transform.position.y, Is.GreaterThan(0f));
        Assert.That(text.color.a, Is.InRange(.01f, initialAlpha - .01f),
            "Damage must fade during its rise, not disappear at full opacity.");
        Assert.That(popup.transform.localScale.y, Is.LessThan(authoredScale.y * 1.05f));
        Assert.That(text.fontSharedMaterial, Is.SameAs(damageMaterial), "Animation must reuse the authored shared material.");
        while (Time.time < hitAt + 1.1f) yield return null;
        Assert.That(item.gameObject.activeSelf, Is.False, "Popup must return to its scene-owned pool.");
        var reused = pool.Get(Vector3.right, Quaternion.identity);
        Assert.That(reused, Is.SameAs(item));
        reused.DamagePopup.SetDamage(7);
        Assert.That(text.color.a, Is.EqualTo(initialAlpha).Within(.001f), "A pooled fade must reset its opacity.");
        Assert.That(text.fontSharedMaterial, Is.SameAs(damageMaterial));
        reused.Release();
        var labOwner = new GameObject("Popup lab test owner");
        var tuning = labOwner.AddComponent<ProductionFeelTuningController>();
        tuning.Configure();
        reused = pool.Get(Vector3.zero, Quaternion.identity);
        reused.DamagePopup.SetDamage(9);
        Assert.That(reused.transform.localScale.y, Is.EqualTo(initialScale).Within(.001f),
            "Neutral F1 Lab must preserve the authored production animation.");
        reused.Release();
        tuning.Lab.Set(CombatFeelParameter.PopupScalePunch, .3f);
        reused = pool.Get(Vector3.zero, Quaternion.identity);
        reused.DamagePopup.SetDamage(9);
        Assert.That(reused.transform.localScale.y, Is.GreaterThan(initialScale), "F1 punch tuning must remain effective.");
        reused.Release();
        Object.Destroy(labOwner);
        pool.Dispose();
        Object.Destroy(owner.gameObject);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator CriticalNormalHealingAndRewardReuseDoNotLeakPresentation()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Bootstrap/LocalizationService.prefab"));
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var template = prefab.GetComponent<TextMeshPro>();
        var owner = new GameObject("Popup reuse test owner").AddComponent<EnemyWhiteFlash>();
        var pool = new SimplePrefabPool(owner, prefab, 1, 1);
        var item = pool.Get(Vector3.zero, Quaternion.identity);
        var text = item.GetComponent<TextMeshPro>();
        item.DamagePopup.SetDamage(80, true);
        float criticalScale = item.transform.localScale.y;
        Color criticalColor = text.color;
        Assert.That(criticalColor.g, Is.GreaterThan(.3f), "Critical hits use a warm amber accent, not flat debug red.");
        item.Release();
        item = pool.Get(Vector3.zero, Quaternion.identity);
        item.DamagePopup.SetDamage(8);
        Assert.That(item.transform.localScale.y, Is.LessThan(criticalScale));
        Assert.That(text.color, Is.Not.EqualTo(criticalColor));
        item.Release(); // Interrupt animation to catch transform/material state leaks.
        item = pool.Get(Vector3.zero, Quaternion.identity);
        Assert.That(item.transform.localScale, Is.EqualTo(prefab.transform.localScale));
        Assert.That(item.transform.localRotation, Is.EqualTo(prefab.transform.localRotation));
        Assert.That(text.fontSharedMaterial, Is.SameAs(template.fontSharedMaterial));
        Assert.That(text.fontStyle, Is.EqualTo(template.fontStyle));
        item.DamagePopup.SetHealing(2.5f);
        Assert.That(text.text, Is.EqualTo("+2.5"));
        Assert.That(text.color.g, Is.GreaterThan(text.color.r));
        Assert.That(text.renderer.sortingOrder, Is.EqualTo(100));
        DamagePopup.DebugDamageNumbersVisible = false;
        float healingAt = Time.time;
        while (Time.time < healingAt + .4f) yield return null;
        Assert.That(text.renderer.enabled, Is.True, "Capture's damage toggle must leave healing visible.");
        Assert.That(text.color.a, Is.InRange(.2f, .65f));
        Assert.That(item.transform.localScale, Is.EqualTo(prefab.transform.localScale));
        item.Release();
        item = pool.Get(Vector3.zero, Quaternion.identity);
        item.DamagePopup.SetRewardMultiplier(2f, Color.yellow);
        Assert.That(text.text, Does.Contain("2"));
        Assert.That(text.color, Is.EqualTo(Color.yellow));
        Assert.That(text.fontSize, Is.EqualTo(template.fontSize * 1.15f).Within(.001f));
        Assert.That(text.fontSharedMaterial, Is.SameAs(template.fontSharedMaterial));
        Assert.That(text.renderer.sortingOrder, Is.EqualTo(template.renderer.sortingOrder));
        yield return null;
        Assert.That(text.renderer.enabled, Is.True, "Capture's damage toggle must leave rewards visible.");
        DamagePopup.DebugDamageNumbersVisible = true;
        item.Release();
        pool.Dispose();
        Object.Destroy(owner.gameObject);
        yield return new ExitPlayMode();
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator ProductionEnemyDamageShowsNormalAndCriticalThroughGameplayCamera()
    {
        // Keep the domain transition at test level; nested EnterPlayMode iterators
        // cannot reliably resume after Unity reloads the test assembly.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return CaptureProductionDamage();
    }

    private static IEnumerator CaptureProductionDamage()
    {
        // The production Bunker -> MVP sequence from CoreTestSupport.BeginRun,
        // with its EnterPlayMode step flattened into the test above.
        yield return CoreTestSupport.LoadBunker();
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            ProductionSceneComposition.Active is { IsReady: true } &&
            PlayerRuntimeReference.CachedPlayer != null &&
            Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true } &&
            !SceneTransitionOverlay.IsTransitioning);
        Time.timeScale = 1f;
        var player = PlayerRuntimeReference.CachedPlayer;
        player.GetComponent<CharacterMovement2D>().enabled = false;
        player.GetComponent<PlayerHealth>().DebugInvulnerable = true;
        var playerBody = player.GetComponent<Rigidbody2D>();
        playerBody.linearVelocity = Vector2.zero;
        playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        station.enabled = false;
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        spawner.StopSpawning();
        foreach (var activeEnemy in EnemyHealth.ActiveInstances.ToArray()) Object.Destroy(activeEnemy.gameObject);
        yield return null;

        var camera = Camera.main;
        Assert.That(camera, Is.Not.Null);
        Assert.That(camera.isActiveAndEnabled, Is.True);
        var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab");
        // Reuse the spawner's production pool setup, not a synthetic popup.
        var enemy = spawner.SpawnDebugEnemyAt(enemyPrefab, player.transform.position + Vector3.right * 4f);
        foreach (var movement in enemy.GetComponents<EnemyMovement>()) movement.enabled = false;
        var body = enemy.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.linearVelocity = Vector2.zero;
        var health = enemy.GetComponent<EnemyHealth>();
        yield return null;
        health.SetRuntimeMaxHealth(100f);
        Directory.CreateDirectory(Output);
        float normalHeight = 0f;
        foreach (bool critical in new[] { false, true })
        {
            float previousHealth = health.CurrentHealth;
            float hitAt = Time.time;
            health.TakeDamage(8f, enemy.transform.position, critical);
            Assert.That(health.CurrentHealth, Is.EqualTo(previousHealth - 8f), "Presentation must not alter damage.");
            var popup = Object.FindObjectsByType<DamagePopup>(FindObjectsSortMode.None)
                .Single(p => p.GetComponent<TextMeshPro>().text == "8" &&
                    Vector3.Distance(p.transform.position, enemy.transform.position +
                        enemy.GetComponent<EnemyHealthPresentation>().PopupOffset) < .1f);
            Assert.That(popup.GetComponent<PooledGameObject>(), Is.Not.Null,
                "Actual enemy hit presentation must obtain its popup from the existing pool.");
            string prefix = critical ? "damage-critical" : "damage-normal";
            foreach (var frame in new[] { (age: .05f, name: "early"), (age: .25f, name: "mid"), (age: .5f, name: "fade") })
            {
                while (Time.time < hitAt + frame.age) yield return null;
                float height = AssertVisibleDamage(popup, camera, enemy.transform.position);
                if (frame.name == "early")
                {
                    if (!critical) normalHeight = height;
                    else Assert.That(height, Is.InRange(normalHeight * 1.1f, normalHeight * 1.65f),
                        "Critical emphasis must be readable without becoming a giant caption.");
                }
                if (frame.name == "fade")
                    Assert.That(popup.GetComponent<TextMeshPro>().color.a, Is.InRange(.01f, .85f));
                ScreenCapture.CaptureScreenshot(Path.Combine(Output, prefix + "-" + frame.name + ".png"));
                yield return null; // Capture the actual Game View at end of this frame.
            }
            yield return CoreTestSupport.Await(() => !popup.gameObject.activeSelf);
            Assert.That(Camera.main, Is.SameAs(camera), "Use the unchanged production gameplay camera for every capture.");
        }
        Object.Destroy(enemy);
    }

    private static float AssertVisibleDamage(DamagePopup popup, Camera camera, Vector3 enemyPosition)
    {
        Assert.That(popup.gameObject.activeInHierarchy, Is.True);
        var text = popup.GetComponent<TextMeshPro>();
        text.ForceMeshUpdate();
        Assert.That(text.renderer.enabled, Is.True);
        Assert.That(text.color.a, Is.GreaterThan(.01f));
        Bounds bounds = text.renderer.bounds;
        Vector3 min = camera.WorldToScreenPoint(bounds.min);
        Vector3 max = camera.WorldToScreenPoint(bounds.max);
        Assert.That(min.z, Is.GreaterThan(0f));
        Assert.That(min.x, Is.GreaterThan(0f));
        Assert.That(min.y, Is.GreaterThan(0f));
        Assert.That(max.x, Is.LessThan(Screen.width));
        Assert.That(max.y, Is.LessThan(Screen.height));
        float height = Mathf.Abs(max.y - min.y);
        Assert.That(height, Is.InRange(6f, Screen.height * .1f), "Digits must be legible and compact in the gameplay view.");
        Assert.That(Mathf.Abs(max.x - min.x), Is.LessThan(Screen.width * .1f));
        Assert.That(bounds.min.y, Is.GreaterThan(enemyPosition.y), "The popup must sit above the enemy body.");
        return height;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        DamagePopup.DebugDamageNumbersVisible = true;
        IEnumerator cleanup = CoreTestSupport.CleanupPlayMode();
        while (cleanup.MoveNext()) yield return cleanup.Current;
    }
}
#endif
