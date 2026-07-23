using UnityEngine;

[CreateAssetMenu(
    fileName = "LevelAnomalyData",
    menuName = "Game/Levels/Level Anomaly Data"
)]
public sealed class LevelAnomalyData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string id;
    [SerializeField] private LevelAnomalyType anomalyType;

    [Header("View")]
    [SerializeField] private string displayName;
    [TextArea(2, 4)]
    [SerializeField] private string description;
    [TextArea(1, 2)]
    [SerializeField] private string pinnedDescription;
    [SerializeField] private Sprite icon;

    [Header("Selection")]
    [SerializeField, Min(0f)] private float selectionWeight = 1f;

    [Header("Explosive Infection")]
    [SerializeField, Min(0.1f)] private float explosionRadius = 2.2f;
    [SerializeField, Min(0f)] private float playerExplosionDamage = 20f;
    [SerializeField, Min(0f)] private float enemyExplosionDamage = 25f;
    [SerializeField] private bool allowChainReaction = true;

    [Header("Berserk")]
    [SerializeField, Min(0.1f)] private float outgoingDamageMultiplier = 2f;
    [SerializeField, Min(0.1f)] private float incomingDamageMultiplier = 2f;

    [Header("Haste")]
    [SerializeField, Min(0.1f)] private float enemySpeedMultiplier = 1f;
    [SerializeField, Min(0.1f)] private float experienceGainMultiplier = 1f;

    [Header("Regeneration")]
    [SerializeField, Min(0f)] private float playerHealthPerSecond;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public LevelAnomalyType AnomalyType => anomalyType;
    public string DisplayName => displayName;
    public string Description => description;
    public string PinnedDescription => pinnedDescription;
    public Sprite Icon => icon;
    public float SelectionWeight => Mathf.Max(0f, selectionWeight);
    public float ExplosionRadius => Mathf.Max(0.1f, explosionRadius);
    public float PlayerExplosionDamage => Mathf.Max(0f, playerExplosionDamage);
    public float EnemyExplosionDamage => Mathf.Max(0f, enemyExplosionDamage);
    public bool AllowChainReaction => allowChainReaction;
    public float OutgoingDamageMultiplier => Mathf.Max(0.1f, outgoingDamageMultiplier);
    public float IncomingDamageMultiplier => Mathf.Max(0.1f, incomingDamageMultiplier);
    public float EnemySpeedMultiplier => Mathf.Max(0.1f, enemySpeedMultiplier);
    public float ExperienceGainMultiplier => Mathf.Max(0.1f, experienceGainMultiplier);
    public float PlayerHealthPerSecond => Mathf.Max(0f, playerHealthPerSecond);
}
