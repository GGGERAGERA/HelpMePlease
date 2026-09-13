using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class AssaultEventsSmoke
{
    private readonly Subject42FinalBossFlowTests preferences = new();
    [SetUp] public void Preserve() => preferences.PreserveRewardsAndUnlockProgress();
    [UnityTearDown] public IEnumerator Cleanup() => preferences.CleanupPlayMode();

    [UnityTest]
    public IEnumerator ForceEachProductionEventOnce()
    {
        Directory.CreateDirectory("Artifacts/GeneratedQA/AssaultEvents");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return (IEnumerator)typeof(Subject42FinalBossFlowTests).GetMethod("StartRun",
            BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { "Gera" });
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        player.GetComponent<PlayerHealth>().SetIncomingDamageMultiplier(0f);
        // Keep the five Force calls isolated from autonomous events without stopping Threat.
        typeof(EnemySpawner).GetField("assaultEventsEnabled", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(spawner, false);
        string report = "Compile PASS; production MVP run.\n";
        foreach (AssaultEventType type in Enum.GetValues(typeof(AssaultEventType)))
        {
            float wait = Time.realtimeSinceStartup + 15f;
            while (!spawner.CanStartAssault && Time.realtimeSinceStartup < wait) yield return null;
            Assert.That(spawner.CanStartAssault, Is.True);
            var before = EnemyHealth.ActiveInstances.ToArray();
            Assert.That(spawner.ForceAssaultEvent(type), Is.True, type.ToString());
            var spawned = EnemyHealth.ActiveInstances.Except(before).ToArray();
            var limits = type switch
            {
                AssaultEventType.BomberRush => (8, 16),
                AssaultEventType.ShooterSquad => (6, 10),
                AssaultEventType.Encirclement => (12, 20),
                AssaultEventType.Crossfire => (6, 10),
                _ => (20, 30)
            };
            Assert.That(spawned.Length, Is.InRange(limits.Item1, limits.Item2));
            Assert.That(spawner.IsAssaultActive, Is.True);
            Vector3 center = player.transform.position;
            Vector3[] starts = spawned.Select(e => e.transform.position).ToArray();
            foreach (var enemy in spawned)
            {
                Assert.That(GameplayAreaService.Instance.IsInsideSpawnArea(enemy.transform.position), Is.True);
                Vector3 viewport = Camera.main.WorldToViewportPoint(enemy.transform.position);
                Assert.That(viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1, Is.True, "Offscreen spawn");
                Assert.That(enemy.GetComponent<EnemyMovement>(), Is.Not.Null);
            }
            if (type == AssaultEventType.Crossfire)
            {
                Assert.That(starts.Count(p => p.x < center.x), Is.EqualTo(spawned.Length / 2));
                Assert.That(starts.Count(p => p.x > center.x), Is.EqualTo(spawned.Length / 2));
            }
            if (type == AssaultEventType.Encirclement)
                Assert.That(starts.All(p => Mathf.Abs(Vector2.Distance(p, center) - Vector2.Distance(starts[0], center)) < .05f), Is.True);
            if (type == AssaultEventType.Stampede)
                Assert.That(spawned.All(e => e.GetComponent<EnemyChaseMovement>().HasAssaultDestination), Is.True);

            // Observe real AI, physics and pooled shooter/bomber initialization for each Force.
            float observeUntil = Time.time + 2f;
            while (Time.time < observeUntil) yield return null;
            Assert.That(spawned.Where(e => e != null).Any(e =>
                Vector2.Distance(e.transform.position, starts[Array.IndexOf(spawned, e)]) > .1f), Is.True, "Wave moves");
            Assert.That(CrowdSteeringRuntime.CurrentPreset, Is.EqualTo(CrowdMovementPreset.Production));
            report += $"PASS {type}: {spawned.Length}; offscreen, inside area, production AI moving, formation valid.\n";
            File.WriteAllText("Artifacts/GeneratedQA/AssaultEvents/checks.txt", report);
            foreach (var enemy in spawned) if (enemy != null) Object.Destroy(enemy.gameObject);
            yield return null;
            yield return null;
            Assert.That(spawner.IsAssaultActive, Is.False);
        }
        // Unity Test Runner automatically fails on errors/asserts/exceptions.
        // Production informational logs are expected during a real run.
    }
}
