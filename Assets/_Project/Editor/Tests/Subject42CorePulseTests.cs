#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class Subject42CorePulseTests
{
    private const string Output = "Artifacts/GeneratedQA/RewardProgression/Combat/";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) { Time.timeScale = 1f; yield return new ExitPlayMode(); }
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void WavesFollowRingOrderAndPauseAndReset(int level)
    {
        var root = new GameObject("Core sequence fixture");
        var config = OrbitalPresentationConfig.Active;
        var rings = new List<OrbitalRingRuntime>();
        try
        {
            for (int i = 0; i < 8; i++) rings.Add(new OrbitalRingRuntime(
                new OrbitalRingState { StableRingId = i + 1, Order = i, Radius = 1 + i },
                root.transform, config.VisualMaterial, config.PixelSprite));
            var core = new OrbitalCoreRuntime(new OrbitalCoreState { Level = level,
                DamageMultiplier = 9f, CooldownMultiplier = .01f });
            Assert.That(core.DamageMultiplier, Is.EqualTo(1f));
            Assert.That(core.CooldownMultiplier, Is.EqualTo(1f));
            int waves = 0, hits = 0;
            core.WaveStarted += (_, wave) => { waves++; Assert.That(wave, Is.EqualTo(waves)); };
            core.RingActivated += (ring, _, wave) => {
                Assert.That(ring.RingId, Is.EqualTo(hits % 8 + 1)); hits++;
            };
            float interval = OrbitalProgressionConfig.Default.GetCoreInterval(level);
            core.Tick(interval, rings);
            Assert.That(waves, Is.Zero);
            core.Tick(0f, rings);
            Assert.That(waves, Is.Zero);
            for (int i = 0; i < 400; i++) core.Tick(.01f, rings);
            Assert.That(waves, Is.EqualTo(level));
            Assert.That(hits, Is.EqualTo(level * 8));
            core.Reset(); core.Tick(0f, rings);
            Assert.That(core.CascadeActive, Is.False);
            Assert.That(core.Charge, Is.Zero);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [UnityTest]
    public IEnumerator ActualCombatDamageAndCaptures() => Run(false);

    [UnityTest]
    public IEnumerator VisualStages() => Run(true);

    private IEnumerator Run(bool visualsOnly)
    {
        Directory.CreateDirectory(Output);
        if (!visualsOnly) File.WriteAllText(Output + "damage.csv", "character,rings,modules,core,damage60s,dps,vsPrevious,steadyUs,pulseUs,steadyBytes,pulseBytes\n");
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        foreach (string name in new[] { "Gera", "Vika" })
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            float deadline = Time.realtimeSinceStartup + 30f;
            BunkerRunStarter starter;
            while ((starter = Object.FindFirstObjectByType<BunkerRunStarter>()) == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(starter, Is.Not.Null);
            var character = AssetDatabase.FindAssets("t:CharacterData").Select(id =>
                AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(id))).First(c => c.characterName == name);
            RunSelectionManager.Instance.SelectCharacter(character);
            starter.StartRun((Transform)typeof(BunkerRunStarter).GetField("cameraRig", Private).GetValue(starter));
            OrbitalStationRuntime station = null;
            deadline = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < deadline)
            {
                station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
                if (SceneManager.GetActiveScene().name == "MVP" && station != null && station.IsInitialized && !SceneTransitionOverlay.IsTransitioning) break;
                yield return null;
            }
            Assert.That(station != null && station.IsInitialized, Is.True);
            station.Owner.DamageOwner.GetComponent<PlayerHealth>()?.SetIncomingDamageMultiplier(0f);
            Time.timeScale = 0f;
            station.enabled = false;
            var targetRoot = new GameObject("Core QA stationary damage grid");
            // A fixed 2D grid covers the swept sword paths as well as ranged weapons.
            for (int x = -8; x <= 8; x++) for (int y = -5; y <= 5; y++)
            {
                var go = new GameObject("Dummy"); go.transform.SetParent(targetRoot.transform);
                go.transform.position = station.Owner.Transform.position + new Vector3(x, y, 0);
                go.AddComponent<EnemyHealth>().SetRuntimeMaxHealth(10000000f);
            }
            foreach (int count in visualsOnly ? new[] { 3, 8 } : new[] { 1, 3, 5, 8 })
            {
                double previous = 0;
                for (int level = 0; level <= 3; level++)
                {
                    if (visualsOnly && count == 8 && level != 3) continue;
                    var state = RunStateManager.Instance.DebugResetOrbitalRunState();
                    state.Modules.Clear();
                    while (state.Rings.Count < count) state.AddRing();
                    int moduleIndex = 0;
                    foreach (var ring in state.Rings)
                    {
                        while (ring.MountCount < ring.MountCapacity) state.AddMount(ring.StableRingId, out _);
                        if (count == 8)
                        {
                            Assert.That(state.UpgradeRingCapacity(ring.StableRingId), Is.True);
                            Assert.That(state.AddMount(ring.StableRingId, out _), Is.True);
                        }
                        for (int m = 0; m < ring.MountCount; m++)
                            Assert.That(state.InstallModule((OrbitalModuleKind)(moduleIndex++ % 4), ring.StableRingId, m, out _), Is.True);
                    }
                    for (int i = 0; i < level; i++) Assert.That(state.UpgradeCore(), Is.True);
                    Assert.That(station.RebuildRuntimeFromState(), Is.True);
                    station.enabled = false;
                    yield return null;
                    double damage = 0;
                    station.Combat.Hit += (_, amount) => damage += amount;
                    var tick = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), station,
                        typeof(OrbitalStationRuntime).GetMethod("TickStation", Private));
                    long steadyTicks = 0, pulseTicks = 0, steadyBytes = 0, pulseBytes = 0;
                    int steadyFrames = 0, pulseFrames = 0;
                    bool pulse = false;
                    station.Core.RingActivated += (_, __, ___) => pulse = true;
                    for (int frame = 0; frame < (visualsOnly ? 0 : 3600); frame++)
                    {
                        pulse = false;
                        long bytes = GC.GetAllocatedBytesForCurrentThread();
                        long start = System.Diagnostics.Stopwatch.GetTimestamp();
                        tick(1f / 60f);
                        station.GetComponent<OrbitalStationView>().CoreParticles.Simulate(1f / 60f, false, false, false);
                        long ticks = System.Diagnostics.Stopwatch.GetTimestamp() - start;
                        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
                        if (pulse) { pulseTicks += ticks; pulseBytes += bytes; pulseFrames++; }
                        else { steadyTicks += ticks; steadyBytes += bytes; steadyFrames++; }
                    }
                    double scale = 1000000d / System.Diagnostics.Stopwatch.Frequency;
                    string row = FormattableString.Invariant($"{name},{count},{moduleIndex},{level},{damage:F2},{damage / 60:F2},{(previous > 0 ? damage / previous : 1):F4},{steadyTicks * scale / Math.Max(1,steadyFrames):F2},{pulseTicks * scale / Math.Max(1,pulseFrames):F2},{steadyBytes},{pulseBytes}\n");
                    if (!visualsOnly)
                    {
                        File.AppendAllText(Output + "damage.csv", row);
                        Assert.That(damage, Is.GreaterThan(0));
                        if (level > 0) Assert.That(damage, Is.GreaterThan(previous));
                    }
                    previous = damage;
                    if (count == 3 || (count == 8 && level == 3))
                    {
                        station.GetComponent<OrbitalStationView>().CoreParticles.Play();
                        station.Core.Reset();
                        int lastWave = 0;
                        bool finished = level == 0;
                        string prefix = Path.GetFullPath(Output + $"{name}-{count}-Core{level}-");
                        Action<OrbitalRingRuntime, int, int> captureRing = (ring, _, wave) =>
                        {
                            string phase = wave == level && ring == station.Rings[count - 1] ? "final" :
                                wave == 1 && ring == station.Rings[0] ? "ring1" :
                                wave == 1 && ring == station.Rings[count / 2] ? "middle" : null;
                            lastWave = wave;
                            if (phase != null)
                            {
                                ScreenCapture.CaptureScreenshot(prefix + phase + ".png");
                                File.AppendAllText(Output + "captures.txt", $"{name} rings={count} core={level} {phase} actualWave={wave} ring={ring.State.Order + 1}\n");
                            }
                            if (wave == level && ring == station.Rings[count - 1]) finished = true;
                        };
                        station.Core.RingActivated += captureRing;
                        station.Core.Tick(OrbitalProgressionConfig.Default.GetCoreInterval(level), station.Rings);
                        station.enabled = true;
                        Time.timeScale = 1f;
                        bool chargeCaptured = false;
                        float captureDeadline = Time.realtimeSinceStartup + 15f;
                        do
                        {
                            if (!chargeCaptured && (level == 0 || station.Core.Charge > .4f))
                            {
                                chargeCaptured = true;
                                ScreenCapture.CaptureScreenshot(prefix + "charge.png");
                            }
                            yield return null;
                        } while (!finished && Time.realtimeSinceStartup < captureDeadline);
                        Assert.That(finished, Is.True, "Live cascade must reach its final ring");
                        Assert.That(lastWave, Is.EqualTo(level));
                        station.Core.RingActivated -= captureRing;
                        for (int settle = 0; settle < 3; settle++) yield return null;
                        Time.timeScale = 0f; station.enabled = false;
                    }
                    if (count == 8 && level == 3)
                    {
                        var cooldown = typeof(OrbitalModuleRuntime).GetField("Cooldown", Private);
                        foreach (var module in station.Modules)
                        {
                            var dummy = new GameObject("Pulse reaction target");
                            dummy.transform.position = module.WorldPosition;
                            var health = dummy.AddComponent<EnemyHealth>(); health.SetRuntimeMaxHealth(10000);
                            cooldown.SetValue(module, .2f);
                            float before = health.CurrentHealth;
                            module.OnCorePulse(); station.Combat.Tick(1f);
                            Assert.That(health.CurrentHealth, Is.LessThan(before), module.Kind.ToString());
                            Assert.That((float)cooldown.GetValue(module), Is.EqualTo(.2f));
                            Object.Destroy(dummy);
                            yield return null;
                        }
                        Vector3 original = station.Owner.Transform.position;
                        station.Owner.Transform.position += Vector3.right * 10000;
                        double noTargetDamage = damage;
                        foreach (var module in station.Modules) module.OnCorePulse();
                        station.Combat.Tick(1f);
                        Assert.That(damage, Is.EqualTo(noTargetDamage), "No target must not produce attacks");
                        station.Owner.Transform.position = original;
                    }
                    yield return null;
                }
            }
            Object.Destroy(targetRoot);
            Time.timeScale = 1f;
        }
        yield return new ExitPlayMode();
    }
}
#endif
