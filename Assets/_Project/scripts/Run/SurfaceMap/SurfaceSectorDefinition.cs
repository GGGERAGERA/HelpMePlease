using UnityEngine;

// Map illustrations only. These categories never modify a run or unlock a sector.
public enum SurfaceLocationFamily { Auto = 0, Ruins = 1, Technogenic = 2, Anomalous = 3, Weather = 4, AdvancedAnomalous = 5 }

[CreateAssetMenu(menuName = "Subject42/Surface/Sector")]
public sealed class SurfaceSectorDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Vector2 mapPosition;
    [Tooltip("Illustration family only. Weather rules automatically use the weather family; gameplay remains in parameters.")]
    [SerializeField] private SurfaceLocationFamily locationFamily;
    [Tooltip("Hide additional expedition intel until scanning or completing this sector.")]
    [SerializeField] private bool requiresScan;
    [Tooltip("Enable only for authored story progression sectors.")]
    [SerializeField] private bool storySector;
    [Header("Escape Protocol final trial")]
    [SerializeField] private bool escapeFinal;
    [SerializeField] private WorldEvent requiredFinalEvent;
    [SerializeField] private string requiredFinalEventTag;
    public bool IsEscapeFinal => escapeFinal;
    public bool FinalChallengeReady => !escapeFinal || requiredFinalEvent != null && requiredFinalEvent.AvailableInProduction &&
        requiredFinalEvent.AllowedInSite && requiredFinalEvent.TryValidateConfiguration(out _);
    [SerializeField] private string[] neighbours = System.Array.Empty<string>();
    [SerializeField] private RunConfigParameters parameters = new();
    [SerializeField] private SurfaceSectorContent[] content = System.Array.Empty<SurfaceSectorContent>();
    [Header("Permanent expedition rewards")]
    [SerializeField, Min(0)] private int victoryCores;
    [SerializeField, Min(0)] private int firstClearCores;
    [SerializeField] private ExpeditionTaskDefinition[] expeditionTasks = System.Array.Empty<ExpeditionTaskDefinition>();
    public System.Collections.Generic.IReadOnlyList<ExpeditionTaskDefinition> ExpeditionTasks => expeditionTasks;
    public int VictoryCores => Mathf.Max(0, victoryCores);
    public int FirstClearCores => Mathf.Max(0, firstClearCores);
    public System.Collections.Generic.IReadOnlyList<SurfaceSectorContent> Content => content;
    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;
    public Vector2 MapPosition => mapPosition;
    public SurfaceLocationFamily LocationFamily
    {
        get
        {
            if (IsWeatherRule(parameters.worldRule)) return SurfaceLocationFamily.Weather;
            if (locationFamily != SurfaceLocationFamily.Auto) return locationFamily;
            bool anomaly = parameters.localAnomaly != null ||
                (parameters.worldRule != null && parameters.worldRule.RuleType != WorldRuleType.None);
            return anomaly ? (parameters.initialThreat >= 20 ? SurfaceLocationFamily.AdvancedAnomalous : SurfaceLocationFamily.Anomalous)
                : SurfaceLocationFamily.Ruins;
        }
    }
    public static bool IsWeatherRule(WorldRuleData rule) => rule != null &&
        (rule.RuleType == WorldRuleType.Rain || rule.RuleType == WorldRuleType.Snow || rule.RuleType == WorldRuleType.Wind);
    public bool RequiresScan => requiresScan;
    public bool IsStorySector => storySector;
    public System.Collections.Generic.IReadOnlyList<string> Neighbours => neighbours;
    public RunConfig BuildRunConfig(string mapId, ExpeditionTaskDefinition task = null) =>
        new(mapId, id, parameters, VictoryCores, FirstClearCores, task, escapeFinal, requiredFinalEvent, requiredFinalEventTag);
}
