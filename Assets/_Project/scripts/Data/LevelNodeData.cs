using UnityEngine;

[CreateAssetMenu(
    fileName = "LevelNodeData",
    menuName = "Game/Levels/Level Node Data"
)]
public class LevelNodeData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string id;

    [Header("View")]
    public string nodeName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Type")]
    public LevelNodeType nodeType;
    public LevelWeatherType weatherType;

    [Header("Scene")]
    [Tooltip("Leave empty to reuse the current gameplay scene.")]
    [SerializeField] private string sceneName;

    [Header("Level Rules")]
    [SerializeField, Min(1f)] private float duration = 90f;
    [SerializeField] private EnemySpawnProfile spawnProfile;
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private string mainThreat;

    [Header("Card Availability")]
    [SerializeField, Min(1)] private int minimumRunLevel = 1;
    [SerializeField, Min(0)] private int maximumRunLevel;
    [SerializeField, Min(0f)] private float choiceWeight = 1f;
    [SerializeField] private bool allowSameWeatherAsCurrent = true;

    [Header("Enemy Modifiers")]
    [Min(0.1f)] public float enemyHealthMultiplier = 1f;
    [Min(0.1f)] public float enemySpeedMultiplier = 1f;
    [Min(0.1f)] public float spawnRateMultiplier = 1f;

    [Header("Special Rules")]
    public bool hasEliteEnemies;
    public bool hasExplosiveEnemies;
    public bool hasHoldZoneEvent;
    public bool hasWorldAccelerationRule;
    public bool hasNoDamageChallenge;
    public bool hasExtraChest;

    [Header("Reward")]
    public UpgradeRarity guaranteedRewardRarity;
    [Range(0f, 1f)] public float bonusRareChance;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string SceneName => sceneName;
    public float Duration => Mathf.Max(1f, duration);
    public EnemySpawnProfile SpawnProfile => spawnProfile;
    public GameObject BossPrefab => bossPrefab;
    public string MainThreat => mainThreat;
    public int MinimumRunLevel => Mathf.Max(1, minimumRunLevel);
    public int MaximumRunLevel => Mathf.Max(0, maximumRunLevel);
    public float ChoiceWeight => Mathf.Max(0f, choiceWeight);
    public bool AllowSameWeatherAsCurrent => allowSameWeatherAsCurrent;
}
