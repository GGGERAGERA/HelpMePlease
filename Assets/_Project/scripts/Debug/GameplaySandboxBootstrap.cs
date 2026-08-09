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
    [SerializeField] private GameObject turretEnemyPrefab;
    [SerializeField] private GameObject eyesEnemyPrefab;
    [SerializeField] private GameObject[] massTestEnemyPrefabs;

    [Header("Area")]
    [SerializeField] private Vector2 areaSize = new(32f, 20f);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Awake()
    {
        GameplayAreaService gameplayArea = CreateGameplayArea();
        CreateCamera();
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
        ruleController.ConfigureDebugVisual(ruleVisual);
        LevelAnomalyController anomalyController =
            systems.AddComponent<LevelAnomalyController>();
        WorldEventSpawner eventSpawner =
            systems.AddComponent<WorldEventSpawner>();
        eventSpawner.ConfigureDebugEventPrefabs(eventPrefabs);

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
        powerController.Configure(powerTest);

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

        AnomalySiteDebugSelector siteSelector =
            systems.AddComponent<AnomalySiteDebugSelector>();
        siteSelector.Configure(gravitySite, electricSite, beamSite);
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

    private static void CreateCamera()
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
