#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Scene-local controls for production world systems without the combat run.
/// This class only routes lab actions to production controllers and runtimes.
/// </summary>
[DefaultExecutionOrder(-250)]
public sealed class WorldSystemsLabController : MonoBehaviour
{
    public const string ScenePath =
        "Assets/_Project/Scenes/Dev/Labs/WorldSystemsLab.unity";

    [Header("Production scene objects")]
    [SerializeField] private Transform player;
    [SerializeField] private CameraFollow cameraRig;
    [SerializeField] private GameplayAreaService gameplayArea;
    [SerializeField] private WorldRuleController worldRules;
    [SerializeField] private LevelAnomalyController anomalies;
    [SerializeField] private WorldEventSpawner events;
    [SerializeField] private TacticalMapHUD tacticalMap;

    [Header("Production assets")]
    [SerializeField] private WorldRuleData[] worldRuleAssets;
    [SerializeField] private LocalAnomalyData[] normalAnomalyAssets;
    [SerializeField] private WorldEvent[] eventPrefabs;
    [SerializeField] private ExplorationSectorConfig explorationConfig;
    [SerializeField] private PropScatterProfile propScatterProfile;

    private readonly List<ProductionAnomalySite> spawnedSites = new();
    private ProductionPortalPair portalPair;
    private ProductionSectorProps props;
    private int nextNormalAnomaly;
    private int nextSpecialAnomaly;
    private Vector2 scroll;
    private GUIStyle titleStyle;
    private GUIStyle sectionStyle;
    private GUIStyle buttonStyle;
    private GUIStyle noteStyle;
    private string notice = "Ready";
    private bool panelVisible = true;

    public Transform Player => player;
    public WorldRuleController WorldRules => worldRules;
    public LevelAnomalyController Anomalies => anomalies;
    public WorldEventSpawner Events => events;
    public TacticalMapHUD TacticalMap => tacticalMap;
    public IReadOnlyList<WorldRuleData> WorldRuleAssets => worldRuleAssets;
    public IReadOnlyList<LocalAnomalyData> NormalAnomalyAssets =>
        normalAnomalyAssets;
    public IReadOnlyList<WorldEvent> EventPrefabs => eventPrefabs;
    public IReadOnlyList<ProductionAnomalySite> SpawnedSites => spawnedSites;
    public ProductionPortalPair PortalPair => portalPair;

    private void Awake()
    {
        Time.timeScale = 1f;
        events.ConfigureDebugEventPrefabs(eventPrefabs);
        events.ConfigureDebugRewardContainer(null);
        events.ConfigureDebugConcurrentEventCapacity(16);
        events.ConfigureSiteControlledMode(16);
        worldRules.ConfigureDebugGoldenAssets(null, null);
        tacticalMap?.BindPlayer(player);
        BuildProductionProps();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            panelVisible = !panelVisible;
    }

    public bool ApplyWorldRule(WorldRuleData rule)
    {
        if (worldRules == null)
            return false;

        worldRules.Apply(rule);
        notice = rule == null || rule.RuleType == WorldRuleType.None
            ? "World rule: None"
            : $"World rule: {rule.RuleType}";
        return true;
    }

    public bool SpawnNormalAnomaly()
    {
        if (!CanSpawnSite() || normalAnomalyAssets.Length == 0)
            return false;

        LocalAnomalyData data = normalAnomalyAssets[
            nextNormalAnomaly++ % normalAnomalyAssets.Length
        ];
        Vector2 position = NextSitePosition(false);
        GameObject root = new($"Lab Normal Anomaly - {data.AnomalyType}");
        ProductionAnomalySite site = root.AddComponent<ProductionAnomalySite>();
        bool initialized = site.InitializeNormal(
            position,
            new Vector2(13f, 10f),
            data,
            FirstSiteEvent(),
            events,
            anomalies,
            new Vector2(1000f, 1000f),
            0.5f
        );
        ClearBootstrapEvent(site);

        if (!initialized || site.AnomalyZone == null)
        {
            Destroy(root);
            return false;
        }

        spawnedSites.Add(site);
        notice = $"Normal anomaly: {data.AnomalyType}";
        return true;
    }

