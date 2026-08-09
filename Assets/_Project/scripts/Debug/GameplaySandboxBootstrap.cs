using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class GameplaySandboxBootstrap : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private CharacterData defaultCharacter;
    [SerializeField] private WeaponData defaultWeapon;
    [SerializeField] private WeaponData[] debugWeapons;

    [Header("Debug content")]
    [SerializeField] private UpgradeData[] upgrades;
    [SerializeField] private WorldRuleData[] worldRules;
    [SerializeField] private LocalAnomalyData[] localAnomalies;
    [SerializeField] private WorldEvent[] eventPrefabs;
    [SerializeField] private WorldEventRewardChest rewardChestPrefab;
    [SerializeField] private GameObject turretEnemyPrefab;
    [SerializeField] private GameObject eyesEnemyPrefab;
    [SerializeField] private GameObject[] massTestEnemyPrefabs;

    [Header("Existing World Rule presentation assets")]
    [SerializeField] private Material worldRuleOverlayMaterial;
    [SerializeField] private Material rainWorldMaterial;
    [SerializeField] private Material snowWorldMaterial;
    [SerializeField] private GameObject snowParticlePrefab;
    [SerializeField] private Shader condensationFogShader;
    [SerializeField] private Sprite darknessMarkerSprite;
    [SerializeField] private Material darknessMarkerMaterial;
    [SerializeField] private ParticleSystem goldenDeathFxPrefab;
    [SerializeField] private GoldenCoinPickup goldenCoinPrefab;

    [Header("Environment readability test")]
    [SerializeField] private GameObject readabilityEnvironmentPrefab;
    [SerializeField] private Shader environmentReadabilityShader;

    [Header("Area")]
    [SerializeField] private Vector2 areaSize = new(70f, 45f);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Awake()
    {
        GameplayAreaService gameplayArea = CreateGameplayArea();
        Camera sandboxCamera = CreateCamera();
        CreateEventSystem();
        CreateHud();

        GameObject systems = new("Sandbox Production Systems");
        UpgradeApplier applier = systems.AddComponent<UpgradeApplier>();
        UpgradeManager upgradeManager = systems.AddComponent<UpgradeManager>();
        upgradeManager.ConfigureDebugUpgradePool(upgrades, applier);
        systems.AddComponent<KillManager>();

        EnemySpawner enemySpawner = systems.AddComponent<EnemySpawner>();
        enemySpawner.StopSpawning();
        WorldRuleVisual ruleVisual = systems.AddComponent<WorldRuleVisual>();
        WorldRuleController ruleController =
            systems.AddComponent<WorldRuleController>();
        ConfigureWorldRulePresentation(ruleVisual, sandboxCamera);
        ruleController.ConfigureDebugVisual(ruleVisual);
        ruleController.ConfigureDebugGoldenAssets(
            goldenDeathFxPrefab,
            goldenCoinPrefab
        );
        LevelAnomalyController anomalyController =
            systems.AddComponent<LevelAnomalyController>();
        WorldEventSpawner eventSpawner =
            systems.AddComponent<WorldEventSpawner>();
        eventSpawner.ConfigureDebugEventPrefabs(eventPrefabs);
        eventSpawner.ConfigureDebugRewardChest(rewardChestPrefab);
        WorldEventDebugStatusOverlay eventStatusOverlay =
            systems.AddComponent<WorldEventDebugStatusOverlay>();
        eventStatusOverlay.Configure(eventSpawner);

        SectorVisualDebugController sectorVisual =
            systems.AddComponent<SectorVisualDebugController>();
        sectorVisual.Configure(gameplayArea, sandboxCamera);
        EnvironmentReadabilityDebugController readabilityController =
            systems.AddComponent<EnvironmentReadabilityDebugController>();
        try
        {
            readabilityController.Configure(
                readabilityEnvironmentPrefab,
                environmentReadabilityShader
            );
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                $"Environment Readability debug setup was skipped: {exception.Message}"
            );
        }

        CharacterSpawner characterSpawner =
            systems.AddComponent<CharacterSpawner>();
        characterSpawner.ConfigureDebugDefaults(
            defaultCharacter,
            defaultWeapon,
            applier
        );

        Subject42DebugMenu debugMenu =
            systems.AddComponent<Subject42DebugMenu>();
        debugMenu.ConfigureSandbox(
            ruleController,
            anomalyController,
            eventSpawner,
            enemySpawner,
            characterSpawner,
            upgradeManager,
            worldRules,
            localAnomalies,
            eventPrefabs,
            turretEnemyPrefab,
            eyesEnemyPrefab,
            debugWeapons,
            upgrades
        );

        ExitMassTestController massTest =
            systems.AddComponent<ExitMassTestController>();
        massTest.Configure(
            enemySpawner,
            gameplayArea,
            massTestEnemyPrefabs
        );

        AnomalyPowerDebugController powerController =
            systems.AddComponent<AnomalyPowerDebugController>();
        PowerTestController powerTest =
            systems.AddComponent<PowerTestController>();
        powerTest.Configure(
            enemySpawner,
            gameplayArea,
            massTestEnemyPrefabs,
            massTest
        );
        GravityAnomalySiteController gravitySite =
            systems.AddComponent<GravityAnomalySiteController>();
        gravitySite.Configure(
            enemySpawner,
            eventSpawner,
            anomalyController,
            powerController,
            powerTest,
            FindGravityAnomaly(),
            FindCaptureZoneEvent(),
            massTestEnemyPrefabs
        );
        GravityTrajectoryPreview trajectoryPreview =
            systems.AddComponent<GravityTrajectoryPreview>();
        trajectoryPreview.Configure(gravitySite);
        powerController.Configure(powerTest, trajectoryPreview);

        ElectricAnomalySiteController electricSite =
            systems.AddComponent<ElectricAnomalySiteController>();
        electricSite.Configure(
            enemySpawner,
            eventSpawner,
            powerController,
            powerTest,
            FindCaptureZoneEvent(),
            massTestEnemyPrefabs
        );

        BeamAnomalySiteController beamSite =
            systems.AddComponent<BeamAnomalySiteController>();
        beamSite.Configure(
            enemySpawner,
            eventSpawner,
            powerController,
            powerTest,
            massTestEnemyPrefabs
        );

        NormalAnomalySiteController normalSite =
            systems.AddComponent<NormalAnomalySiteController>();
        normalSite.Configure(
            enemySpawner,
            eventSpawner,
            anomalyController,
            powerTest,
            FindLocalAnomaly(LocalAnomalyType.Stasis),
            FindCaptureZoneEvent(),
            massTestEnemyPrefabs
        );
        NormalAnomalySiteController normalSite2 =
            systems.AddComponent<NormalAnomalySiteController>();
        normalSite2.Configure(
            enemySpawner,
            eventSpawner,
            anomalyController,
            powerTest,
            FindLocalAnomaly(LocalAnomalyType.Berserk),
            FindCaptureZoneEvent(),
            massTestEnemyPrefabs
        );
        NormalAnomalySiteController normalSite3 =
            systems.AddComponent<NormalAnomalySiteController>();
        normalSite3.Configure(
            enemySpawner,
            eventSpawner,
            anomalyController,
            powerTest,
            FindLocalAnomaly(LocalAnomalyType.Glitch),
            FindCaptureZoneEvent(),
            massTestEnemyPrefabs
        );

        AnomalySiteDebugSelector siteSelector =
            systems.AddComponent<AnomalySiteDebugSelector>();
        siteSelector.Configure(
            gravitySite,
            electricSite,
            beamSite,
            normalSite
        );

        ExplorationSectorController explorationSector =
            systems.AddComponent<ExplorationSectorController>();
        explorationSector.Configure(
            gameplayArea,
            enemySpawner,
            eventSpawner,
            powerController,
            powerTest,
            massTest,
            siteSelector,
            gravitySite,
            new[] { normalSite, normalSite2, normalSite3 },
            new[]
            {
                FindLocalAnomaly(LocalAnomalyType.Stasis),
                FindLocalAnomaly(LocalAnomalyType.Berserk),
                FindLocalAnomaly(LocalAnomalyType.Glitch)
            },
            massTestEnemyPrefabs,
            turretEnemyPrefab,
            eyesEnemyPrefab,
            sectorVisual
        );
        debugMenu.ConfigureSandboxLab(
            explorationSector,
            sectorVisual,
            powerController,
            trajectoryPreview,
            eventStatusOverlay,
            readabilityController
        );
        explorationSector.PrepareAsDefaultSandboxMode(characterSpawner);
    }

    private LocalAnomalyData FindGravityAnomaly()
    {
        if (localAnomalies == null)
            return null;

        for (int i = 0; i < localAnomalies.Length; i++)
        {
            LocalAnomalyData anomaly = localAnomalies[i];
            if (anomaly != null &&
                anomaly.AnomalyType == LocalAnomalyType.Gravity)
            {
                return anomaly;
            }
        }

        return null;
    }

    private LocalAnomalyData FindLocalAnomaly(LocalAnomalyType type)
    {
        if (localAnomalies == null)
            return null;
        for (int i = 0; i < localAnomalies.Length; i++)
        {
            LocalAnomalyData anomaly = localAnomalies[i];
            if (anomaly != null && anomaly.AnomalyType == type)
                return anomaly;
        }
        return null;
    }

    private CaptureZoneEvent FindCaptureZoneEvent()
    {
        if (eventPrefabs == null)
            return null;

        for (int i = 0; i < eventPrefabs.Length; i++)
        {
            if (eventPrefabs[i] is CaptureZoneEvent capture)
                return capture;
        }

        return null;
    }

    private GameplayAreaService CreateGameplayArea()
    {
        GameObject root = new("GameplayArea");
        BoxCollider2D playable = CreateAreaCollider(
            "PlayableArea",
            root.transform,
            areaSize
        );
        BoxCollider2D spawn = CreateAreaCollider(
            "SpawnArea",
            root.transform,
            areaSize - Vector2.one * 2f
        );
        GameplayAreaService service =
            root.AddComponent<GameplayAreaService>();
        service.ConfigureDebugAreas(playable, spawn);
        return service;
    }

    private static BoxCollider2D CreateAreaCollider(
        string objectName,
        Transform parent,
        Vector2 size)
    {
        GameObject areaObject = new(objectName);
        areaObject.transform.SetParent(parent, false);
        BoxCollider2D collider = areaObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(
            Mathf.Max(4f, size.x),
            Mathf.Max(4f, size.y)
        );
        return collider;
    }

    private static Camera CreateCamera()
    {
        GameObject cameraObject = new("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 12.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.02f, 0.025f, 0.035f, 1f);
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<CameraFollow>();
        return camera;
    }

    private void ConfigureWorldRulePresentation(
        WorldRuleVisual ruleVisual,
        Camera sandboxCamera)
    {
        GameObject canvasObject = new(
            "Sandbox World Rule Presentation",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler)
        );
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        Image screenImage = CreateFullscreenImage(
            "World Rule Overlay",
            canvasObject.transform
        );
        Image darknessImage = CreateFullscreenImage(
            "Darkness Overlay",
            canvasObject.transform
        );
        darknessImage.enabled = false;

        GameObject fogObject = new(
            "Condensation Fog Overlay",
            typeof(RectTransform),
            typeof(RawImage)
        );
        RectTransform fogRect = fogObject.GetComponent<RectTransform>();
        fogRect.SetParent(canvasObject.transform, false);
        Stretch(fogRect);
        CondensationFogOverlay condensation =
            fogObject.AddComponent<CondensationFogOverlay>();
        condensation.ConfigureDebugShader(condensationFogShader);

        ruleVisual.ConfigureDebugRuntime(
            screenImage,
            darknessImage,
            condensation,
            worldRuleOverlayMaterial,
            rainWorldMaterial,
            snowWorldMaterial,
            snowParticlePrefab,
            sandboxCamera,
            darknessMarkerSprite,
            darknessMarkerMaterial
        );
    }

    private static Image CreateFullscreenImage(string name, Transform parent)
    {
        GameObject imageObject = new(
            name,
            typeof(RectTransform),
            typeof(Image)
        );
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Stretch(rect);
        Image image = imageObject.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystemObject = new("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private static void CreateHud()
    {
        GameObject canvasObject = new(
            "Sandbox HUD",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<HUDManager>();
    }
#endif
}
