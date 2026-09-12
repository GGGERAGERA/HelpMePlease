#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class Subject42BotLabTests
{
    [UnityTearDown]
    public IEnumerator CleanupPlayMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
    [Test]
    public void BotIntentIsSampledForEveryPhysicsStep()
    {
        var owner = new GameObject("Physics intent test", typeof(Rigidbody2D));
        try
        {
            var movement = owner.AddComponent<CharacterMovement2D>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(CharacterMovement2D).GetMethod("Start", flags).Invoke(movement, null);
            movement.MovementIntent = () => Vector2.left;
            typeof(CharacterMovement2D).GetMethod("FixedUpdate", flags).Invoke(movement, null);
            Assert.That(typeof(CharacterMovement2D).GetField("moveInput", flags).GetValue(movement), Is.EqualTo(Vector2.left));
            movement.MovementIntent = () => Vector2.up;
            typeof(CharacterMovement2D).GetMethod("FixedUpdate", flags).Invoke(movement, null);
            Assert.That(typeof(CharacterMovement2D).GetField("moveInput", flags).GetValue(movement), Is.EqualTo(Vector2.up));
        }
        finally { Object.DestroyImmediate(owner); }
    }

    [Test]
    public void MovementHasAnOptionalIntentSource()
    {
        Assert.That(typeof(CharacterMovement2D).GetProperty("MovementIntent"), Is.Not.Null,
            "Bot must use the same movement pipeline and release it back to human input.");
    }

    [Test]
    public void WatchdogIgnoresPauseAndResetsOnProgress()
    {
        var watchdog = new BotProgressWatchdog();
        Assert.That(watchdog.Tick(29f, true, false), Is.False);
        Assert.That(watchdog.Tick(90f, false, false), Is.False);
        Assert.That(watchdog.Tick(1f, true, true), Is.False);
        Assert.That(watchdog.Tick(30f, true, false), Is.True);
    }

    [Test]
    public void ControllerReleasesOnlyItsOwnIntent()
    {
        var owner = new GameObject("Bot ownership test");
        try
        {
            var movement = owner.AddComponent<CharacterMovement2D>();
            using (var bot = new BotController(movement, null))
            {
                Assert.That(movement.MovementIntent, Is.Not.Null);
                Assert.Throws<InvalidOperationException>(() => new BotController(movement, null));
            }
            Assert.That(movement.MovementIntent, Is.Null);
            var second = new BotController(movement, null);
            Func<Vector2> replacement = () => Vector2.left;
            movement.MovementIntent = replacement;
            second.Dispose();
            Assert.That(movement.MovementIntent, Is.SameAs(replacement));
        }
        finally { Object.DestroyImmediate(owner); }
    }

    [Test]
    public void RejectedRewardClosingTheQueueDoesNotCountAsGranted()
    {
        var owner = new GameObject("Rejected reward test");
        var reward = ScriptableObject.CreateInstance<OrbitalRewardData>();
        float previousScale = Time.timeScale;
        try
        {
            var manager = owner.AddComponent<UpgradeManager>();
            manager.ConfigureDebugUpgradePool(Array.Empty<UpgradeData>(), owner.GetComponent<UpgradeApplier>());
            reward.RewardKind = (OrbitalRewardKind)int.MaxValue; // A stale, now ineligible card.
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(UpgradeManager).GetField("isChoosingUpgrade", flags).SetValue(manager, true);
            typeof(UpgradeManager).GetField("currentChoices", flags).SetValue(manager,
                new System.Collections.Generic.List<UpgradeData> { reward });
            int granted = 0;
            manager.DebugRewardCommitted += _ => granted++;
            Assert.That(manager.DebugSelectCurrentChoice(0), Is.True);
            Assert.That(manager.IsRewardQueueIdle, Is.True);
            Assert.That(granted, Is.Zero, "Rejecting a stale card must not count as a reward or progress");
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(reward);
            Time.timeScale = previousScale;
        }
    }

    [UnityTest]
    public IEnumerator RealSector_BotAndRewardLifecycle()
    {
        yield return new EnterPlayMode();
        yield return RunScenario();
        yield return new ExitPlayMode();
    }

    private static IEnumerator RunScenario()
    {
        // Start with the existing production bunker flow, as the real player does.
        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return Await(() => Object.FindFirstObjectByType<BunkerRunStarter>() != null, 20f);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        Assert.That(starter, Is.Not.Null);
        var character = AssetDatabase.FindAssets("t:CharacterData")
            .Select(id => AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(id)))
            .First(c => c != null && c.characterPrefab != null);
        RunSelectionManager.Instance.SelectCharacter(character);
        var camera = (Transform)typeof(BunkerRunStarter).GetField("cameraRig", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(starter);
        starter.StartRun(camera);
        yield return Await(() => SceneManager.GetActiveScene().name == "MVP" && !SceneTransitionOverlay.IsTransitioning, 30f);
        var spawner = Object.FindFirstObjectByType<CharacterSpawner>();
        var movement = spawner.SpawnedPlayer.GetComponent<CharacterMovement2D>();
        Assert.That(movement.MovementIntent, Is.Null, "Manual launch retains normal input");
        var session = BotRunSession.Ensure();
        session.BindScene(spawner, RunFlowController.Instance, UpgradeManager.Instance, GameplayAreaService.Instance);
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(Subject42DebugMenu).GetMethod("SetOpen", flags).Invoke(menu, new object[] { true });
        var tab = Enum.Parse(typeof(Subject42DebugMenu).GetNestedType("DebugTab", BindingFlags.NonPublic), "QA");
        typeof(Subject42DebugMenu).GetMethod("SelectTab", flags).Invoke(menu, new[] { tab, (object)true });
        typeof(Subject42DebugMenu).GetMethod("SelectQaSection", flags).Invoke(menu, new object[] { 2 });
        yield return null;
        yield return null;
        System.IO.Directory.CreateDirectory("Artifacts/GeneratedQA/BotLab");
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/BotLab/menu.png");
        var startButton = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
            .Single(b => b.GetComponentInChildren<TMPro.TextMeshProUGUI>()?.text == "START" && b.transform.parent.name == "Start Bot Run");
        Assert.That(startButton.interactable, Is.True);
        yield return null;
        startButton.onClick.Invoke();
        Assert.That(Subject42DebugMenu.IsDebugMenuOpen, Is.False);
        yield return Await(() => session.IsRunning, 35f);
        spawner = Object.FindFirstObjectByType<CharacterSpawner>();
        movement = spawner.SpawnedPlayer.GetComponent<CharacterMovement2D>();
        Vector3 start = movement.transform.position;
        yield return Await(() => Vector3.Distance(start, movement.transform.position) > 1f, 5f);
        Assert.That(movement.MovementIntent, Is.Not.Null);
        session.StopBot();
        Assert.That(session.Result.Result, Is.EqualTo("Aborted"));
        Assert.That(movement.MovementIntent, Is.Null);
        Assert.That(Time.timeScale, Is.GreaterThan(0f));

        Assert.That(session.BeginCurrentSector(), Is.True);
        var station = movement.GetComponentInChildren<OrbitalStationRuntime>();
        var upgrades = UpgradeManager.Instance;
        foreach (var kind in new[] { OrbitalRewardKind.AddMount, OrbitalRewardKind.Pistol, OrbitalRewardKind.RingSpeed })
        {
            Assert.That(upgrades.DebugForceOrbitalReward(kind), Is.True, kind.ToString());
            yield return Await(() => upgrades.IsRewardQueueIdle, 6f);
        }
        Assert.That(session.Result.RewardsTaken.Count, Is.EqualTo(3));
        Assert.That(station.State.Modules.Count, Is.EqualTo(2));
        Assert.That(station.State.Rings[0].SpeedUpgradeLevel, Is.EqualTo(1));
        // Two legal link endpoints, including an actual second-target flight.
        foreach (var kind in new[] { OrbitalRewardKind.AddMount, OrbitalRewardKind.NewRing, OrbitalRewardKind.LinkPair })
        {
            Assert.That(upgrades.DebugForceOrbitalReward(kind), Is.True, kind.ToString());
            yield return Await(() => upgrades.IsRewardQueueIdle, 6f);
        }
        Assert.That(station.State.Modules.Count(m => m.ModuleType == OrbitalModuleKind.LinkNode), Is.EqualTo(2));
        // Stop during reward returns pointer ownership, preserving the live production request.
        Assert.That(upgrades.DebugForceOrbitalReward(OrbitalRewardKind.RingPower), Is.True);
        session.StopBot();
        Assert.That(station.InputOwner.DebugSuppressPlayerInput, Is.False);
        Assert.That(upgrades.IsRewardQueueIdle, Is.False);
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(upgrades.DebugSelectCurrentChoice(0), Is.True);
        Assert.That(station.RewardFlow.DebugChooseFirstValidTarget(), Is.True);
        Assert.That(upgrades.IsRewardQueueIdle, Is.True);
        Assert.That(Time.timeScale, Is.GreaterThan(0f));

        // Death path, then a completely fresh unmodified natural run.
        Assert.That(session.BeginCurrentSector(), Is.True);
        var health = movement.GetComponent<PlayerHealth>();
        float deathDeadline = Time.realtimeSinceStartup + 3f;
        while (!health.IsDead && Time.realtimeSinceStartup < deathDeadline)
        {
            health.TakeDamage(100000f, Vector2.zero);
            yield return null;
        }
        Assert.That(health.IsDead, Is.True);
        yield return Await(() => !session.IsRunning, 3f);
        Assert.That(session.Result.Result, Is.EqualTo("PlayerDead"));
        Assert.That(session.Result.MinimumHPFraction, Is.Zero);
        Assert.That(session.StartBotRun(), Is.True);
        yield return Await(() => session.IsRunning, 35f);
        yield return Await(() => !session.IsRunning, 200f);
        Debug.Log("[Bot Lab QA] Natural outcome: " + session.Result.Report());
        Assert.That(session.Result.Result, Is.EqualTo("SectorCompleted"), "Natural run must physically reach exit");
        Assert.That(session.Result.Kills, Is.GreaterThan(0));
        Assert.That(session.Result.DamageDealt, Is.GreaterThan(0f));
        Assert.That(session.Result.XPCollected, Is.GreaterThan(0));
        Assert.That(session.Result.FinalOrbital, Is.Not.Null);
        Assert.That(System.IO.File.Exists(session.OutputPath), Is.True);
        Assert.That(RunStateManager.Instance.CurrentSector.SectorNumber, Is.EqualTo(1));

        Assert.That(session.StartBotRun(), Is.True);
        yield return Await(() => session.IsRunning, 35f);
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        movement = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponent<CharacterMovement2D>();
        movement.enabled = false;
        movement.GetComponent<Rigidbody2D>().simulated = false;
        // Accelerate only this intentionally immobilized test, not the natural balance run above.
        Time.timeScale = 10f;
        yield return Await(() => !session.IsRunning, 35f);
        Assert.That(session.Result.Result, Is.EqualTo("Stuck"));
        Assert.That(movement.MovementIntent, Is.Null);
        Assert.That(movement.GetComponentInChildren<OrbitalStationRuntime>().InputOwner.DebugSuppressPlayerInput, Is.False);
        Time.timeScale = 1f;
    }

    private static IEnumerator Await(Func<bool> condition, float seconds)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "Bot scenario timed out after " + seconds + " seconds");
    }
}
#endif