    public bool SpawnSpecialAnomaly()
    {
        if (!CanSpawnSite() || explorationConfig.SpecialPowerPool.Length == 0)
            return false;

        AnomalyPowerType power = explorationConfig.SpecialPowerPool[
            nextSpecialAnomaly++ % explorationConfig.SpecialPowerPool.Length
        ];
        Vector2 position = NextSitePosition(true);
        GameObject root = new($"Lab Special Anomaly - {power}");
        ProductionAnomalySite site = root.AddComponent<ProductionAnomalySite>();
        bool initialized = site.InitializeSpecial(
            position,
            new Vector2(15f, 11f),
            power,
            FirstSiteEvent(),
            events,
            anomalies,
            gameObject,
            explorationConfig,
            new Vector2(1000f, 1000f),
            0.5f
        );
        ClearBootstrapEvent(site);

        if (!initialized)
        {
            Destroy(root);
            return false;
        }

        spawnedSites.Add(site);
        notice = $"Special anomaly: {power}";
        return true;
    }

    public void ClearAnomalies()
    {
        for (int i = spawnedSites.Count - 1; i >= 0; i--)
        {
            if (spawnedSites[i] != null)
                spawnedSites[i].RemoveForLayout();
        }

        spawnedSites.Clear();
        anomalies?.BeginSiteLayout();
        notice = "Anomalies cleared";
    }

    public bool SpawnEvent(WorldEvent prefab)
    {
        if (events == null || prefab == null)
            return false;

        ClearEvents();
        Vector3 position = player != null
            ? player.position + new Vector3(7f, 0f, 0f)
            : new Vector3(7f, 0f, 0f);
        bool spawned = events.SpawnDebugEventAt(
            prefab,
            position,
            true,
            out WorldEvent instance
        );

        if (spawned)
        {
            notice = instance is CarrierHuntEvent
                ? "Carrier Hunt (preview only)"
                : $"Event: {instance.EventDisplayName}";
        }

        return spawned;
    }

    public void ClearEvents()
    {
        events?.ClearAllDebugEvents();
        notice = "Events cleared";
    }

    public bool SpawnPortalPair()
    {
        ClearPortals();
        GameObject root = new("Lab Production Portal Pair");
        portalPair = root.AddComponent<ProductionPortalPair>();
        portalPair.Initialize(new Vector2(-14f, -8f), new Vector2(14f, 8f));
        notice = "Portal pair spawned";
        return true;
    }

    public void ClearPortals()
    {
        if (portalPair != null)
        {
            portalPair.gameObject.SetActive(false);
            Destroy(portalPair.gameObject);
        }

        portalPair = null;
        notice = "Portals cleared";
    }

    public void ResetLab()
    {
        worldRules?.Clear();
        ClearEvents();
        ClearAnomalies();
        ClearPortals();
        nextNormalAnomaly = 0;
        nextSpecialAnomaly = 0;
        TeleportPlayerCenter();
        tacticalMap?.SetVisible(true);
        props?.Regenerate();
        notice = "Lab reset";
    }

    public void TeleportPlayerCenter()
    {
        if (player == null)
            return;

        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = Vector2.zero;
            body.linearVelocity = Vector2.zero;
        }

