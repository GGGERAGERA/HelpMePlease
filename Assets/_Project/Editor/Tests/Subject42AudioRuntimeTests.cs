#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Subject42.Combat.OrbitalStation;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class Subject42AudioPassTelemetry
{
    const string Root = "Artifacts/AudioPass/";
    static AudioService service;
    static string label;
    public static readonly Dictionary<AudioCueId, int> Counts = new();
    static int maxPool, maxSources, maxPlaying;
    static double nextSample;
    static readonly List<string> logs = new();
    static Subject42AudioPassTelemetry()
    {
        EditorApplication.update += Observe;
        Application.logMessageReceived += Log;
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingPlayMode) Flush(); };
    }
    public static void Begin(string name)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(Root + "active.txt", name);
        Reset(name);
    }
    static void Reset(string name)
    {
        if (service != null) service.DebugCuePlayed -= Played;
        service = null; label = name; Counts.Clear(); logs.Clear(); maxPool = maxSources = maxPlaying = 0;
    }
    static void Played(AudioCueId cue)
    {
        Counts[cue] = Count(cue) + 1;
    }
    public static int Count(AudioCueId cue) => Counts.TryGetValue(cue, out int n) ? n : 0;
    static void Observe()
    {
        if (!Application.isPlaying || !File.Exists(Root + "active.txt")) return;
        if (label == null) Reset(File.ReadAllText(Root + "active.txt"));
        if (service != AudioService.Instance)
        {
            if (service != null) service.DebugCuePlayed -= Played;
            service = AudioService.Instance;
            if (service != null) service.DebugCuePlayed += Played;
        }
        if (EditorApplication.timeSinceStartup < nextSample) return;
        nextSample = EditorApplication.timeSinceStartup + .1;
        var all = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        maxSources = Math.Max(maxSources, all.Length);
        maxPlaying = Math.Max(maxPlaying, all.Count(s => s.isPlaying));
        if (service != null) maxPool = Math.Max(maxPool, service.GetComponentsInChildren<AudioSource>().Count(s => s.name.StartsWith("SFX ") && s.isPlaying));
    }
    static void Log(string message, string stack, LogType type)
    {
        if (label == null || !File.Exists(Root + "active.txt")) return;
        if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            logs.Add(type + ": " + message + "\n" + stack);
    }
    public static void Flush()
    {
        if (label == null) return;
        File.WriteAllLines(Root + label + "-telemetry.txt", new[] { $"maxAllSources={maxSources}; maxPlaying={maxPlaying}; maxPooledPlaying={maxPool}" }
            .Concat(Counts.OrderBy(p => (int)p.Key).Select(p => $"{p.Key}={p.Value}")));
        File.WriteAllLines(Root + label + "-warnings.txt", logs);
    }
    public static void End()
    {
        Flush();
        if (File.Exists(Root + "active.txt")) File.Delete(Root + "active.txt");
        if (service != null) service.DebugCuePlayed -= Played;
        service = null; label = null;
    }
}

