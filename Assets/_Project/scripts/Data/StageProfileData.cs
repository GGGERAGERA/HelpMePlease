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

    [SerializeField]
    [Tooltip("Enemy spawn phases used by this sector.")]
    private EnemySpawnProfile spawnProfile;

    [SerializeField]
    [Tooltip("Boss spawned when the final sector zone is reached.")]
    private GameObject bossPrefab;

    [SerializeField] private WorldHazardPreset worldHazards;
    public WorldHazardPreset WorldHazards => worldHazards;

    [Header("Sector Pacing")]
    [SerializeField, Min(0f)] private float exitActivationTime = 90f;
    [Tooltip("Unlock only if the first automatic Assault has never started (technical fallback).")]
    [SerializeField, Min(0f)] private float exitUnlockTimeout = 140f;
    [SerializeField, Range(0.1f, 1f)] private float assaultSizeMultiplier = 1f;
    [SerializeField, Min(0f)] private float bossPreparationDuration = 10f;

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
    public EnemySpawnProfile SpawnProfile => spawnProfile;
    public GameObject BossPrefab => bossPrefab;
    public float ExitActivationTime => exitActivationTime;
    public float ExitUnlockTimeout => Mathf.Max(exitActivationTime, exitUnlockTimeout);
    public float AssaultSizeMultiplier => assaultSizeMultiplier;
    public float BossPreparationDuration => bossPreparationDuration;
    public float EnemyHealthMultiplier => enemyHealthMultiplier;
    public float EnemySpeedMultiplier => enemySpeedMultiplier;
    public float SpawnPressureMultiplier => spawnPressureMultiplier;
    public float ExperienceGainMultiplier => experienceGainMultiplier;
    public float CompletionGoldMultiplier => completionGoldMultiplier;
}
