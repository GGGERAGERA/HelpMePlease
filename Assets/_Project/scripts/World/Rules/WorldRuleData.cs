using UnityEngine;

public enum WindDirectionMode
{
    None = 0,
    Fixed = 1,
    RandomCardinal = 2,
    RandomEightDirections = 3
}

[CreateAssetMenu(
    fileName = "WorldRuleData",
    menuName = "Game/Levels/World Rule Data"
)]
public sealed class WorldRuleData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable ID assigned explicitly for migration and persistence.")]
    [SerializeField] private string stableId;
    [SerializeField] private WorldRuleType ruleType;

    [Header("View")]
    [SerializeField] private string displayName;
    [TextArea(2, 4)]
    [SerializeField] private string description;
    [TextArea(1, 2)]
    [Tooltip("Short sector-choice description, limited to one concise effect.")]
    [SerializeField] private string shortDescription;
    [Tooltip("Icon used by the sector-choice card.")]
    [SerializeField] private Sprite icon;
    [Tooltip("Accent used by the sector-choice frame, icon and header.")]
    [SerializeField] private Color presentationColor =
        new(0.2f, 0.72f, 0.82f, 1f);
    [TextArea(1, 2)]
    [SerializeField] private string pinnedDescription;

    [Header("Selection")]
    [SerializeField, Min(0f)] private float selectionWeight = 1f;

    [Header("Sector Rewards")]
    [Tooltip("Experience multiplier applied to a sector using this rule.")]
    [SerializeField, Min(0.1f)]
    private float sectorExperienceMultiplier = 1f;
    [Tooltip("Completion-gold multiplier applied to a sector using this rule.")]
    [SerializeField, Min(0.1f)]
    private float sectorCompletionGoldMultiplier = 1f;

    [Header("Gameplay")]
    [Tooltip("Multiplier applied to the player's regular movement speed.")]
    [SerializeField, Min(0.1f)]
    private float playerMoveSpeedMultiplier = 1f;
    [Tooltip("Multiplier applied to enemy movement speed.")]
    [SerializeField, Min(0.1f)]
    private float enemyMoveSpeedMultiplier = 1f;
    [Tooltip("Multiplier applied to all enemy health for this world rule.")]
    [SerializeField, Min(0.1f)]
    private float enemyHealthMultiplier = 1f;
    [Tooltip("Multiplier applied once to the level spawn pressure.")]
    [SerializeField, Min(0.1f)]
    private float spawnPressureMultiplier = 1f;

    [Header("Golden Enemies")]
    [Tooltip("Chance that an eligible newly spawned enemy becomes Golden.")]
    [SerializeField, Range(0f, 1f)]
    private float goldenEnemyChance;
    [Tooltip("Maximum health multiplier applied to a Golden enemy after spawn scaling.")]
    [SerializeField, Min(1f)]
    private float goldenEnemyHealthMultiplier = 1f;
    [Tooltip("Multiplier for the kill-reward share of that Golden enemy only.")]
    [SerializeField, Min(1f)]
    private float goldenEnemyRewardMultiplier = 1f;

    [Header("Golden Coin Pickups")]
    [Tooltip("Minimum number of physical bonus coins dropped by a Golden enemy.")]
    [SerializeField, Min(1)] private int goldenCoinCountMin = 2;
    [Tooltip("Maximum number of physical bonus coins dropped by a Golden enemy.")]
    [SerializeField, Min(1)] private int goldenCoinCountMax = 4;
    [Tooltip("Regular CurrencyManager gold granted by each collected coin.")]
    [SerializeField, Min(1)] private int goldenCoinValue = 1;
    [Tooltip("Seconds before an uncollected coin expires.")]
    [SerializeField, Min(0.1f)] private float goldenCoinLifetime = 6f;
    [Tooltip("Fallback attraction radius when the player has no PlayerPickupRadius.")]
    [SerializeField, Min(0.1f)] private float goldenCoinPickupRadius = 3f;
    [Tooltip("Movement speed while a coin is attracted to the player.")]
    [SerializeField, Min(0.1f)] private float goldenCoinAttractSpeed = 8f;
    [Tooltip("Initial visual scatter speed when coins are dropped.")]
    [SerializeField, Min(0f)] private float goldenCoinScatterSpeed = 2.4f;
    [Tooltip("Duration of the fade and pulse before an uncollected coin expires.")]
    [SerializeField, Range(0.1f, 3f)] private float goldenCoinFadeDuration = 1.25f;
    [Tooltip("Safety limit for simultaneously active Golden coin pickups.")]
    [SerializeField, Range(4, 128)] private int goldenCoinActiveLimit = 48;

    [Header("Snow Cycle")]
    [Tooltip("Minimum duration of calm snowfall before a blizzard warning.")]
    [SerializeField, Min(0.1f)] private float snowCalmDurationMin = 15f;
    [Tooltip("Maximum duration of calm snowfall before a blizzard warning.")]
    [SerializeField, Min(0.1f)] private float snowCalmDurationMax = 24f;
    [Tooltip("Duration of the gradual visual build-up before a blizzard.")]
    [SerializeField, Min(0.1f)] private float snowWarningDuration = 2.5f;
    [Tooltip("Minimum duration of a fully developed blizzard.")]
    [SerializeField, Min(0.1f)] private float snowBlizzardDurationMin = 5f;
    [Tooltip("Maximum duration of a fully developed blizzard.")]
    [SerializeField, Min(0.1f)] private float snowBlizzardDurationMax = 7f;
    [Tooltip("Falling-snow emission multiplier during the calm phase.")]
    [SerializeField, Min(0f)] private float snowCalmEmissionMultiplier = 1f;
    [Tooltip("Falling-snow emission multiplier during a blizzard.")]
    [SerializeField, Min(0f)] private float snowBlizzardEmissionMultiplier = 3.5f;
    [Tooltip("Falling-snow speed multiplier during the calm phase.")]
    [SerializeField, Min(0f)]
    private float snowCalmParticleSpeedMultiplier = 1f;
    [Tooltip("Falling-snow speed multiplier during a blizzard.")]
    [SerializeField, Min(0f)]
    private float snowBlizzardParticleSpeedMultiplier = 2.1f;
    [Tooltip("Fraction of distant-field visibility retained at full blizzard strength.")]
    [SerializeField, Range(0f, 1f)]
    private float snowBlizzardVisibilityMultiplier = 0.65f;
    [Tooltip("Duration of the smooth return from blizzard to calm snowfall.")]
    [SerializeField, Min(0.01f)] private float snowTransitionDuration = 1f;
    [Tooltip("Horizontal visual speed of falling snow during a blizzard.")]
    [SerializeField, Min(0f)] private float snowBlizzardHorizontalSpeed = 2.4f;

    [Header("Wind")]
    [Tooltip("Constant external velocity applied to the player by this rule.")]
    [SerializeField, Min(0f)] private float windForce;
    [Tooltip("How the wind direction is selected when the rule is applied.")]
    [SerializeField] private WindDirectionMode windDirectionMode;
    [Tooltip("Direction used by Fixed mode. A zero vector safely falls back to right.")]
    [SerializeField] private Vector2 fixedWindDirection = Vector2.right;
    [Tooltip("Minimum time in seconds before the wind changes direction.")]
    [SerializeField, Min(0.1f)] private float windMinDirectionDuration = 18f;
    [Tooltip("Maximum time in seconds before the wind changes direction.")]
    [SerializeField, Min(0.1f)] private float windMaxDirectionDuration = 30f;
    [Tooltip("How long the existing wind indicator warns about the next direction.")]
    [SerializeField, Range(0.1f, 10f)]
    private float windDirectionWarningDuration = 2.5f;
    [Tooltip("Wind strength applied to enemies relative to the player.")]
    [SerializeField, Range(0f, 1f)] private float windEnemyForceMultiplier = 0.4f;
    [Tooltip("Wind strength applied to compatible projectiles relative to the player.")]
    [SerializeField, Range(0f, 1f)]
    private float windProjectileForceMultiplier = 0.12f;

    [Header("Darkness")]
    [Tooltip("Outer radius of the player's existing point light during Darkness.")]
    [SerializeField, Min(0.1f)] private float darknessPlayerLightRadius = 4.8f;
    [Tooltip("Intensity of the player's existing point light during Darkness.")]
    [SerializeField, Min(0f)] private float darknessPlayerLightIntensity = 1.15f;
    [Tooltip("Brightness multiplier for the small enemy danger markers.")]
    [SerializeField, Range(0f, 2f)] private float darknessEnemyMarkerIntensity = 0.75f;
    [Tooltip("Outer radius of the short reveal flash caused by a player shot.")]
    [SerializeField, Min(0.1f)] private float darknessShotRevealRadius = 3.5f;
    [Tooltip("Lifetime of the single reusable shot reveal light.")]
    [SerializeField, Range(0.01f, 1f)] private float darknessShotRevealDuration = 0.12f;
    [Tooltip("Intensity of the short shot reveal light.")]
    [SerializeField, Min(0f)] private float darknessShotRevealIntensity = 1.25f;
    [Tooltip("Radius and intensity multiplier used by explosive shots.")]
    [SerializeField, Min(1f)] private float darknessExplosiveRevealMultiplier = 1.3f;
    [Tooltip("Minimum interval between reveal flashes caused by a laser.")]
    [SerializeField, Min(0.01f)] private float darknessLaserRevealCooldown = 0.16f;

    public string Id => stableId;
    public WorldRuleType RuleType => ruleType;
    public float SelectionWeight => Mathf.Max(0f, selectionWeight);
    public float SectorExperienceMultiplier =>
        sectorExperienceMultiplier > 0f
            ? sectorExperienceMultiplier
            : 1f;
    public float SectorCompletionGoldMultiplier =>
        sectorCompletionGoldMultiplier > 0f
            ? sectorCompletionGoldMultiplier
            : 1f;
    public float PlayerMoveSpeedMultiplier =>
        Mathf.Max(0.1f, playerMoveSpeedMultiplier);
    public float EnemyMoveSpeedMultiplier =>
        Mathf.Max(0.1f, enemyMoveSpeedMultiplier);
    public float EnemyHealthMultiplier =>
        enemyHealthMultiplier > 0f ? enemyHealthMultiplier : 1f;
    public float SpawnPressureMultiplier =>
        Mathf.Max(0.1f, spawnPressureMultiplier);
    public float GoldenEnemyChance => Mathf.Clamp01(goldenEnemyChance);
    public float GoldenEnemyHealthMultiplier =>
        Mathf.Max(1f, goldenEnemyHealthMultiplier);
    public float GoldenEnemyRewardMultiplier =>
        Mathf.Max(1f, goldenEnemyRewardMultiplier);
    public int GoldenCoinCountMin => Mathf.Max(1, goldenCoinCountMin);
    public int GoldenCoinCountMax => Mathf.Max(
        GoldenCoinCountMin,
        goldenCoinCountMax
    );
    public int GoldenCoinValue => Mathf.Max(1, goldenCoinValue);
    public float GoldenCoinLifetime => Mathf.Max(0.1f, goldenCoinLifetime);
    public float GoldenCoinPickupRadius =>
        Mathf.Max(0.1f, goldenCoinPickupRadius);
    public float GoldenCoinAttractSpeed =>
        Mathf.Max(0.1f, goldenCoinAttractSpeed);
    public float GoldenCoinScatterSpeed =>
        Mathf.Max(0f, goldenCoinScatterSpeed);
    public float GoldenCoinFadeDuration => Mathf.Clamp(
        goldenCoinFadeDuration,
        0.1f,
        GoldenCoinLifetime
    );
    public int GoldenCoinActiveLimit =>
        Mathf.Clamp(goldenCoinActiveLimit, 4, 128);
    public float SnowCalmDurationMin =>
        Mathf.Max(0.1f, snowCalmDurationMin);
    public float SnowCalmDurationMax => Mathf.Max(
        SnowCalmDurationMin,
        snowCalmDurationMax
    );
    public float SnowWarningDuration =>
        Mathf.Max(0.1f, snowWarningDuration);
    public float SnowBlizzardDurationMin =>
        Mathf.Max(0.1f, snowBlizzardDurationMin);
    public float SnowBlizzardDurationMax => Mathf.Max(
        SnowBlizzardDurationMin,
        snowBlizzardDurationMax
    );
    public float SnowCalmEmissionMultiplier =>
        Mathf.Max(0f, snowCalmEmissionMultiplier);
    public float SnowBlizzardEmissionMultiplier =>
        Mathf.Max(0f, snowBlizzardEmissionMultiplier);
    public float SnowCalmParticleSpeedMultiplier =>
        Mathf.Max(0f, snowCalmParticleSpeedMultiplier);
    public float SnowBlizzardParticleSpeedMultiplier =>
        Mathf.Max(0f, snowBlizzardParticleSpeedMultiplier);
    public float SnowBlizzardVisibilityMultiplier =>
        Mathf.Clamp01(snowBlizzardVisibilityMultiplier);
    public float SnowTransitionDuration =>
        Mathf.Max(0.01f, snowTransitionDuration);
    public float SnowBlizzardHorizontalSpeed =>
        Mathf.Max(0f, snowBlizzardHorizontalSpeed);
    public float WindForce => Mathf.Max(0f, windForce);
    public WindDirectionMode WindDirectionMode => windDirectionMode;
    public Vector2 FixedWindDirection =>
        fixedWindDirection.sqrMagnitude > 0.0001f
            ? fixedWindDirection.normalized
            : Vector2.right;
    public float WindMinDirectionDuration =>
        Mathf.Max(0.1f, windMinDirectionDuration);
    public float WindMaxDirectionDuration => Mathf.Max(
        WindMinDirectionDuration,
        windMaxDirectionDuration
    );
    public float WindDirectionWarningDuration => Mathf.Clamp(
        windDirectionWarningDuration,
        0.1f,
        WindMinDirectionDuration
    );
    public float WindEnemyForceMultiplier =>
        Mathf.Clamp01(windEnemyForceMultiplier);
    public float WindProjectileForceMultiplier =>
        Mathf.Clamp01(windProjectileForceMultiplier);
    public float DarknessPlayerLightRadius =>
        Mathf.Max(0.1f, darknessPlayerLightRadius);
    public float DarknessPlayerLightIntensity =>
        Mathf.Max(0f, darknessPlayerLightIntensity);
    public float DarknessEnemyMarkerIntensity =>
        Mathf.Clamp(darknessEnemyMarkerIntensity, 0f, 2f);
    public float DarknessShotRevealRadius =>
        Mathf.Max(0.1f, darknessShotRevealRadius);
    public float DarknessShotRevealDuration =>
        Mathf.Clamp(darknessShotRevealDuration, 0.01f, 1f);
    public float DarknessShotRevealIntensity =>
        Mathf.Max(0f, darknessShotRevealIntensity);
    public float DarknessExplosiveRevealMultiplier =>
        Mathf.Max(1f, darknessExplosiveRevealMultiplier);
    public float DarknessLaserRevealCooldown =>
        Mathf.Max(0.01f, darknessLaserRevealCooldown);
    public string DisplayName => displayName;
    public string ShortDescription => shortDescription;
    public Sprite Icon => icon;
    public Color PresentationColor => presentationColor;
    public LevelMechanicPresentationData Presentation =>
        new(displayName, description, pinnedDescription);
}
