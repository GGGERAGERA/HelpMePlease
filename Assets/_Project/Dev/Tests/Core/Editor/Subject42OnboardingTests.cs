#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
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

public sealed class Subject42OnboardingTests
{
    private static readonly string[] Keys = { RunMessageService.MovementHintPreferenceKey,
        RunMessageService.AutoAttackHintPreferenceKey, RunMessageService.ExperienceHintPreferenceKey,
        RunMessageService.SlowFieldHintPreferenceKey };
    private const string Backup = "Subject42.OnboardingTests.";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string placementPhase;
    private static int placementDiagnostics;

    [SetUp]
    public void PreservePreferences()
    {
        CoreTestSupport.PreservePreferences();
        foreach (var key in Keys)
        {
            SessionState.SetBool(Backup + key + ".exists", PlayerPrefs.HasKey(key));
            SessionState.SetInt(Backup + key, PlayerPrefs.GetInt(key));
            PlayerPrefs.DeleteKey(key);
        }
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        EndPlacementDiagnostics();
        var cleanup = CoreTestSupport.CleanupPlayMode();
        while (cleanup.MoveNext()) yield return cleanup.Current;
        foreach (var key in Keys)
        {
            if (SessionState.GetBool(Backup + key + ".exists", false))
                PlayerPrefs.SetInt(key, SessionState.GetInt(Backup + key, 0));
            else PlayerPrefs.DeleteKey(key);
            SessionState.EraseBool(Backup + key + ".exists");
            SessionState.EraseInt(Backup + key);
        }
        PlayerPrefs.Save();
    }

