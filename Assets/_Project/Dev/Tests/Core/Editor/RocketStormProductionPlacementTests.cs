#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class RocketStormProductionPlacementTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private int startupSeed;
    // Keep failures individually reproducible without changing production marker sampling.
    public static readonly int[] StartupSeeds =
    {
        1, 2, 3, 4, 5, 7, 11, 17, 23, 31, 42, 53, 67, 83, 101, 127,
        211, 397, 7919, 104729, -1, -17, -7919, 20261026
    };

    [SetUp]
    public void PreservePreferences() => CoreTestSupport.PreservePreferences();

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Application.logMessageReceived -= CaptureRejectedPlacement;
        GameplayRandom.End();
        IEnumerator cleanup = CoreTestSupport.CleanupPlayMode();
        while (cleanup.MoveNext()) yield return cleanup.Current;
    }

    [UnityTest, Timeout(90000)]
    public IEnumerator SeededBunkerRunAdmitsEverySiteBeforeCompositionIsReady(
        [ValueSource(nameof(StartupSeeds))] int seed)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        Application.logMessageReceived += CaptureRejectedPlacement;
        yield return StartSeededRun(seed);
        AssertEverySiteAdmitted(seed);
    }

    [UnityTest, Timeout(600000)]
    public IEnumerator RemainingSeedsAndRepeatedSceneLifecycleAdmitEveryObjective()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        Application.logMessageReceived += CaptureRejectedPlacement;
        // The six individual startup cases already cover 1, 2, 3, 4, 42 and 104729.
        // Repeat 1 and 42 after the remaining seeds to catch stale scene ownership.
        foreach (int seed in new[] { 5, 7, 11, 17, 23, 31, 53, 67, 83, 101, 127,
            211, 397, 7919, -1, -17, -7919, 20261026, 1, 42 })
        {
            yield return StartSeededRun(seed);
            AssertEverySiteAdmitted(seed);
            if (seed == 5) yield return CaptureProductionRocket();

            var oldSites = ProductionAnomalySite.ActiveSites.ToArray();
            var oldEvents = Object.FindFirstObjectByType<WorldEventSpawner>().SpawnedEvents.ToArray();
            var run = RunStateManager.Instance;
            Assert.That(run.EndRun(RunEndReason.ReturnedToBunker, run.RunId), Is.Not.Null, $"seed={seed}");
            yield return null;
            Assert.That(oldSites.All(site => site == null), Is.True, $"seed={seed}: previous sites must be released");
            Assert.That(oldEvents.All(worldEvent => worldEvent == null), Is.True,
                $"seed={seed}: previous objectives must be released");
            Assert.That(ProductionAnomalySite.ActiveSites, Is.Empty, $"seed={seed}");
            GameplayRandom.End();
        }
    }

    [UnityTest, Timeout(90000)]
    public IEnumerator SpawnedExitKeepsTheReservedCircleClearance()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        var host = new GameObject("Exit admission fixture");
        host.SetActive(false);
        var spawner = host.AddComponent<WorldEventSpawner>();
        var storm = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RocketStormEvent>(
            "Assets/_Project/prefabs/Environment/WorldEvents/RocketStormEvent.prefab"));
        Vector2 exitPosition = new(10000f, 10000f);
        spawner.ReserveSectorExit(exitPosition, 2.5f);
        var context = PlacementContext(spawner, storm);
        Rect clearFootprint = new(exitPosition + new Vector2(4f, -1f), Vector2.one * 2f);
        Rect blockedFootprint = new(exitPosition + new Vector2(3f, -1f), Vector2.one * 2f);
        Assert.That(context.IsStaticFootprintClear(clearFootprint), Is.True,
            "A footprint 4m from a 2.5m circular exit preserves its 1m safety margin before spawn.");
        Assert.That(context.IsStaticFootprintClear(blockedFootprint), Is.False);

        var exit = new GameObject("Actual production circular exit").AddComponent<ProductionSectorExit>();
        exit.Initialize(exitPosition, 2.5f, null, null);
        Physics2D.SyncTransforms();
        var trigger = exit.GetComponent<CircleCollider2D>();
        Assert.That(context.IsStaticFootprintClear(clearFootprint), Is.True,
            $"The same 4m-clear footprint must stay valid after spawn: trigger.radius={trigger.radius:F5}, " +
            $"scale={trigger.transform.lossyScale:F5}, offset={trigger.offset:F5}, " +
            $"bounds.extents={trigger.bounds.extents:F5}, oldDiagonalClearance={trigger.bounds.extents.magnitude + 1f:F5}.");
        Assert.That(context.IsStaticFootprintClear(blockedFootprint), Is.False,
            "Spawning the exit must retain its actual circle and 1m clearance.");
    }

    private static IEnumerator CaptureProductionRocket()
    {
        var storm = Object.FindFirstObjectByType<WorldEventSpawner>().SpawnedEvents.OfType<RocketStormEvent>().First();
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        player.transform.position = storm.transform.position;
        var body = player.GetComponent<Rigidbody2D>();
        body.position = storm.transform.position;
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        yield return new WaitForSeconds(.3f);
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/RocketAndPistol-20261026/rocket-production-5-a.png");
        yield return null;
        yield return new WaitForSeconds(.2f);
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/RocketAndPistol-20261026/rocket-production-5-b.png");
        yield return null;
    }

    private IEnumerator StartSeededRun(int seed)
    {
        yield return CoreTestSupport.LoadBunker();
        yield return CoreTestSupport.Await(() => ProductionSceneComposition.Active is { IsReady: true } &&
            !SceneTransitionOverlay.IsTransitioning);

        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/01_Gera.asset"));
        GameplayRandom.End();
        GameplayRandom.Begin(seed);
        startupSeed = seed;
        Debug.Log($"[RocketStormPlacementTest] Production startup seed={seed}");
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            ProductionSceneComposition.Active is { IsReady: true } &&
            Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true } &&
            !SceneTransitionOverlay.IsTransitioning);
    }

    private static void AssertEverySiteAdmitted(int seed)
    {
        var sites = ProductionAnomalySite.ActiveSites.ToArray();
        Assert.That(sites, Has.Length.EqualTo(7), $"seed={seed}: no mandatory site may disappear");
        var spawner = Object.FindFirstObjectByType<WorldEventSpawner>();
        Assert.That(spawner.SpawnedEvents.Count, Is.EqualTo(7), $"seed={seed}");
        Assert.That(spawner.SpawnedEvents.OfType<RocketStormEvent>(), Is.Not.Empty, $"seed={seed}");
        foreach (var site in sites)
        {
            var siteEvent = (WorldEvent)typeof(ProductionAnomalySite)
                .GetField("activeEvent", PrivateInstance).GetValue(site);
            Assert.That(siteEvent, Is.Not.Null, $"seed={seed}: {site.name}");
            Assert.That(siteEvent.SourcePrefab, Is.Not.Null, $"seed={seed}: {site.name}");
            Assert.That(siteEvent.IsCompleted, Is.False, $"seed={seed}: {site.name}");
            if (siteEvent is not RocketStormEvent storm) continue;

            var footprints = new System.Collections.Generic.List<Rect>();
            storm.CollectReservedFootprints(footprints);
            Assert.That(footprints, Has.Count.EqualTo(1), $"seed={seed}");
            Assert.That(PlacementContext(spawner, storm).IsStaticFootprintClear(footprints[0]),
                Is.True, $"seed={seed}: full arena must preserve obstacles and the actual exit. " +
                DescribeFootprint(storm, footprints[0]));
        }
    }

    private static string DescribeFootprint(RocketStormEvent storm, Rect footprint)
    {
        var descriptions = new System.Collections.Generic.List<string> { $"arena={footprint:F5}" };
        foreach (var exit in ProductionSectorExit.ActiveExits)
        {
            var trigger = exit.GetComponent<CircleCollider2D>();
            Vector2 closest = new(Mathf.Clamp(exit.transform.position.x, footprint.xMin, footprint.xMax),
                Mathf.Clamp(exit.transform.position.y, footprint.yMin, footprint.yMax));
            descriptions.Add($"exit={exit.transform.position:F5}, radius={trigger.radius:F5}, " +
                $"scale={trigger.transform.lossyScale:F5}, offset={trigger.offset:F5}, " +
                $"distance={Vector2.Distance(exit.transform.position, closest):F5}, " +
                $"bounds.extents={trigger.bounds.extents:F5}, oldDiagonalClearance={trigger.bounds.extents.magnitude + 1f:F5}");
        }
        foreach (var collider in Physics2D.OverlapBoxAll(footprint.center, footprint.size, 0f))
            if (!collider.isTrigger && !collider.transform.IsChildOf(storm.transform))
                descriptions.Add($"overlap={collider.name}/{collider.GetType().Name}, body={collider.attachedRigidbody?.bodyType}, " +
                    $"player={collider.GetComponentInParent<PlayerHealth>() != null}, " +
                    $"enemy={collider.GetComponentInParent<EnemyHealth>() != null}");
        return string.Join("; ", descriptions);
    }

    private static WorldEventPlacementContext PlacementContext(WorldEventSpawner spawner, WorldEvent worldEvent) =>
        (WorldEventPlacementContext)typeof(WorldEventSpawner).GetMethod("CreatePlacementContext", PrivateInstance)
            .Invoke(spawner, new object[] { worldEvent });

    private void CaptureRejectedPlacement(string message, string stackTrace, LogType type)
    {
        if (!message.StartsWith("[WorldEventSpawner] Placement rejected 'RocketStormEvent")) return;
        var spawner = Object.FindFirstObjectByType<WorldEventSpawner>();
        Vector2 exit = (Vector2)typeof(WorldEventSpawner).GetField("reservedExit", PrivateInstance).GetValue(spawner);
        float exitRadius = (float)typeof(WorldEventSpawner).GetField("reservedExitRadius", PrivateInstance).GetValue(spawner);
        foreach (var storm in Object.FindObjectsByType<RocketStormEvent>(FindObjectsSortMode.None))
        {
            // A rejected instance has not yet been bound or disposed when the spawner emits its warning.
            if (storm.SourcePrefab != null || storm.IsCompleted) continue;
            var context = PlacementContext(spawner, storm);
            if (!context.SiteStartBounds.HasValue) continue;
            Rect site = context.SiteStartBounds.Value;
            Rect available = Rect.MinMaxRect(Mathf.Max(site.xMin, context.PlayableArea.xMin),
                Mathf.Max(site.yMin, context.PlayableArea.yMin),
                Mathf.Min(site.xMax, context.PlayableArea.xMax), Mathf.Min(site.yMax, context.PlayableArea.yMax));
            Vector2 authoredSize = new SerializedObject(storm).FindProperty("settings.zoneSize").vector2Value;
            Vector2 size = Vector2.Min(authoredSize, available.size - Vector2.one * 2f);
            Vector2 minimum = available.min + size * .5f + Vector2.one;
            Vector2 maximum = available.max - size * .5f - Vector2.one;
            bool exitBlocksEveryCenter = exitRadius > 0f;
            foreach (var center in new[] { minimum, maximum,
                new Vector2(minimum.x, maximum.y), new Vector2(maximum.x, minimum.y) })
            {
                Rect footprint = new(center - size * .5f, size);
                Vector2 closest = new(Mathf.Clamp(exit.x, footprint.xMin, footprint.xMax),
                    Mathf.Clamp(exit.y, footprint.yMin, footprint.yMax));
                exitBlocksEveryCenter &= Vector2.Distance(exit, closest) < exitRadius + 1f;
            }
            // For one circular exit, its forbidden center region is convex. Blocking all
            // four legal-center corners proves no center can admit this fixed-size arena.
            Debug.Log($"[RocketStormPlacementTest] REJECTED seed={startupSeed}; site={site:F5}; " +
                $"origin={context.Origin:F5}; arenaSize={size:F5}; exit={exit:F5}; exitRadius={exitRadius:F5}; " +
                $"legalCenterMin={minimum:F5}; legalCenterMax={maximum:F5}; exitBlocksEveryCenter={exitBlocksEveryCenter}");
        }
    }
}
#endif
