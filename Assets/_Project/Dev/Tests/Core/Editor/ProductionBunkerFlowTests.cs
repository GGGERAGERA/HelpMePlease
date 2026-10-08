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
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class ProductionBunkerFlowTests
{
    private sealed class Storage : ISurfaceMapStorage { public string Load(string id) => ""; public void Save(string id,string json) { } }
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();
    private static T Read<T>(Object target,string name) => (T)target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
    private static BunkerStation Station(BunkerStationType kind) => Object.FindObjectsByType<BunkerStation>(FindObjectsSortMode.None).First(s => Read<BunkerStationType>(s,"stationType") == kind);

    [Test] public void ProductionScenesHaveExplicitCompositionAndListenerOwnership()
    {
        VerifyCatalogOwnership();
        var missing = new System.Collections.Generic.List<string>();
        foreach (string name in new[] { "StartScreen", "MainMenu", "MVP" })
        {
            var scene = EditorSceneManager.OpenScene($"Assets/_Project/Scenes/MainBuild/{name}.unity");
            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
            Assert.That(components.OfType<ProductionSceneComposition>().Count(), Is.EqualTo(1), name);
            Assert.That(components.OfType<AudioListener>().Count(value => value.enabled && value.gameObject.activeInHierarchy), Is.EqualTo(1), name);
            Assert.That(components.OfType<AudioSettingsService>(), Is.Empty, "Audio settings must belong to the persistent AudioService prefab.");
            Assert.That(scene.GetRootGameObjects().Any(root => root.name == "SYSTEM"), Is.True, name);
            AuditReferences(name, components);
        }
        foreach (string name in new[] { "GameplayRun", "GameplayPlayer", "GameplayRewards", "GameplayWorld", "GameplayEnemies",
            "CurrencyManager", "RunSelectionManager", "MetaProgressionManager", "ProductionSceneComposition" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/prefabs/Bootstrap/{name}.prefab");
            Assert.That(prefab, Is.Not.Null, name);
            AuditReferences(name, prefab.GetComponentsInChildren<Component>(true));
        }
        Assert.That(missing, Is.Empty, "Missing presentation references: " + string.Join("; ", missing));
        var gallery = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Bunker/EnemyGallery/EnemyGalleryTargetButton.prefab")
            .GetComponent<Subject42.Bunker.Gallery.EnemyGalleryController>();
        var entries = new SerializedObject(gallery).FindProperty("enemies");
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            foreach (string field in new[] { "previewPrefab", "warningPrefab", "explosionPrefab", "projectilePrefab", "shockwavePrefab" })
            {
                var reference = entry.FindPropertyRelative(field);
                if (reference == null || reference.objectReferenceValue == null) continue;
                var visual = (GameObject)reference.objectReferenceValue;
                Assert.That(visual.GetComponentsInChildren<MonoBehaviour>(true).Where(value => value.GetType().Name != "Light2D"),
                    Is.Empty, $"Gallery entry {i}/{field} must be authored presentation, without gameplay components.");
                Assert.That(visual.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                Assert.That(visual.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
                Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(visual.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            }
        }

        void AuditReferences(string name, Component[] components)
        {
            foreach (var component in components)
            {
                if (component == null) { Assert.Fail($"Missing script in {name}"); continue; }
                var property = new SerializedObject(component).GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                        if (property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                            missing.Add($"{name}/{component.name}/{property.propertyPath}");
            }
        }
    }

    private static void VerifyCatalogOwnership()
    {
        var catalog = Object.Instantiate(AssetDatabase.LoadAssetAtPath<BunkerSelectionCatalog>(
            "Assets/_Project/Data/SurfaceMap/BunkerSelectionCatalog.asset"));
        var host = new GameObject("catalog ownership probe");
        try
        {
            var hub = host.AddComponent<BunkerSelectionSourceHub>();
            typeof(BunkerSelectionSourceHub).GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hub, catalog);
            typeof(BunkerSelectionSourceHub).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(hub, null);
            Assert.That(hub.GetDefaultCharacter(), Is.Not.Null);
            catalog.characters = System.Array.Empty<CharacterData>();
            Assert.That(hub.GetDefaultCharacter(), Is.Null, "Station must read the catalog, not its stale runtime copy.");
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(catalog); }
    }
    [UnityTest] public IEnumerator DirectStartScreenComposition()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/StartScreen.unity");
        yield return new EnterPlayMode();
        yield return AwaitComposition(false);
    }

    [UnityTest] public IEnumerator DirectBunkerComposition()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
        yield return new EnterPlayMode();
        yield return AwaitComposition(true);
        yield return VerifyStationAndGalleryFlow();
    }

    private static IEnumerator VerifyStationAndGalleryFlow()
    {
        var panels = Object.FindFirstObjectByType<BunkerPanelManager>();
        var keyboard = PlayerRuntimeReference.CachedPlayer.GetComponent<PlayerInteractor>();
        Vector3 playerPosition = keyboard.transform.position;
        keyboard.transform.position = Station(BunkerStationType.CharacterSelection).transform.position;
        yield return null;
        Assert.That(keyboard.GetCurrentInteractable(), Is.Not.Null, "The keyboard path discovers the shared contract.");
        foreach (var kind in new[] { BunkerStationType.CharacterSelection, BunkerStationType.WeaponSelection, BunkerStationType.StartRun })
        {
            IBunkerInteractable station = Station(kind);
            Assert.That(station.CanInteract, Is.True);
            Assert.That(station.InteractionText, Is.Not.Empty);
            station.Interact();
            Assert.That(panels.IsAnyPanelOpen, Is.True, kind.ToString());
            yield return null;
            Assert.That(keyboard.GetCurrentInteractable(), Is.Null, "An open panel blocks keyboard interaction.");
            panels.CloseAll(false);
            yield return null;
        }
        keyboard.transform.position = playerPosition;
        IBunkerInteractable mission = Object.FindFirstObjectByType<MissionProvider>();
        mission.Interact();
        Assert.That(panels.IsAnyPanelOpen, Is.True, "Mission provider owns its panel navigation reference.");
        panels.CloseAll(false);
        var gallery = Object.FindFirstObjectByType<Subject42.Bunker.Gallery.EnemyGalleryController>();
        var preview = Read<Subject42.Bunker.Gallery.EnemyGalleryPreviewController>(gallery, "preview");
        var entries = Read<Subject42.Bunker.Gallery.EnemyGalleryController.Entry[]>(gallery, "enemies");
        for (int i = 0; i < entries.Length; i++)
        {
            gallery.GetType().GetField("currentIndex", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gallery, i);
            gallery.GetType().GetMethod("ShowCurrent", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(gallery, null);
            yield return CoreTestSupport.Await(() => preview.CompletedDemoCycles > 0);
            Assert.That(preview.CurrentPreview.GetComponentsInChildren<MonoBehaviour>(true).Where(value => value.GetType().Name != "Light2D"), Is.Empty);
            Assert.That(preview.CurrentPreview.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        }
        gallery.enabled = false;
        Assert.That(preview.CurrentPreview, Is.Null);
        gallery.enabled = true;
        yield return null;
        Assert.That(gallery.CurrentIndex, Is.Zero);
        Assert.That(preview.CurrentPreview, Is.Not.Null);
    }

    [UnityTest] public IEnumerator DirectGameplayComposition()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MVP.unity");
        yield return new EnterPlayMode();
        yield return AwaitComposition(true);
        Assert.That(Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None).Count(value => value.isActiveAndEnabled), Is.EqualTo(1));
    }

    [UnityTest] public IEnumerator ShortSceneCompositionTransitionFlow()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/StartScreen.unity");
        yield return new EnterPlayMode();
        yield return AwaitComposition(false);
        var audio = AudioService.Instance;
        Object.FindFirstObjectByType<StartScreenController>().Begin();
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MainMenu" && !SceneTransitionOverlay.IsTransitioning);
        yield return AwaitComposition(true);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" && !SceneTransitionOverlay.IsTransitioning);
        yield return AwaitComposition(true);
        RunEndService.Instance.ReturnToBunker();
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MainMenu" && !SceneTransitionOverlay.IsTransitioning);
        yield return AwaitComposition(true);
        Assert.That(AudioService.Instance, Is.SameAs(audio), "All transitions reuse the persistent audio owner.");
    }

    private static IEnumerator AwaitComposition(bool hasPlayer)
    {
        // Reflection keeps the ownership regression runnable before readiness is introduced.
        var ready = typeof(ProductionSceneComposition).GetProperty("IsReady");
        Assert.That(ready, Is.Not.Null, "Composition must publish readiness.");
        yield return CoreTestSupport.Await(() => Object.FindFirstObjectByType<ProductionSceneComposition>() is { } composition &&
            (bool)ready.GetValue(composition));
        var taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
        Assert.That(taggedPlayers.Length, Is.EqualTo(hasPlayer ? 1 : 0), string.Join("; ", taggedPlayers.Select(value =>
            value.name + " parent=" + value.transform.parent?.name + " movement=" + (value.GetComponent<CharacterMovement2D>()?.enabled.ToString() ?? "none"))));
        // Enemy gallery intentionally renders its model into a RenderTexture.
        // Only display cameras compete for the scene's gameplay view/listener.
        Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
            .Count(value => value.isActiveAndEnabled && value.targetTexture == null), Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Count(value => value.isActiveAndEnabled), Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(value => value.isActiveAndEnabled), Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<AudioService>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        Assert.That(Object.FindObjectsByType<AudioSettingsService>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        Assert.That(Object.FindObjectsByType<LocalizationService>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        Assert.That(Object.FindObjectsByType<UnlockProgressService>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        Assert.That(Object.FindObjectsByType<BunkerStationProgressionService>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        Assert.That(Object.FindObjectsByType<MetaProgressionManager>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        Assert.That(Object.FindObjectsByType<RunStateManager>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        Assert.That(Object.FindObjectsByType<RunSelectionManager>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        Assert.That(Object.FindObjectsByType<CurrencyManager>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        Assert.That(Object.FindObjectsByType<SceneTransitionOverlay>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
    }

    [UnityTest] public IEnumerator StartupStationsSurfaceRunRestartAbortDeathAndVictory()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/StartScreen.unity");
        yield return new EnterPlayMode();
        // Allocate captured locals after domain reload: the Test Runner cannot restore closure objects.
        yield return ExerciseProductionFlow();
    }
    private static IEnumerator ExerciseProductionFlow()
    {
        Object.FindFirstObjectByType<StartScreenController>().Begin();
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == RunEndService.BunkerSceneName && !SceneTransitionOverlay.IsTransitioning);
        var context = BunkerContext.Instance;
        Assert.That(context, Is.Not.Null); Assert.That(context.StationProgression, Is.SameAs(BunkerStationProgressionService.Instance));
        Assert.That(Object.FindObjectsByType<RunSelectionManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<MetaProgressionManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<BunkerStationProgressionService>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        UnityEngine.ScreenCapture.CaptureScreenshot("Artifacts/SurfaceMap/production-bunker.png");
        foreach (var kind in new[] { BunkerStationType.CharacterSelection, BunkerStationType.WeaponSelection, BunkerStationType.Upgrade, BunkerStationType.AnomalyStabilizer })
        {
            Station(kind).Interact(); Assert.That(Object.FindFirstObjectByType<SelectionPanelController>().IsOpen, Is.True, kind.ToString()); context.Panels.CloseAll(false);
        }
        var pause = Object.FindFirstObjectByType<PauseMenuUI>(); pause.Pause(); Assert.That(pause.IsPaused, Is.True); pause.Resume();
        var football = Object.FindFirstObjectByType<FootballMinigame>(); Assert.That(football, Is.Not.Null);
        Assert.That(Read<CameraFollow>(football,"cameraFollow"), Is.Not.Null);

        // Exercise the real controllers without writing map progression into the user's save.
        var map = AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
        var service = new SurfaceMapService(map,new Storage());
        typeof(MetaProgressionManager).GetProperty("SurfaceMap").SetValue(MetaProgressionManager.Instance,service);
        var exit = Station(BunkerStationType.StartRun); var target = Read<Transform>(exit,"runTransitionTarget");
        exit.Interact();
        var view = Object.FindFirstObjectByType<SurfaceMapView>(); Assert.That(view, Is.Not.Null);
        Assert.That(context.Panels.IsAnyPanelOpen, Is.True); Assert.That(service.TrySelect("C1"), Is.True);
        yield return null;
        UnityEngine.ScreenCapture.CaptureScreenshot("Artifacts/SurfaceMap/surface-map.png");
        yield return null;
        Read<Button>(view,"startButton").onClick.Invoke();
        yield return ReadyRun();
        var run = RunStateManager.Instance;
        Assert.That(run, Is.Not.Null, "RunState singleton lost after scene transition; instances=" + Object.FindObjectsByType<RunStateManager>(FindObjectsSortMode.None).Length);
        var config = run.CurrentConfig;
        Assert.That(config, Is.Not.Null, "RunConfig lost after scene transition");
        Assert.That(config.SectorId, Is.EqualTo("C1")); Assert.That(config.Gold, Is.EqualTo(1.5f)); Assert.That(run.ThreatValue, Is.GreaterThanOrEqualTo(15));
        var firstSector = run.CurrentSector;
        Assert.That(firstSector, Is.Not.Null); Assert.That(firstSector.StageProfile, Is.Not.Null);
        Assert.That(firstSector.SpawnPressureMultiplier, Is.EqualTo(firstSector.StageProfile.SpawnPressureMultiplier * config.SpawnPressure));
        var exploration = Object.FindFirstObjectByType<ProductionExplorationSectorController>();
        Assert.That(exploration, Is.Not.Null); Assert.That(exploration.Config, Is.SameAs(config.LayoutProfile));

        Assert.That(RunEndService.Instance, Is.Not.Null, "Gameplay RunEndService is missing");
        int previousId = run.RunId;
        RunEndService.Instance.RestartRun(RunEndReason.ReturnedToBunker);
        yield return CoreTestSupport.Await(() => run.RunId > previousId && !SceneTransitionOverlay.IsTransitioning && Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true });
        Assert.That(run.CurrentConfig, Is.SameAs(config)); Assert.That(service.GetStatus("C2"), Is.EqualTo(SurfaceSectorStatus.Locked));
        Assert.That(RunEndService.Instance, Is.Not.Null);
        RunEndService.Instance.ReturnToBunker(); yield return Bunker();
        Assert.That(service.GetStatus("C1"), Is.EqualTo(SurfaceSectorStatus.Available));

        // Start C again and use the actual death -> game-over -> bunker route.
        context = BunkerContext.Instance; exit = Station(BunkerStationType.StartRun); target = Read<Transform>(exit,"runTransitionTarget");
        context.RunStarter.StartSurfaceRun(target); yield return ReadyRun();
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        player.GetComponent<PlayerHealth>().TakeDamage(player.GetComponent<PlayerHealth>().MaxHealth * 2f,Vector2.zero);
        GameOverManager.Instance.MainMenu(); yield return Bunker();
        Assert.That(service.GetStatus("C2"), Is.EqualTo(SurfaceSectorStatus.Locked));

        // A Victory reason without final-boss confirmation must not advance the map.
        context = BunkerContext.Instance; exit = Station(BunkerStationType.StartRun); target = Read<Transform>(exit,"runTransitionTarget");
        context.RunStarter.StartSurfaceRun(target); yield return ReadyRun();
        run.EndRun(RunEndReason.Victory,run.RunId); Assert.That(service.GetStatus("C2"), Is.EqualTo(SurfaceSectorStatus.Locked));
        RunEndService.RecoverToBunker(); yield return Bunker();

        // Skip route travel only: the existing final boss flow still spawns and confirms a real boss death.
        context = BunkerContext.Instance; exit = Station(BunkerStationType.StartRun); target = Read<Transform>(exit,"runTransitionTarget");
        context.RunStarter.StartSurfaceRun(target); yield return ReadyRun();
        var finalProfile = AssetDatabase.LoadAssetAtPath<StageProfileData>("Assets/_Project/Data/Stages/StageProfiles/StageProfile_10.asset");
        run.SetCurrentSector(new RunSector(RunRoute.FinalSector,finalProfile,config.WorldRule,config.LocalAnomaly));
        var flow = RunFlowController.Instance; flow.InitializeSector(finalProfile);
        typeof(RunFlowController).GetProperty("IsExitUnlocked").SetValue(flow,true);
        Assert.That(flow.HandleExitReached(), Is.True);
        yield return CoreTestSupport.Await(() => flow.FinalBoss != null && flow.Phase == RunPhase.FinalBossCombat);
        flow.FinalBoss.TakeDamage(flow.FinalBoss.CurrentHealth * 2f,flow.FinalBoss.transform.position);
        yield return Bunker();
        Assert.That(service.GetStatus("C1"), Is.EqualTo(SurfaceSectorStatus.Completed));
        Assert.That(service.GetStatus("C2"), Is.EqualTo(SurfaceSectorStatus.Available));
        Assert.That(run.CurrentConfig, Is.SameAs(RunConfig.Default));
        Assert.That(Object.FindObjectsByType<MetaProgressionManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
    }
    private static IEnumerator ReadyRun()
    {
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" && !SceneTransitionOverlay.IsTransitioning && Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true });
        var spawner = Object.FindFirstObjectByType<CharacterSpawner>();
        Assert.That(spawner, Is.Not.Null); Assert.That(spawner.SpawnedPlayer, Is.Not.Null);
        var health = spawner.SpawnedPlayer.GetComponent<PlayerHealth>(); Assert.That(health, Is.Not.Null);
        health.AddMaxHealth(1000000f);
    }
    private static IEnumerator Bunker() => CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == RunEndService.BunkerSceneName && !SceneTransitionOverlay.IsTransitioning);
}
#endif
