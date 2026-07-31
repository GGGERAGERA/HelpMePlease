using UnityEngine;

[CreateAssetMenu(
    fileName = "LocalAnomalyData",
    menuName = "Game/Levels/Local Anomaly Data"
)]
public sealed class LocalAnomalyData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable identifier used by migration and diagnostics.")]
    [SerializeField] private string stableId;
    [SerializeField] private LocalAnomalyType anomalyType;

    [Header("View")]
    [SerializeField] private string displayName;
    [TextArea(2, 4)]
    [SerializeField] private string description;
    [TextArea(1, 2)]
    [SerializeField] private string pinnedDescription;

    [Header("Zone Gameplay")]
    [Tooltip("Zone prefab spawned for this anomaly. Null means neutral gameplay.")]
    [SerializeField] private LocalAnomalyZone zonePrefab;
    [Tooltip("World-space radius of each spawned zone.")]
    [SerializeField, Min(0.1f)] private float zoneRadius = 4f;
    [Tooltip("Enemy movement multiplier while inside this anomaly zone.")]
    [SerializeField, Min(0.1f)] private float enemySpeedMultiplier = 1f;
    [Tooltip("Player movement multiplier while inside this anomaly zone.")]
    [SerializeField, Min(0.1f)] private float playerSpeedMultiplier = 1f;
    [Tooltip("Additional zone data spawned with this anomaly. Used to preserve multi-zone level layouts.")]
    [SerializeField] private LocalAnomalyData[] additionalAnomalies;

    public string Id => string.IsNullOrWhiteSpace(stableId)
        ? name
        : stableId;
    public LocalAnomalyType AnomalyType => anomalyType;
    public LocalAnomalyZone ZonePrefab => zonePrefab;
    public float ZoneRadius => Mathf.Max(0.1f, zoneRadius);
    public float EnemySpeedMultiplier =>
        Mathf.Max(0.1f, enemySpeedMultiplier);
    public float PlayerSpeedMultiplier =>
        Mathf.Max(0.1f, playerSpeedMultiplier);
    public LocalAnomalyData[] AdditionalAnomalies => additionalAnomalies;
    public LevelMechanicPresentationData Presentation =>
        new(displayName, description, pinnedDescription);
}
