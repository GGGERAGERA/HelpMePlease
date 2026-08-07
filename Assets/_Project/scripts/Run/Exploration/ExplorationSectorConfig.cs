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

    [Header("Layout")]
    [SerializeField, Range(0.88f, 0.9f)]
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

    public LocalAnomalyData[] NormalAnomalies => normalAnomalies;
    public LocalAnomalyData GravityAnomaly => gravityAnomaly;
    public AnomalyPowerType[] SpecialPowerPool => specialPowerPool;
    public float TargetAnomalyCoverage =>
        Mathf.Clamp(targetAnomalyCoverage, 0.88f, 0.9f);
    public float EdgePadding => Mathf.Max(0f, edgePadding);
    public float ExitRadius => Mathf.Max(0.5f, exitRadius);
    public float ElectricEnemyDamage => Mathf.Max(0f, electricEnemyDamage);
    public float ElectricPlayerDamage => Mathf.Max(0f, electricPlayerDamage);
    public float BeamEnemyDamage => Mathf.Max(0f, beamEnemyDamage);
    public float BeamPlayerDamage => Mathf.Max(0f, beamPlayerDamage);
    public RunThreatConfig ThreatConfig => threatConfig;
}