public sealed class Subject42AudioRuntimeTests
{
    static void KnownVisual(string message, string stack, LogType type)
    {
        if (type == LogType.Assert && message.StartsWith("Setting the duration while system is still playing", StringComparison.Ordinal)
            && stack.Contains("WorldRuleVisual:EnsureWindResources")) LogAssert.Expect(type, message);
    }
    static void Call(object owner, string method, params object[] args) =>
        owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);

    [UnityTest, Timeout(7200000)]
    public IEnumerator GoldenPathAtFive()
    {
        Subject42AudioPassTelemetry.Begin("golden");
        return (IEnumerator)typeof(Subject42GoldenPathTests).GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { 1, 48151623, 5f });
    }

    [UnityTest, Timeout(300000)]
    public IEnumerator CombatSmokeAtOneValidated()
    {
        Subject42AudioPassTelemetry.Begin("smoke-1x");
        yield return new EnterPlayMode();
        // Allocate captured locals only after Unity's domain-reload yield.
        IEnumerator body = RunSmokeBody();
        while (body.MoveNext()) yield return body.Current;
        Subject42AudioPassTelemetry.Flush();
        yield return new ExitPlayMode();
        Subject42AudioPassTelemetry.End();
    }

    private static IEnumerator RunSmokeBody()
    {
        Application.runInBackground = true;
        Application.logMessageReceived += KnownVisual;
        yield return SceneManager.LoadSceneAsync("MainMenu");
        var character = AssetDatabase.FindAssets("t:CharacterData").Select(id => AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(id)))
            .Where(c => c != null && c.characterPrefab != null).OrderBy(c => c.name, StringComparer.Ordinal).First();
        RunSelectionManager.Instance.SelectCharacter(character);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        float deadline = Time.realtimeSinceStartup + 30;
        while ((SceneManager.GetActiveScene().name != "MVP" || SceneTransitionOverlay.IsTransitioning ||
            !Object.FindObjectsByType<OrbitalStationRuntime>(FindObjectsSortMode.None).Any(s => s != null && s.IsInitialized)) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MVP"));
        Time.timeScale = 1;
        Debug.Log("[AudioSmoke] locating initialized station");
        var station = Object.FindObjectsByType<OrbitalStationRuntime>(FindObjectsSortMode.None).FirstOrDefault(s => s != null && s.IsInitialized);
        File.WriteAllLines("Artifacts/AudioPass/smoke-startup.txt", Object.FindObjectsByType<CharacterSpawner>(FindObjectsSortMode.None)
            .Where(s => s != null).Select(s => $"{s.name}: player={s.SpawnedPlayer}, scene={s.gameObject.scene.name}, enabled={s.enabled}"));
        Assert.That(station, Is.Not.Null, "Production ORBITAL must finish startup");
        var player = station.Owner.Transform.gameObject;
        Assert.That(station.IsInitialized, Is.True);
        Object.FindFirstObjectByType<EnemySpawner>()?.StopSpawning();
        var health = player.GetComponent<PlayerHealth>();
        health.maxHealth = health.currentHealth = 10000; // fixture only; avoid unrelated combat death interrupting coverage
        station.AddRing(); station.AddRing();
        foreach (var ring in station.Rings)
            while (ring.Mounts.Count < 3 && station.AddMount(ring.RingId, out _)) { }
        var kinds = new[] { OrbitalModuleKind.Pistol, OrbitalModuleKind.LaserSword, OrbitalModuleKind.ImpulseGun, OrbitalModuleKind.ArcEmitter };
        int index = 0;
        foreach (var ring in station.Rings)
            foreach (var mount in ring.Mounts)
                if (station.State.IsMountFree(ring.RingId, mount.MountIndex))
                    station.InstallModule(kinds[index++ % kinds.Length], ring.RingId, mount.MountIndex, out _);
        foreach (var kind in kinds)
            Assert.That(station.Modules.Any(m => m.Kind == kind), Is.True, "Smoke fixture missing " + kind);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/p_Enemy.prefab");
        var enemies = new List<EnemyHealth>();
        for (int i = 0; i < 30; i++)
        {
            var go = Object.Instantiate(prefab, player.transform.position + new Vector3(Mathf.Cos(i), Mathf.Sin(i), 0) * (1 + i % 3), Quaternion.identity);
            var enemy = go.GetComponent<EnemyHealth>();
            enemy.maxHealth = 1000;
            enemies.Add(enemy);
        }
        yield return null;
        int[] sourceIds = AudioService.Instance.GetComponentsInChildren<AudioSource>().Select(s => s.GetInstanceID()).OrderBy(i => i).ToArray();
        foreach (var enemy in enemies) for (int hit = 0; hit < 8; hit++) enemy.TakeDamage(1, enemy.transform.position, hit == 7);
        health.TakeDamage(1, Vector2.right);
        Assert.That(Subject42AudioPassTelemetry.Count(AudioCueId.PlayerHurt), Is.GreaterThan(0));
        Assert.That(enemies.All(e => e.GetComponentsInChildren<AudioSource>().Length == 0), Is.True, "Crowd hits must not add per-enemy audio sources");
        CollectionAssert.AreEqual(sourceIds, AudioService.Instance.GetComponentsInChildren<AudioSource>().Select(s => s.GetInstanceID()).OrderBy(i => i).ToArray());

        // Real modules acquire real targets; move one durable target to the sword's contact range.
        float until = Time.realtimeSinceStartup + 6;
        while (Time.realtimeSinceStartup < until)
        {
            var sword = station.Modules.First(m => m.Kind == OrbitalModuleKind.LaserSword);
            if (enemies[0] != null) enemies[0].transform.position = sword.WorldPosition;
            yield return null;
        }
        foreach (var enemy in enemies.Where(e => e != null && !e.IsDead).Take(15)) enemy.TakeDamage(100000, enemy.transform.position);
        yield return new WaitForSecondsRealtime(.2f);
        var xpPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Pickups/p_Hex1.prefab");
        int xpBefore = Subject42AudioPassTelemetry.Count(AudioCueId.XPPickup);
        for (int i = 0; i < 15; i++)
        {
            var xp = Object.Instantiate(xpPrefab, player.transform.position, Quaternion.identity).GetComponent<ExperiencePickup>();
            Call(xp, "Collect");
        }
        Assert.That(Subject42AudioPassTelemetry.Count(AudioCueId.XPPickup) - xpBefore, Is.LessThanOrEqualTo(1));
        // End the crowd phase before deterministic reward/core/lifecycle checks:
        // stray drops must not open a second level-up and pause the core timer.
        foreach (var enemy in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)) enemy.gameObject.SetActive(false);
        foreach (var pickup in Object.FindObjectsByType<ExperiencePickup>(FindObjectsSortMode.None)) Object.Destroy(pickup.gameObject);
        // Resolve any actual level-up selection before the isolated flight check.
        deadline = Time.realtimeSinceStartup + 6;
        while (!UpgradeManager.Instance.IsRewardQueueIdle && Time.realtimeSinceStartup < deadline)
        {
            if (station.RewardFlow.State is OrbitalRewardFlowState.DirectMountSelection or OrbitalRewardFlowState.SecondLinkPlacement or OrbitalRewardFlowState.RingSelection or OrbitalRewardFlowState.ModuleSelection)
                station.RewardFlow.DebugChooseFirstValidTarget();
            else UpgradeManager.Instance.DebugSelectCurrentChoice(0);
            yield return null;
        }
        Time.timeScale = 1;
        // Force a single eligible reward through the real selection and coroutine flight entry points.
        station.AddRing();
        if (!station.Rings.Any(r => r.Mounts.Any(m => station.State.IsMountFree(r.RingId, m.MountIndex))))
            station.RemoveModule(station.Modules.Last().StableModuleId);
        Assert.That(UpgradeManager.Instance.DebugForceOrbitalReward(OrbitalRewardKind.Pistol), Is.True);
        int installs = Subject42AudioPassTelemetry.Count(AudioCueId.ModuleInstall);
        Assert.That(UpgradeManager.Instance.DebugSelectCurrentChoice(0), Is.True);
        Assert.That(Subject42AudioPassTelemetry.Count(AudioCueId.ModuleInstall), Is.EqualTo(installs));
        Assert.That(station.RewardFlow.DebugChooseFirstValidTarget(), Is.True);
        Assert.That(station.RewardFlow.State, Is.EqualTo(OrbitalRewardFlowState.ModuleFlight));
        yield return new WaitForSecondsRealtime(.5f);
        Assert.That(Subject42AudioPassTelemetry.Count(AudioCueId.ModuleInstall), Is.EqualTo(installs + 1));
        Time.timeScale = 1;

        // Keep production Update from overriding synthetic RMB intent; gameplay Tick stays active via reflection.
        station.enabled = false;
        station.enabled = true;
        Call(station, "UpdateCompression", .016f, true);
        Assert.That(AudioService.Instance.GetComponentsInChildren<AudioSource>().Any(s => s.isPlaying && s.loop && s.name.StartsWith("SFX ")), Is.True);
        int loops = Subject42AudioPassTelemetry.Count(AudioCueId.OrbitalCompress);
        for (int frame = 0; frame < 60; frame++) Call(station, "UpdateCompression", .016f, true);
        Assert.That(Subject42AudioPassTelemetry.Count(AudioCueId.OrbitalCompress), Is.EqualTo(loops), "Hold must not restart each frame");
        Call(station, "UpdateCompression", .016f, false);
        Assert.That(Subject42AudioPassTelemetry.Count(AudioCueId.OrbitalRelease), Is.GreaterThan(0));
        Call(station, "UpdateCompression", .016f, true);
        Time.timeScale = 0;
        yield return null; yield return null;
        AssertNoLoop();
        Time.timeScale = 1;
        station.Core.Reset();
        station.State.CoreState.Level = 1;
        typeof(OrbitalCoreRuntime).GetField("pulseTimer", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(station.Core, OrbitalProgressionConfig.Default.GetCoreInterval(1));
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.That(Time.timeScale, Is.EqualTo(1f), "Core phase must remain at real-time speed");
        Assert.That(station.Core.Level, Is.EqualTo(1));
        station.Core.Reset(); station.State.CoreState.Level = 3;
        typeof(OrbitalCoreRuntime).GetField("pulseTimer", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(station.Core, OrbitalProgressionConfig.Default.GetCoreInterval(3));
        yield return new WaitForSecondsRealtime(3f);
        foreach (var cue in new[] { AudioCueId.PistolShot, AudioCueId.EnemyHit, AudioCueId.CommonEnemyDeath, AudioCueId.PlayerHurt,
            AudioCueId.OrbitalSwordHit, AudioCueId.OrbitalImpulseFire, AudioCueId.OrbitalArcFire, AudioCueId.OrbitalCompress,
            AudioCueId.OrbitalRelease, AudioCueId.CorePulse, AudioCueId.CoreCascade, AudioCueId.ModuleInstall, AudioCueId.XPPickup, AudioCueId.LevelUp, AudioCueId.RewardSelect })
            Assert.That(Subject42AudioPassTelemetry.Count(cue), Is.GreaterThan(0), cue.ToString());
        Call(station, "UpdateCompression", .016f, false);
        Call(station, "UpdateCompression", .016f, true);
        RunFlowController.Instance.StopRunGameplay(); AssertNoLoop();
        typeof(RunFlowController).GetField("<Phase>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(RunFlowController.Instance, RunPhase.NormalSector);
        Call(station, "UpdateCompression", .016f, false);
        Call(station, "UpdateCompression", .016f, true);
        health.currentHealth = 1;
        yield return new WaitForSecondsRealtime(.7f);
        var deathLoop = AudioService.Instance.StartLoop(AudioCueId.OrbitalCompress, station.transform);
        Assert.That(deathLoop, Is.Not.Null);
        health.TakeDamage(100, Vector2.right);
        yield return null; AssertNoLoop();
        Assert.That(deathLoop.IsPlaying, Is.False);
        Time.timeScale = 1;
        var transitionLoop = AudioService.Instance.StartLoop(AudioCueId.OrbitalCompress, AudioService.Instance.transform);
        Assert.That(transitionLoop, Is.Not.Null);
        yield return SceneManager.LoadSceneAsync("MainMenu");
        AssertNoLoop();
        Assert.That(transitionLoop.IsPlaying, Is.False);
        Subject42AudioPassTelemetry.Flush();
    }
    static void AssertNoLoop() => Assert.That(AudioService.Instance.GetComponentsInChildren<AudioSource>()
        .Any(s => s.isPlaying && s.loop && s.name.StartsWith("SFX ")), Is.False);
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Application.logMessageReceived -= KnownVisual;
        Subject42AudioPassTelemetry.End();
        Time.timeScale = 1;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
