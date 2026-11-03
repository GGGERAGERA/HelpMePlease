#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEditor;
using Subject42.Combat.OrbitalStation;
using Object = UnityEngine.Object;

public sealed class TrailerCaptureSmokeTests
{
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest]
    public IEnumerator CapturePresetsRepeatHudEventBossAndReset()
    {
        CoreTestSupport.PreservePreferences();
        // Enter directly so Unity restores the user's open/dirty scenes on exit.
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        var character = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset");
        RunSelectionManager.Instance.SelectCharacter(character);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        GameplayRandom.Begin(12345);
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true } &&
            !SceneTransitionOverlay.IsTransitioning);
        var captureOwner = new GameObject("Trailer capture test session");
        var capture = captureOwner.AddComponent<TrailerCaptureSession>();
        var run = RunStateManager.Instance;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        int originalRings = station.Rings.Count;
        int originalModules = station.Modules.Count;
        var health = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponent<PlayerHealth>();
        float originalHealth = health.CurrentHealth;
        float originalThreat = run.ThreatValue;
        int gold = PlayerPrefs.GetInt("TOTAL_GOLD");
        var worldRules = WorldRuleController.Instance;
        var originalRule = worldRules.ActiveRule;
        var enemies = Object.FindFirstObjectByType<EnemySpawner>();
        float originalDensity = enemies.DebugCaptureDensity;
        Assert.That(capture.Config, Is.Not.Null);
        Assert.That(capture.Config.presets.Length, Is.EqualTo(5));
        capture.SetInvulnerable(true);
        Assert.That(health.TakeDamage(10, Vector2.right), Is.False);
        Assert.That(capture.Config.worldRules.Length, Is.EqualTo(7));
        foreach (var rule in capture.Config.worldRules)
        {
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Assert.That(capture.SetWorldRule(rule), Is.True, capture.Status);
                yield return null;
                Assert.That(worldRules.ActiveRule, Is.EqualTo(rule.RuleType == WorldRuleType.None ? null : rule));
            }
        }
        capture.SetSpawnDensity(.5f);
        int sparseCap = enemies.DebugCaptureMaxAlive;
        float sparseInterval = enemies.DebugCaptureRefillInterval;
        capture.SetSpawnDensity(2f);
        Assert.That(enemies.DebugCaptureMaxAlive, Is.GreaterThan(sparseCap).And.LessThanOrEqualTo(40));
        Assert.That(enemies.DebugCaptureRefillInterval, Is.LessThan(sparseInterval));
        capture.FillEnemies();
        yield return null;
        Assert.That(enemies.DebugCaptureAliveCount, Is.GreaterThan(0).And.LessThanOrEqualTo(enemies.DebugCaptureMaxAlive));
        capture.SetSpawnDensity(.5f);
        Assert.That(enemies.DebugCaptureAliveCount, Is.LessThanOrEqualTo(enemies.DebugCaptureMaxAlive));
        capture.SetSpawning(false);
        int stoppedCount = enemies.DebugCaptureAliveCount;
        capture.FillEnemies();
        Assert.That(enemies.DebugCaptureAliveCount, Is.EqualTo(stoppedCount));
        foreach (var shot in new[] { TrailerCaptureConfig.Shot.Beginning, TrailerCaptureConfig.Shot.Developed, TrailerCaptureConfig.Shot.Intense })
        {
            var preset = capture.Config.presets.Single(p => p.shot == shot);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Assert.That(capture.Apply(preset), Is.True, capture.Status);
                yield return null;
                Assert.That(station.Rings.Count, Is.EqualTo(preset.rings));
                Assert.That(station.Modules.Count, Is.EqualTo(preset.modules.Length + 1));
                Assert.That(station.ValidateState(out var error), Is.True, error);
                Assert.That(EnemyHealth.ActiveInstances.Count(e => e != null && !e.IsBoss), Is.LessThanOrEqualTo(preset.openingEnemies + 2));
            }
        }
        var hud = HUDManager.Instance;
        var pause = Object.FindFirstObjectByType<PauseMenuUI>();
        var pauseGraphics = pause.GetComponentsInChildren<Graphic>(true).Select(g => (g, g.enabled)).ToArray();
        capture.SetHudVisible(false);
        capture.SetNumbersVisible(false);
        Assert.That(hud.DebugHudVisible, Is.False);
        Assert.That(hud.IsTacticalMapVisible, Is.False);
        Assert.That(pauseGraphics.All(pair => pair.g.enabled == pair.enabled), Is.True, "Capture must preserve the pause menu graphics.");
        Assert.That(hud.GetComponentsInChildren<HudBar>(true).SelectMany(b => b.GetComponentsInChildren<Graphic>(true)).All(g => !g.enabled), Is.True);
        System.IO.Directory.CreateDirectory("Artifacts/GeneratedQA/TrailerCapture");
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/TrailerCapture/capture.png");
        yield return null;
        yield return null;
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(station.InputOwner.IsGameplayInputBlocked, Is.False);
        var rocket = capture.Config.presets.Single(p => p.shot == TrailerCaptureConfig.Shot.RocketStorm);
        var eventOwner = Object.FindFirstObjectByType<WorldEventSpawner>();
        Assert.That(eventOwner, Is.Not.Null, "Production event owner must exist before the shot.");
        Assert.That(capture.Apply(rocket), Is.True, capture.Status);
        Assert.That(eventOwner.isActiveAndEnabled, Is.True, "Shot must retain its production event owner.");
        Assert.That(eventOwner.ActiveEvent, Is.TypeOf<RocketStormEvent>());
        Assert.That(eventOwner.ActiveEvent.IsStarted, Is.True);
        var liveRocket = (RocketStormEvent)eventOwner.ActiveEvent;
        float deadline = Time.realtimeSinceStartup + 6f;
        while (liveRocket != null && liveRocket.IsRunning && liveRocket.WavesLaunched == 0 && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(liveRocket != null && liveRocket.IsRunning && liveRocket.WavesLaunched > 0, Is.True,
            $"Rocket wave: status={liveRocket?.Status}, scale={Time.timeScale}, ownerActive={eventOwner.isActiveAndEnabled}");
        Assert.That(capture.Apply(rocket), Is.True, capture.Status);
        yield return null;
        Assert.That(Object.FindObjectsByType<RocketStormEvent>(FindObjectsSortMode.None).Count(e => !e.IsCompleted), Is.EqualTo(1));
        var boss = capture.Config.presets.Single(p => p.shot == TrailerCaptureConfig.Shot.Boss);
        Assert.That(capture.Apply(boss), Is.True, capture.Status);
        yield return CoreTestSupport.Await(() => RunFlowController.Instance.Phase == RunPhase.FinalBossCombat);
        Assert.That(RunFlowController.Instance.FinalBoss.IsBoss, Is.True);
        Assert.That(capture.Apply(boss), Is.True, capture.Status);
        yield return CoreTestSupport.Await(() => RunFlowController.Instance.Phase == RunPhase.FinalBossCombat);
        Assert.That(EnemyHealth.ActiveInstances.Count(e => e != null && e.IsBoss), Is.EqualTo(1));
        Assert.That(capture.ResetSettings(), Is.True, capture.Status);
        yield return null;
        Assert.That(station.Rings.Count, Is.EqualTo(originalRings));
        Assert.That(station.Modules.Count, Is.EqualTo(originalModules));
        Assert.That(health.CurrentHealth, Is.EqualTo(originalHealth));
        Assert.That(run.ThreatValue, Is.EqualTo(originalThreat).Within(.2f));
        Assert.That(health.DebugInvulnerable, Is.False);
        Assert.That(hud.DebugHudVisible && DamagePopup.DebugDamageNumbersVisible, Is.True);
        Assert.That(hud.IsTacticalMapVisible, Is.True);
        Assert.That(eventOwner.ActiveEvent, Is.Null);
        Assert.That(RunFlowController.Instance.Phase, Is.EqualTo(RunPhase.NormalSector));
        Assert.That(run.IsDevelopmentRun, Is.True);
        Assert.That(worldRules.ActiveRule, Is.EqualTo(originalRule));
        Assert.That(enemies.DebugCaptureDensity, Is.EqualTo(originalDensity));
        capture.SetSpawnDensity(2f);
        Assert.That(capture.SetWorldRule(capture.Config.worldRules.Single(r => r.RuleType == WorldRuleType.Wind)), Is.True);
        run.EndRun(RunEndReason.ReturnedToBunker);
        Assert.That(PlayerPrefs.GetInt("TOTAL_GOLD"), Is.EqualTo(gold));
        Assert.That(capture.HasSettings, Is.False);
        Assert.That(enemies.DebugCaptureDensity, Is.EqualTo(originalDensity));
    }
}
#endif
