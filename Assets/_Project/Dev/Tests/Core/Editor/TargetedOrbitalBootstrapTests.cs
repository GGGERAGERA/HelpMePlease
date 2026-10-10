#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class TargetedOrbitalBootstrapTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnitySetUp] public IEnumerator BeginRun() => CoreTestSupport.BeginRun();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest]
    public IEnumerator LinkHitsNearestEligibleTargetAcrossWholeSegmentWithExistingCooldown()
    {
        Assert.That(RunStateManager.Instance.IsDevelopmentRun, Is.False);
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        station.enabled = false;
        var ring = station.Rings[0];
        Assert.That(station.AddMount(ring.RingId, out _), Is.True);
        Assert.That(station.AddMount(ring.RingId, out _), Is.True);
        var free = ring.Mounts.Where(m => m.Module == null).ToArray();
        Assert.That(station.InstallLinkPair(ring.RingId, free[0].MountIndex,
            ring.RingId, free[1].MountIndex, out _), Is.True);
        var links = station.Modules.OfType<OrbitalLinkNodeModule>().ToArray();
        Vector2 origin = station.Owner.Transform.position;
        links[0].CurrentMount.Transform.position = origin + Vector2.left * 4f;
        links[1].CurrentMount.Transform.position = origin + Vector2.right * 4f;
        var decoy = new GameObject("Link midpoint outside damage width").AddComponent<EnemyHealth>();
        var near = new GameObject("Link nearest eligible").AddComponent<EnemyHealth>();
        var far = new GameObject("Link outer segment").AddComponent<EnemyHealth>();
        try
        {
            decoy.transform.position = origin + Vector2.up * .401f;
            near.transform.position = origin + new Vector2(1f, .2f);
            far.transform.position = origin + new Vector2(-3.5f, .2f);
            var update = typeof(OrbitalStationRuntime).GetMethod("UpdateLinkNodes", Private);
            var cooldown = typeof(OrbitalModuleRuntime).GetProperty("RuntimeCooldown", Private);
            update.Invoke(station, new object[] { .02f });
            Assert.That(near.CurrentHealth, Is.EqualTo(25f), "Off-segment midpoint target must not block a valid hit.");
            Assert.That(decoy.CurrentHealth, Is.EqualTo(30f));
            Assert.That(far.CurrentHealth, Is.EqualTo(30f), "Keep one nearest eligible target per attack.");
            Assert.That((float)cooldown.GetValue(links[0]), Is.EqualTo(.55f));
            update.Invoke(station, new object[] { .02f });
            Assert.That(near.CurrentHealth, Is.EqualTo(25f), "Existing cooldown must prevent a second attack.");
            near.transform.position = origin + Vector2.up * 4f;
            cooldown.SetValue(links[0], 0f);
            update.Invoke(station, new object[] { 0f });
            Assert.That(far.CurrentHealth, Is.EqualTo(30f), "Pause must prevent damage.");
            update.Invoke(station, new object[] { .02f });
            Assert.That(far.CurrentHealth, Is.EqualTo(25f), "Reach the segment beyond the old midpoint query radius.");
        }
        finally { Object.Destroy(decoy.gameObject); Object.Destroy(near.gameObject); Object.Destroy(far.gameObject); }
        yield return null;
    }

    [UnityTest]
    public IEnumerator MissingReleaseRunRecoversToBunkerWithoutDevelopmentBootstrapOrLoop()
    {
        var manager = RunStateManager.Instance;
        var modifiers = Object.FindFirstObjectByType<LevelModifiersApplier>();
        var character = Object.FindFirstObjectByType<CharacterSpawner>().DefaultCharacter;
        // Exercise the release policy with the actual authored direct-play assets.
        var policy = typeof(LevelModifiersApplier).GetMethod("PrepareDirectRun", Private,
            null, new[] { typeof(CharacterData), typeof(bool) }, null);
        Assert.That(policy, Is.Not.Null);
        Assert.That(policy.Invoke(modifiers, new object[] { character, false }), Is.EqualTo(true), "Prepared production run must be preserved.");
        int runId = manager.RunId;
        manager.ClearCurrentSector();
        Assert.That(policy.Invoke(modifiers, new object[] { character, false }), Is.EqualTo(false));
        Assert.That(manager.IsDevelopmentRun, Is.False, "Release must never create a development run.");
        Assert.That(manager.RunId, Is.EqualTo(runId), "Reject bootstrap before creating a new run.");
        Assert.That(manager.CurrentSector, Is.Null);
        // Start recovery while another transition owns the overlay, as happens on invalid scene activation.
        Assert.That(SceneTransitionOverlay.Load("MVP"), Is.True);
        var composition = ProductionSceneComposition.Active;
        typeof(ProductionSceneComposition).GetField("recoverMissingRun", Private).SetValue(composition, true);
        var recover = (IEnumerator)typeof(ProductionSceneComposition).GetMethod("Start", Private).Invoke(composition, null);
        Assert.That(recover.MoveNext(), Is.False);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MainMenu" &&
            ProductionSceneComposition.Active.IsReady && !SceneTransitionOverlay.IsTransitioning);
        yield return new WaitForSecondsRealtime(.5f);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        Assert.That(SceneTransitionOverlay.IsTransitioning, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(manager.IsDevelopmentRun, Is.False);
        Assert.That(SceneTransitionOverlay.Load("MVP"), Is.True);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            ProductionSceneComposition.Active is { IsReady: true } && !SceneTransitionOverlay.IsTransitioning);
        Assert.That(manager.IsDevelopmentRun, Is.True, "Editor direct MVP launch must remain available.");
    }
}
#endif
