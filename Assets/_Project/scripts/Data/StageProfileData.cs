using UnityEngine;

[CreateAssetMenu(
    fileName = "StageProfileData",
    menuName = "Game/Levels/Stage Profile Data"
)]
public sealed class StageProfileData : ScriptableObject
{
    [SerializeField, Min(1)]
    [Tooltip("One-based sector number in the finite route.")]
    private int sectorNumber = 1;

    [SerializeField, Min(1f)]
    [Tooltip("Duration of the sector in seconds before its boss appears.")]
    private float duration = 90f;

    [SerializeField]
    [Tooltip("Enemy spawn phases used by this sector.")]
    private EnemySpawnProfile spawnProfile;

    [SerializeField]
    [Tooltip("Boss spawned after the sector duration expires.")]
    private GameObject bossPrefab;

    [SerializeField, Min(0.1f)]
    [Tooltip("Base health multiplier applied to enemies in this sector.")]
    private float enemyHealthMultiplier = 1f;

    [SerializeField, Min(0.1f)]
    [Tooltip("Base movement speed multiplier applied to enemies in this sector.")]
    private float enemySpeedMultiplier = 1f;

    [SerializeField, Min(0.1f)]
    [Tooltip("Base enemy spawn pressure multiplier for this sector.")]
    private float spawnPressureMultiplier = 1f;

    [SerializeField, Min(0.1f)]
    [Tooltip("Experience gain multiplier applied during this sector.")]
    private float experienceGainMultiplier = 1f;

    [SerializeField, Min(0.1f)]
    [Tooltip("Gold reward multiplier applied when this sector is completed.")]
    private float completionGoldMultiplier = 1f;

    public int SectorNumber => sectorNumber;
    public float Duration => duration;
    public EnemySpawnProfile SpawnProfile => spawnProfile;
    public GameObject BossPrefab => bossPrefab;
    public float EnemyHealthMultiplier => enemyHealthMultiplier;
    public float EnemySpeedMultiplier => enemySpeedMultiplier;
    public float SpawnPressureMultiplier => spawnPressureMultiplier;
    public float ExperienceGainMultiplier => experienceGainMultiplier;
    public float CompletionGoldMultiplier => completionGoldMultiplier;
}
