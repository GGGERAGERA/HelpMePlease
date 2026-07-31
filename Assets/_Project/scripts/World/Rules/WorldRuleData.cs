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
    [SerializeField] private string pinnedDescription;

    [Header("Selection")]
    [SerializeField, Min(0f)] private float selectionWeight = 1f;

    [Header("Gameplay")]
    [Tooltip("Multiplier applied to the player's regular movement speed.")]
    [SerializeField, Min(0.1f)]
    private float playerMoveSpeedMultiplier = 1f;
    [Tooltip("Multiplier applied to enemy movement speed.")]
    [SerializeField, Min(0.1f)]
    private float enemyMoveSpeedMultiplier = 1f;

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

    [Header("Wind")]
    [Tooltip("Constant external velocity applied to the player by this rule.")]
    [SerializeField, Min(0f)] private float windForce;
    [Tooltip("How the wind direction is selected when the rule is applied.")]
    [SerializeField] private WindDirectionMode windDirectionMode;
    [Tooltip("Direction used by Fixed mode. A zero vector safely falls back to right.")]
    [SerializeField] private Vector2 fixedWindDirection = Vector2.right;

    [Header("Explosive Infection")]
    [SerializeField, Min(0.1f)] private float explosionRadius = 2.2f;
    [SerializeField, Min(0f)] private float playerExplosionDamage = 20f;
    [SerializeField, Min(0f)] private float enemyExplosionDamage = 25f;
    [SerializeField] private bool allowChainReaction = true;

    [Header("Haste")]
    [SerializeField, Min(0.1f)] private float enemySpeedMultiplier = 1f;
    [SerializeField, Min(0.1f)] private float experienceGainMultiplier = 1f;

    [Header("Regeneration")]
    [SerializeField, Min(0f)] private float playerHealthPerSecond;
    [SerializeField, Min(0.1f)] private float outgoingDamageMultiplier = 1f;

    public string Id => stableId;
    public WorldRuleType RuleType => ruleType;
    public float SelectionWeight => Mathf.Max(0f, selectionWeight);
    public float PlayerMoveSpeedMultiplier =>
        Mathf.Max(0.1f, playerMoveSpeedMultiplier);
    public float EnemyMoveSpeedMultiplier =>
        Mathf.Max(0.1f, enemyMoveSpeedMultiplier);
    public float GoldenEnemyChance => Mathf.Clamp01(goldenEnemyChance);
    public float GoldenEnemyHealthMultiplier =>
        Mathf.Max(1f, goldenEnemyHealthMultiplier);
    public float GoldenEnemyRewardMultiplier =>
        Mathf.Max(1f, goldenEnemyRewardMultiplier);
    public float WindForce => Mathf.Max(0f, windForce);
    public WindDirectionMode WindDirectionMode => windDirectionMode;
    public Vector2 FixedWindDirection =>
        fixedWindDirection.sqrMagnitude > 0.0001f
            ? fixedWindDirection.normalized
            : Vector2.right;
    public float ExplosionRadius => Mathf.Max(0.1f, explosionRadius);
    public float PlayerExplosionDamage => Mathf.Max(0f, playerExplosionDamage);
    public float EnemyExplosionDamage => Mathf.Max(0f, enemyExplosionDamage);
    public bool AllowChainReaction => allowChainReaction;
    public float EnemySpeedMultiplier => Mathf.Max(0.1f, enemySpeedMultiplier);
    public float ExperienceGainMultiplier =>
        Mathf.Max(0.1f, experienceGainMultiplier);
    public float PlayerHealthPerSecond =>
        Mathf.Max(0f, playerHealthPerSecond);
    public float OutgoingDamageMultiplier =>
        Mathf.Max(0.1f, outgoingDamageMultiplier);
    public LevelMechanicPresentationData Presentation =>
        new(displayName, description, pinnedDescription);
}
