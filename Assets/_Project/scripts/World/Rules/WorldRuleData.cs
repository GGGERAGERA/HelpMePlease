using UnityEngine;

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
