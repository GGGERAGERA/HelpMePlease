using UnityEngine;

[CreateAssetMenu(
    fileName = "ExplorationSectorConfig",
    menuName = "Game/Run/Exploration Sector Config"
)]
public sealed class ExplorationSectorConfig : ScriptableObject
{
    [Header("Normal Sites")]
    [SerializeField] private LocalAnomalyData[] normalAnomalies;

    [Header("Special Sites")]
    [SerializeField] private LocalAnomalyData gravityAnomaly;
    [SerializeField] private AnomalyPowerType[] specialPowerPool =
    {
        AnomalyPowerType.GravityOrb,
        AnomalyPowerType.ArcNode,
        AnomalyPowerType.RedBeam
    };

    [Header("Optional Special Site Art Hooks")]
    [SerializeField] private AnomalyArtHookSet electricArtHooks;
    [SerializeField] private AnomalyArtHookSet beamArtHooks;

    [Header("Layout")]
    [SerializeField, Range(0.85f, 0.95f)]
    private float targetAnomalyCoverage = 0.89f;
    [SerializeField, Min(0f)] private float edgePadding = 0.1f;
    [SerializeField, Min(0.5f)] private float exitRadius = 2.5f;

    [Header("Special Environmental Damage")]
    [SerializeField, Min(0f)] private float electricEnemyDamage = 120f;
    [SerializeField, Min(0f)] private float electricPlayerDamage = 18f;
    [SerializeField, Min(0f)] private float beamEnemyDamage = 180f;
    [SerializeField, Min(0f)] private float beamPlayerDamage = 30f;

    [Header("Run Threat")]
    [SerializeField] private RunThreatConfig threatConfig;

    [Header("World Breakables")]
    [SerializeField] private WorldBreakable breakablePrefab;
    [SerializeField, Min(0)] private int breakableMinCount = 4;
    [SerializeField, Min(0)] private int breakableMaxCount = 8;
    [SerializeField, Min(0f)] private float breakablePlayerClearance = 4f;
    [SerializeField, Min(0f)] private float breakableSpacing = 1.4f;
    [SerializeField, Min(0f)] private float breakableObstacleClearance = 0.65f;
    [SerializeField, Min(0f)] private float breakableCriticalClearance = 2.75f;
    [SerializeField, Min(1)] private int breakablePlacementAttempts = 25;
    [SerializeField] private LayerMask breakableObstacleMask = ~0;
    [SerializeField, Range(0f, 1f)] private float breakableClusterChance = 0.35f;

    public LocalAnomalyData[] NormalAnomalies => normalAnomalies;
    public LocalAnomalyData GravityAnomaly => gravityAnomaly;
    public AnomalyPowerType[] SpecialPowerPool => specialPowerPool;
    public AnomalyArtHookSet ElectricArtHooks => electricArtHooks;
    public AnomalyArtHookSet BeamArtHooks => beamArtHooks;
    public float TargetAnomalyCoverage =>
        Mathf.Clamp(targetAnomalyCoverage, 0.85f, 0.95f);
    public float EdgePadding => Mathf.Max(0f, edgePadding);
    public float ExitRadius => Mathf.Max(0.5f, exitRadius);
    public float ElectricEnemyDamage => Mathf.Max(0f, electricEnemyDamage);
    public float ElectricPlayerDamage => Mathf.Max(0f, electricPlayerDamage);
    public float BeamEnemyDamage => Mathf.Max(0f, beamEnemyDamage);
    public float BeamPlayerDamage => Mathf.Max(0f, beamPlayerDamage);
    public RunThreatConfig ThreatConfig => threatConfig;
    public WorldBreakable BreakablePrefab => breakablePrefab;
    public int BreakableMinCount => Mathf.Max(0, breakableMinCount);
    public int BreakableMaxCount =>
        Mathf.Max(BreakableMinCount, breakableMaxCount);
    public float BreakablePlayerClearance =>
        Mathf.Max(0f, breakablePlayerClearance);
    public float BreakableSpacing => Mathf.Max(0f, breakableSpacing);
    public float BreakableObstacleClearance =>
        Mathf.Max(0f, breakableObstacleClearance);
    public float BreakableCriticalClearance =>
        Mathf.Max(0f, breakableCriticalClearance);
    public int BreakablePlacementAttempts =>
        Mathf.Max(1, breakablePlacementAttempts);
    public LayerMask BreakableObstacleMask => breakableObstacleMask;
    public float BreakableClusterChance =>
        Mathf.Clamp01(breakableClusterChance);
}
