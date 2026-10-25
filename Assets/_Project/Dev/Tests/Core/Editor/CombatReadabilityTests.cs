#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class CombatReadabilityTests
{
    private static T Read<T>(object target, string field) => (T)target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, Type type, string field, object value) => type
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest]
    public IEnumerator ProductionComputeBufferSurvivesScenesAndReleasesOnPlayModeExit()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return ComputeBufferLifecycleSmoke.VerifyCurrentRun("Artifacts/GeneratedQA/ComputeBuffer/PlayMode");
        yield return new ExitPlayMode();
        VerifyBufferExitAudit();
    }

    private static void VerifyBufferExitAudit()
    {
        string audit = System.IO.File.ReadAllText("Artifacts/GeneratedQA/ComputeBuffer/PlayMode/shutdown-audit.txt");
        Assert.That(audit, Does.Contain("released=True"), audit);
    }

    [UnityTest]
    public IEnumerator EnemyFlashPreservesRenderedSpriteMaterialAndRendererTint()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return EnemyFlashTintSmoke.Verify("Artifacts/GeneratedQA/EnemyFeedback/ShaderPlayMode");
    }

    [UnityTest]
    public IEnumerator ProductionEnemyHitsDeathsAndReuseKeepCrowdReadable()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return EnemyFeedbackSmoke.VerifyCurrentRun("Artifacts/GeneratedQA/EnemyFeedback/PlayMode");
    }

    [UnityTest]
    public IEnumerator ProductionAnomalyRetryStopsAtApplicationShutdown()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return AnomalyLifecycleSmoke.VerifyCurrentRun("Artifacts/GeneratedQA/EnemyFeedback/LifecyclePlayMode");
    }

    [UnityTest]
    public IEnumerator ProductionEnvironmentAndEventPresentationStayQuietBetweenBeats()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return EnvironmentPaceSmoke.VerifyCurrentRun("Artifacts/GeneratedQA/EnvironmentPace/PlayMode");
    }

    [UnityTest]
    public IEnumerator ProductionOrbitalHierarchyKeepsColoredCannonsAboveTerritories()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return OrbitalHierarchySmoke.VerifyCurrentRun("Artifacts/GeneratedQA/OrbitalHierarchy/PlayMode");
    }

    [UnityTest]
    public IEnumerator PlayerFlashIsWarmAndRestoresWhilePausedOrDisabled()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CheckFlash();
    }

    private static IEnumerator CheckFlash()
    {
        var actor = new GameObject("Flash lifecycle check");
        actor.SetActive(false);
        var sprite = actor.AddComponent<SpriteRenderer>();
        var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/prefabs/Bunker/mat/BunkerElements2D.mat");
        var authoredFlash = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Shaders/M_WhiteFlash.mat");
        sprite.sharedMaterial = original;
        var flash = actor.AddComponent<PlayerWhiteFlash>();
        Set(flash, typeof(PlayerWhiteFlash), "flashMaterial", authoredFlash);
        actor.SetActive(true);
        flash.Flash();
        var tint = sprite.sharedMaterial.GetColor("_FlashColor");
        Assert.That(tint.r, Is.GreaterThan(tint.g * 1.5f), "Player damage must be distinguishable from the enemy white flash.");
        Assert.That(authoredFlash.GetColor("_FlashColor"), Is.EqualTo(Color.white), "Never recolor the shared enemy flash material.");
        Time.timeScale = 0f;
        yield return WaitRealtime(.3f);
        Assert.That(sprite.sharedMaterial, Is.SameAs(original), "A paused UI must not leave a permanent flash.");
        Time.timeScale = 1f;
        flash.Flash();
        actor.SetActive(false);
        Assert.That(sprite.sharedMaterial, Is.SameAs(original), "Disable must restore the material before reuse.");
        actor.SetActive(true);
        flash.Flash();
        yield return WaitRealtime(.3f);
        Assert.That(sprite.sharedMaterial, Is.SameAs(original));
        Object.Destroy(actor);
    }

    [UnityTest]
    public IEnumerator ProductionGameViewShowsPlayerDamageAndThreeCannonColors()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return CombatReadabilitySmoke.VerifyCurrentRun("Artifacts/GeneratedQA/CombatReadability/PlayMode");
    }

    [UnityTest]
    public IEnumerator AcceptedPlayerDamagePulsesExistingHudWithoutChangingHealthRules()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return CheckPlayerDamage();
    }

    private static IEnumerator CheckPlayerDamage()
    {
        var player = PrepareProduction();
        var health = player.GetComponent<PlayerHealth>();
        var hud = Object.FindFirstObjectByType<HUDManager>();
        var vignette = Read<CanvasGroup>(hud, "lowHpVignette");
        health.SetRuntimeHealth(100f, 100f);
        Assert.That(health.TakeDamage(15f, Vector2.right), Is.True);
        Assert.That(health.CurrentHealth, Is.EqualTo(85f));
        Assert.That(vignette.gameObject.activeInHierarchy, Is.True, "An alpha change on an inactive vignette cannot be seen.");
        Assert.That(vignette.alpha, Is.GreaterThan(.15f), "The first accepted hit must be visible even above the low-health threshold.");
        Assert.That(health.TakeDamage(15f, Vector2.right), Is.False, "Keep the existing invulnerability window.");
        Assert.That(health.CurrentHealth, Is.EqualTo(85f));
        yield return WaitRealtime(.4f);
        Assert.That(vignette.alpha, Is.LessThan(.01f), "The pulse must end instead of becoming a constant overlay.");
        health.Heal(5f);
        yield return null;
        Assert.That(vignette.alpha, Is.LessThan(.01f), "Healing must not be presented as damage.");
        yield return WaitGameTime(.7f);
        health.SetRuntimeHealth(100f, 20f);
        Assert.That(health.TakeDamage(5f, Vector2.left), Is.True);
        yield return WaitRealtime(.4f);
        Assert.That(health.CurrentHealth, Is.EqualTo(15f));
        float baseline = Mathf.Lerp(.15f, .55f, 1f - .15f / Read<float>(hud, "lowHpThreshold"));
        Assert.That(vignette.alpha, Is.EqualTo(baseline).Within(.02f), "Recover the authored low-health baseline after the hit pulse.");
        hud.HideLowHpVignette();
        yield return null;
        Assert.That(vignette.alpha, Is.Zero);
    }

    [UnityTest]
    public IEnumerator RoundCannonShotUsesExistingHaloWithoutAddingAContactHitbox()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return CheckCannonShot();
    }

    private static IEnumerator CheckCannonShot()
    {
        var player = PrepareProduction();
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        var module = station.Modules.OfType<OrbitalProjectileModule>().First();
        var enemy = Target(player.transform.position + Vector3.right * 5f);
        yield return null;
        var visual = (GameObject)typeof(OrbitalModuleRuntime).GetField("Visual", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(module);
        var view = visual.GetComponent<OrbitalModuleView>();
        module.SetHighlighted(false);
        Set(module, typeof(OrbitalModuleRuntime), "Cooldown", 0f);
        int shots = module.ShotsFired;
        module.ActivateCombat();
        module.Tick(0f);
        Assert.That(module.ShotsFired, Is.EqualTo(shots + 1));
        Assert.That(view.Halo.enabled, Is.True, "The existing non-colliding halo must emphasize the cannon firing.");
        Assert.That(view.Halo.color.a, Is.GreaterThan(.3f));
        Assert.That(CombatReadabilitySmoke.HaloDoesNotChangeContact(module, view.Halo), Is.True,
            "The shot glow must not enlarge the contact weapon.");
        yield return null;
        Assert.That(view.Halo.color.a, Is.GreaterThan(.3f), "The ordinary sprite tint pass must not erase the shot pulse.");
        module.SetHighlighted(true);
        yield return WaitGameTime(.3f);
        Assert.That(view.Halo.enabled, Is.True, "Shot recovery must keep an active selection halo.");
        module.SetHighlighted(false);
        Assert.That(view.Halo.enabled, Is.False);
        var presentation = typeof(OrbitalModuleRuntime).GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(module);
        presentation.GetType().GetMethod("SetPreviewState").Invoke(presentation, new object[] { true });
        module.Tick(0f);
        Assert.That(view.Halo.enabled, Is.True, "A valid reward preview must retain its existing halo across ticks.");
        presentation.GetType().GetMethod("SetPreviewState").Invoke(presentation, new object[] { false });
        module.Tick(0f);
        Assert.That(view.Halo.enabled, Is.False, "An invalid reward preview must retain its existing hidden halo.");
        Object.Destroy(enemy.gameObject);
    }

    [UnityTest]
    public IEnumerator OrbitalProjectileImpactRemainsReadableAndDamageStaysEight()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return BeginProductionRun();
        yield return CheckImpact();
    }

    private static IEnumerator CheckImpact()
    {
        var player = PrepareProduction();
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        foreach (var module in station.Modules) module.Detach();
        var adapter = (ProductionOrbitalCombatAdapter)station.Combat;
        adapter.DebugClearProjectiles();
        var enemy = Target(player.transform.position + Vector3.right * 4f);
        yield return null;
        enemy.SetRuntimeMaxHealth(1000f);
        bool hit = false;
        Vector2 point = default;
        adapter.ProjectileImpact += (p, _, _) => { hit = true; point = p; };
        adapter.SpawnWeaponProjectile(player.transform.position, enemy, Vector2.right,
            OrbitalPresentationConfig.Active.GetWeaponSettings(OrbitalModuleKind.Pistol), null, 8f);
        yield return CoreTestSupport.Await(() => hit);
        Assert.That(enemy.CurrentHealth, Is.EqualTo(992f), "Presentation must not add damage or alter collision resolution.");
        yield return WaitGameTime(.09f);
        var lines = station.GetComponentsInChildren<LineRenderer>()
            .Where(l => l.enabled && l.positionCount == 2 && Vector2.Distance(l.GetPosition(0), point) < .8f).ToArray();
        Assert.That(lines.Length, Is.GreaterThanOrEqualTo(3), "The impact must remain readable after the first few frames.");
        yield return WaitGameTime(.3f);
        Assert.That(lines.All(l => !l.enabled), Is.True, "Impact lines must return to the existing pool.");
        Object.Destroy(enemy.gameObject);
    }

    private static IEnumerator BeginProductionRun()
    {
        yield return CoreTestSupport.LoadBunker();
        var character = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset");
        RunSelectionManager.Instance.SelectCharacter(character);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        if (!GameplayRandom.IsActive) GameplayRandom.Begin(1709);
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MVP" &&
            Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true } && !SceneTransitionOverlay.IsTransitioning);
    }

    private static GameObject PrepareProduction()
    {
        Object.FindFirstObjectByType<RunFlowController>().StopRunGameplay();
        foreach (var enemy in EnemyHealth.ActiveInstances.ToArray()) Object.DestroyImmediate(enemy.gameObject);
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        var body = player.GetComponent<Rigidbody2D>();
        body.linearVelocity = Vector2.zero;
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        return player;
    }

    private static EnemyHealth Target(Vector3 position)
    {
        var target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Enemy_Shooter.prefab"), position, Quaternion.identity);
        foreach (var movement in target.GetComponents<EnemyMovement>()) movement.enabled = false;
        var body = target.GetComponent<Rigidbody2D>();
        if (body != null) body.constraints = RigidbodyConstraints2D.FreezeAll;
        target.GetComponent<EnemyHealth>().SetRuntimeMaxHealth(1000f);
        return target.GetComponent<EnemyHealth>();
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until) yield return null;
    }
    private static IEnumerator WaitGameTime(float seconds)
    {
        float until = Time.time + seconds;
        while (Time.time < until) yield return null;
    }
}
#endif
