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
    [Tooltip("Base world-space size of each rectangular anomaly region.")]
    [SerializeField] private Vector2 zoneSize = new(12f, 9f);
    [Tooltip("Enemy movement multiplier while inside this anomaly zone.")]
    [SerializeField, Min(0.1f)] private float enemySpeedMultiplier = 1f;
    [Tooltip("Player movement multiplier while inside this anomaly zone.")]
    [SerializeField, Min(0.1f)] private float playerSpeedMultiplier = 1f;
    [Tooltip("Projectile movement multiplier inside a Stasis zone.")]
    [SerializeField, Min(0.1f)] private float projectileSpeedMultiplier = 0.45f;
    [Tooltip("Pickup movement multiplier inside a Stasis zone.")]
    [SerializeField, Min(0.1f)] private float pickupSpeedMultiplier = 0.5f;
    [Tooltip("Additional zone data spawned with this anomaly. Used to preserve multi-zone level layouts.")]
    [SerializeField] private LocalAnomalyData[] additionalAnomalies;
    [Tooltip("External velocity pulling objects toward a Gravity zone center.")]
    [SerializeField, Min(0f)] private float gravityForce = 1.5f;

    [Header("Explosion")]
    [SerializeField, Min(0f)] private float explosionDelay;
    [SerializeField, Min(0.1f)] private float explosionRadius = 0.1f;
    [SerializeField, Min(0f)] private float explosionDamage;
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField, Min(1f)]
    private float explosiveZoneBomberRadiusMultiplier = 1.5f;

    public string Id => string.IsNullOrWhiteSpace(stableId)
        ? name
        : stableId;
    public LocalAnomalyType AnomalyType => anomalyType;
    public LocalAnomalyZone ZonePrefab => zonePrefab;
    public Vector2 ZoneSize => new(
        Mathf.Max(0.1f, zoneSize.x),
        Mathf.Max(0.1f, zoneSize.y)
    );
    public float EnemySpeedMultiplier =>
        Mathf.Max(0.1f, enemySpeedMultiplier);
    public float PlayerSpeedMultiplier =>
        Mathf.Max(0.1f, playerSpeedMultiplier);
    public float ProjectileSpeedMultiplier =>
        Mathf.Max(0.1f, projectileSpeedMultiplier);
    public float PickupSpeedMultiplier =>
        Mathf.Max(0.1f, pickupSpeedMultiplier);
    public LocalAnomalyData[] AdditionalAnomalies => additionalAnomalies;
    public float GravityForce => Mathf.Max(0f, gravityForce);
    public float ExplosionDelay => Mathf.Max(0f, explosionDelay);
    public float ExplosionRadius => Mathf.Max(0.1f, explosionRadius);
    public float ExplosionDamage => Mathf.Max(0f, explosionDamage);
    public GameObject ExplosionEffectPrefab => explosionEffectPrefab;
    public float ExplosiveZoneBomberRadiusMultiplier =>
        Mathf.Max(1f, explosiveZoneBomberRadiusMultiplier);
    public LevelMechanicPresentationData Presentation =>
        new(displayName, description, pinnedDescription);
}