    [Test]
    public void Cycle2ReservedExitBlocksEveryRocketStormArenaInTutorialSite()
    {
        // cycle-2/editor.log:3754-4076; reconstruct the site from its legal center bounds.
        Rect playable = new(-50f, -50f, 100f, 100f);
        Rect site = Rect.MinMaxRect(-50f, -9.61182f, -10.35352f, 15.85400f);
        Vector2 size = new(24f, 20f);
        Vector2 minimum = site.min + size * .5f + Vector2.one;
        Vector2 maximum = site.max - size * .5f - Vector2.one;
        Vector2 exit = new(-27.12177f, -6.03996f);
        const float reservedExitRadius = 2.5f;
        float DistanceToExit(Rect footprint) => Vector2.Distance(exit,
            new Vector2(Mathf.Clamp(exit.x, footprint.xMin, footprint.xMax),
                Mathf.Clamp(exit.y, footprint.yMin, footprint.yMax)));

        Assert.That(minimum.x, Is.EqualTo(-37f).Within(.00001f));
        Assert.That(minimum.y, Is.EqualTo(1.38818f).Within(.00001f));
        Assert.That(maximum.x, Is.EqualTo(-23.35352f).Within(.00001f));
        Assert.That(maximum.y, Is.EqualTo(4.85400f).Within(.00001f));
        // The exit projects inside every arena horizontally. Even the furthest bottom
        // edge is less than 0.894m away, so no legal center can meet 3.5m clearance.
        Assert.That(exit.x, Is.InRange(maximum.x - size.x * .5f, minimum.x + size.x * .5f));
        Assert.That(Mathf.Max(0f, maximum.y - size.y * .5f - exit.y),
            Is.LessThan(reservedExitRadius + 1f));

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Environment/WorldEvents/RocketStormEvent.prefab");
        Assert.That(prefab, Is.Not.Null);
        GameObject instance = null;
        try
        {
            instance = Object.Instantiate(prefab);
            var storm = instance.GetComponent<RocketStormEvent>();
            storm.ConfigureSitePlacement(site.center, site.size);
            var siteStartBounds = typeof(WorldEvent).GetProperty("SiteStartBounds",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(siteStartBounds, Is.Not.Null);
            Assert.That(siteStartBounds.PropertyType, Is.EqualTo(typeof(Rect?)));
            var configuredBounds = (Rect?)siteStartBounds.GetValue(storm);
            Assert.That(configuredBounds.HasValue, Is.True);
            Rect configuredSite = configuredBounds.Value;
            Assert.That(Vector2.Distance(configuredSite.min, site.min), Is.LessThan(.00001f));
            Assert.That(Vector2.Distance(configuredSite.max, site.max), Is.LessThan(.00001f));
            var blocked = new WorldEventPlacementContext(site.center, playable, configuredSite, 42,
                footprint => DistanceToExit(footprint) >= reservedExitRadius + 1f);
            Assert.That(storm.TryPreparePlacement(blocked, out string rejected), Is.False);
            Assert.That(rejected, Is.Not.Null.And.Not.Empty);

            var clear = new WorldEventPlacementContext(site.center, playable, configuredSite, 42, null);
            Assert.That(storm.TryPreparePlacement(clear, out string error), Is.True, error);
            var footprints = new System.Collections.Generic.List<Rect>();
            storm.CollectReservedFootprints(footprints);
            Assert.That(footprints, Has.Count.EqualTo(1));
            Rect arena = footprints[0];
            Assert.That(arena.size, Is.EqualTo(size));
            Assert.That(arena.xMin, Is.GreaterThanOrEqualTo(site.xMin + 1f));
            Assert.That(arena.yMin, Is.GreaterThanOrEqualTo(site.yMin + 1f));
            Assert.That(arena.xMax, Is.LessThanOrEqualTo(site.xMax - 1f));
            Assert.That(arena.yMax, Is.LessThanOrEqualTo(site.yMax - 1f));
            Assert.That(DistanceToExit(arena), Is.LessThan(reservedExitRadius + 1f));
        }
        finally
        {
            if (instance != null) Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void OpeningReliefEndsWithoutWeakeningLaterThreat()
    {
        var config = AssetDatabase.LoadAssetAtPath<RunThreatConfig>("Assets/_Project/Data/World/RunThreatConfig.asset");
        Assert.That(config.IsOpening(0f, 0f), Is.True);
        Assert.That(config.IsOpening(9f, 54f), Is.True);
        Assert.That(config.IsOpening(9f, 55f), Is.False);
        Assert.That(config.IsOpening(25f, 10f), Is.False);
        Assert.That(config.MeleeSpeed(24.99f), Is.EqualTo(.87f).Within(.001f));
        Assert.That(config.MeleeSpeed(25f), Is.EqualTo(config.MeleeSpeed(24.99f)).Within(.001f));
        Assert.That(config.MeleeSpeed(37.5f), Is.InRange(.9f, .97f));
        Assert.That(config.MeleeSpeed(50f), Is.EqualTo(1f));
        Assert.That(config.MeleeResponseSeconds(50f), Is.Zero);
    }

    [UnityTest]
    public IEnumerator FieldAndFirstRunHintsSurviveReleaseExhaustionDisableAndReload()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        ProductionSceneCompositionAuthoring.EnsureScene(SceneManager.GetActiveScene());
        yield return new EnterPlayMode();
        BeginPlacementDiagnostics("first launch");
        // Allocate captured locals after the EnterPlayMode domain reload.
        yield return RunSmokeInPlayMode();
    }

    private static IEnumerator RunSmokeInPlayMode()
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset");
        var stage = AssetDatabase.LoadAssetAtPath<StageProfileData>("Assets/_Project/Data/Stages/StageProfiles/StageProfile_01.asset");
        var rule = AssetDatabase.LoadAssetAtPath<WorldRuleData>("Assets/_Project/Data/World/Rules/WorldRule_None.asset");
        var anomaly = AssetDatabase.LoadAssetAtPath<LocalAnomalyData>("Assets/_Project/Data/Anomalies/LocalAnomaly_Gravity.asset");
        RunStateManager.EnsureExists().BeginNewRun(character, null, stage, rule, anomaly, true);
        // First-run hints belong to the normal first sector, without the guided tutorial.
        PlayerPrefs.SetInt(TutorialController.CompletionKey, 0);
        placementPhase = "first launch";
        Debug.Log($"[OnboardingPlacement] Loading MVP: phase={placementPhase}");
        yield return SceneManager.LoadSceneAsync("MVP");
        yield return CoreTestSupport.Await(() => PlayerRuntimeReference.CachedPlayer != null && HUDManager.Instance.IsInformationVisible);
        var player = PlayerRuntimeReference.CachedPlayer;
        player.GetComponent<PlayerHealth>().AddMaxHealth(10000f);
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        Assert.That(station, Is.Not.Null, "Player station must be composed before the smoke");
        Assert.That(station.IsInitialized, Is.True);
        var messages = RunMessageService.Instance;
        Assert.That(messages, Is.Not.Null, "RunMessageService must be enabled with the HUD");
        var hint = HUDManager.Instance.GetComponentsInChildren<Transform>(true)
            .Single(t => t.name == "MovementHint").gameObject;
        var text = hint.GetComponentInChildren<TMP_Text>(true);
        Assert.That(hint, Is.Not.Null, "Authored onboarding panel is required");
        Assert.That(text, Is.Not.Null, "Authored onboarding text is required");
        yield return CoreTestSupport.Await(() => hint.activeSelf);

        Assert.That(text.text, Does.Contain(LocalizationService.Instance.Get("onboarding.movement")));
        var movement = player.GetComponent<CharacterMovement2D>();
        movement.MovementIntent = () => Vector2.right;
        yield return CoreTestSupport.Await(() => PlayerPrefs.GetInt(Keys[0]) == 1);
        movement.MovementIntent = () => Vector2.zero;

        var tick = typeof(OrbitalStationRuntime).GetMethod("TickRightMouse", Private);
        tick.Invoke(station, new object[] { .3f, true, true });
        Assert.That(station.SlowField.HasBeenUsed, Is.True);
        tick.Invoke(station, new object[] { .3f, false, false });
        station.BulletTime.Reset();

        // A real enemy lifecycle and XP acquisition trigger the remaining hints.
        var encounter = new GameObject("Onboarding encounter", typeof(EnemyHealth));
        yield return CoreTestSupport.Await(() => text.text.Contains(LocalizationService.Instance.Get("onboarding.autoAttack")));
        yield return CoreTestSupport.Await(() => PlayerPrefs.GetInt(Keys[1]) == 1);
        ExperienceManager.Instance.AddExperience(1);
        yield return CoreTestSupport.Await(() => text.text.Contains(LocalizationService.Instance.Get("onboarding.experience")));
        yield return CoreTestSupport.Await(() => PlayerPrefs.GetInt(Keys[2]) == 1);
        yield return CoreTestSupport.Await(() => text.text.Contains(LocalizationService.Instance.Get("onboarding.slowField")));
        tick.Invoke(station, new object[] { .1f, true, true });
        yield return CoreTestSupport.Await(() => PlayerPrefs.GetInt(Keys[3]) == 1);
        Object.Destroy(encounter);

        var threat = Object.FindFirstObjectByType<RunThreatController>();
        Assert.That(threat.DisplayedTier, Is.EqualTo(ThreatTier.Tier1));
        RunStateManager.Instance.AdvanceThreat(25f, 1f);
        yield return null;
        Assert.That(threat.DisplayedTier, Is.EqualTo(ThreatTier.Tier2));

        // Reload the gameplay scene in a fresh run, retaining saved onboarding flags.
        PlayerPrefs.SetInt(TutorialController.CompletionKey, 1);
        RunStateManager.Instance.BeginNewRun(character, null, stage, rule, anomaly, true);
        PlayerPrefs.SetInt(TutorialController.CompletionKey, 0);
        placementPhase = "reload";
        Debug.Log($"[OnboardingPlacement] Loading MVP: phase={placementPhase}");
        yield return SceneManager.LoadSceneAsync("MVP");
        yield return CoreTestSupport.Await(() => HUDManager.Instance.IsInformationVisible);
        messages = RunMessageService.Instance;
        hint = HUDManager.Instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == "MovementHint").gameObject;
        yield return new WaitForSeconds(6f);
        Assert.That(hint.activeSelf, Is.False, "Completed hints must not return on a fresh run");
        foreach (var key in Keys) Assert.That(PlayerPrefs.GetInt(key), Is.EqualTo(1));
    }