        player.position = new Vector3(0f, 0f, player.position.z);
        notice = "Player teleported to center";
    }

    public void ToggleMap()
    {
        if (tacticalMap == null)
            return;

        tacticalMap.SetVisible(!tacticalMap.IsVisible);
        notice = tacticalMap.IsVisible ? "Map shown" : "Map hidden";
    }

    private bool CanSpawnSite()
    {
        return events != null && anomalies != null &&
            explorationConfig != null && FirstSiteEvent() != null;
    }

    private WorldEvent FirstSiteEvent()
    {
        return eventPrefabs?.FirstOrDefault(prefab =>
            prefab != null && prefab.AllowedInSite);
    }

    private Vector2 NextSitePosition(bool special)
    {
        int index = spawnedSites.Count;
        float x = special ? 14f : -14f;
        float y = -11f + index % 3 * 11f;
        return new Vector2(x, y);
    }

    private void ClearBootstrapEvent(ProductionAnomalySite site)
    {
        WorldEvent bootstrap = site != null ? site.SiteEvent : null;
        if (bootstrap != null)
            events.ClearDebugEvent(bootstrap);
    }

    private void BuildProductionProps()
    {
        if (gameplayArea == null || propScatterProfile == null)
            return;

        props = new ProductionSectorProps(
            transform,
            gameplayArea,
            new Vector2[0],
            new Vector2(14f, 0f),
            new Vector2(1000f, 1000f),
            0f,
            propScatterProfile
        );
        props.Regenerate();
    }

    private void OnGUI()
    {
        if (!panelVisible)
        {
            if (GUI.Button(new Rect(8, 8, 190, 30), "F1  SHOW WORLD LAB"))
                panelVisible = true;
            return;
        }

        EnsureStyles();
        float width = Mathf.Min(360f, Screen.width * 0.36f);
        Rect panel = new(8f, 8f, width, Mathf.Max(200f, Screen.height - 16f));
        GUI.Box(panel, GUIContent.none);
        GUILayout.BeginArea(new Rect(
            panel.x + 10f,
            panel.y + 8f,
            panel.width - 20f,
            panel.height - 16f
        ));
        GUILayout.Label("WORLD SYSTEMS LAB", titleStyle);
        GUILayout.Label("Production systems · no combat runtime", noteStyle);
        scroll = GUILayout.BeginScrollView(scroll);

        Section("WORLD RULE");
        if (GUILayout.Button("None", buttonStyle))
            ApplyWorldRule(null);
        foreach (WorldRuleData rule in worldRuleAssets)
        {
            if (rule != null && rule.RuleType != WorldRuleType.None &&
                GUILayout.Button(rule.RuleType.ToString(), buttonStyle))
            {
                ApplyWorldRule(rule);
            }
        }

        Section("ANOMALY");
        if (GUILayout.Button("spawn normal zone", buttonStyle))
            SpawnNormalAnomaly();
        if (GUILayout.Button("spawn special zone", buttonStyle))
            SpawnSpecialAnomaly();
        if (GUILayout.Button("clear zones", buttonStyle))
            ClearAnomalies();

        Section("EVENT");
        foreach (WorldEvent prefab in eventPrefabs)
        {
            if (prefab == null)
                continue;

            string label = prefab is CarrierHuntEvent
                ? "Carrier Hunt (preview only)"
                : prefab.EventDisplayName;
            if (GUILayout.Button(label, buttonStyle))
                SpawnEvent(prefab);
        }
        if (GUILayout.Button("clear events", buttonStyle))
            ClearEvents();

        Section("PORTALS");
        if (GUILayout.Button("spawn pair", buttonStyle))
            SpawnPortalPair();
        if (GUILayout.Button("clear", buttonStyle))
            ClearPortals();

        Section("UTILITY");
        if (GUILayout.Button("reset scene", buttonStyle))
            ResetLab();
        if (GUILayout.Button("teleport player center", buttonStyle))
            TeleportPlayerCenter();
        GUI.enabled = tacticalMap != null;
        if (GUILayout.Button("show/hide map", buttonStyle))
            ToggleMap();
        GUI.enabled = true;

        GUILayout.Space(8f);
        GUILayout.Label(notice, noteStyle);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void Section(string label)
    {
        GUILayout.Space(8f);
        GUILayout.Label(label, sectionStyle);
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
            return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.3f, 0.95f, 1f) }
        };
        sectionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.78f, 0.28f) }
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleLeft,
            fixedHeight = 26f
        };
        noteStyle = new GUIStyle(GUI.skin.label)
        {
            wordWrap = true,
            normal = { textColor = new Color(0.8f, 0.86f, 0.9f) }
        };
    }
}
#endif