    internal static void BeginPlacementDiagnostics(string phase)
    {
        EndPlacementDiagnostics();
        placementDiagnostics = 0;
        SetPlacementPhase(phase);
        Application.logMessageReceived += CaptureRejectedPlacement;
    }

    internal static void EndPlacementDiagnostics()
    {
        Application.logMessageReceived -= CaptureRejectedPlacement;
    }

    internal static void SetPlacementPhase(string phase)
    {
        placementPhase = phase;
        Debug.Log($"[OnboardingPlacement] phase={placementPhase}");
    }

    private static void CaptureRejectedPlacement(string message, string stackTrace, LogType type)
    {
        if (type != LogType.Warning ||
            !message.StartsWith("[WorldEventSpawner] Placement rejected 'RocketStormEvent") ||
            placementDiagnostics >= 8) return;
        placementDiagnostics++;
        var spawner = Object.FindFirstObjectByType<WorldEventSpawner>();
        if (spawner == null) return;
        var reservedExit = (Vector2)typeof(WorldEventSpawner).GetField("reservedExit", Private).GetValue(spawner);
        var reservedRadius = (float)typeof(WorldEventSpawner).GetField("reservedExitRadius", Private).GetValue(spawner);
        foreach (var storm in Object.FindObjectsByType<RocketStormEvent>(FindObjectsSortMode.None).Take(8))
        {
            // The rejected instance is still unbound when the warning is emitted.
            if (storm.SourcePrefab != null || storm.IsCompleted) continue;
            var context = (WorldEventPlacementContext)typeof(WorldEventSpawner)
                .GetMethod("CreatePlacementContext", Private).Invoke(spawner, new object[] { storm });
            Rect available = context.PlayableArea;
            if (context.SiteStartBounds.HasValue)
            {
                Rect site = context.SiteStartBounds.Value;
                available = Rect.MinMaxRect(Mathf.Max(available.xMin, site.xMin),
                    Mathf.Max(available.yMin, site.yMin), Mathf.Min(available.xMax, site.xMax),
                    Mathf.Min(available.yMax, site.yMax));
            }
            Vector2 authoredSize = new SerializedObject(storm).FindProperty("settings.zoneSize").vector2Value;
            Vector2 size = Vector2.Min(authoredSize, available.size - Vector2.one * 2f);
            Debug.Log($"[OnboardingPlacement] REJECTED phase={placementPhase}; origin={context.Origin:F5}; " +
                $"PlayableArea={context.PlayableArea:F5}; SiteStartBounds={context.SiteStartBounds}; " +
                $"settings.zoneSize={authoredSize:F5}; arenaSize={size:F5}; " +
                $"legalCenterMin={available.min + size * .5f + Vector2.one:F5}; " +
                $"legalCenterMax={available.max - size * .5f - Vector2.one:F5}; " +
                $"reservedExit={reservedExit:F5}; reservedExitRadius={reservedRadius:F5}");
        }
        foreach (var exit in ProductionSectorExit.ActiveExits.Take(16))
        {
            if (exit == null || !exit.isActiveAndEnabled) continue;
            var trigger = exit.GetComponent<CircleCollider2D>();
            string path = exit.name;
            for (var parent = exit.transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            Debug.Log($"[OnboardingPlacement] phase={placementPhase}; exit={exit.gameObject.scene.name}/{path}; " +
                $"position={exit.transform.position:F5}; radius={trigger?.radius}; scale={exit.transform.lossyScale:F5}");
        }
    }
}
#endif
