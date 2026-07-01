using UnityEngine;

[CreateAssetMenu(
    fileName = "LevelNodeData",
    menuName = "Game/Levels/Level Node Data"
)]
public class LevelNodeData : ScriptableObject
{
    [Header("View")]
    public string nodeName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Type")]
    public LevelNodeType nodeType;
    public LevelWeatherType weatherType;

    [Header("Enemy Modifiers")]
    [Min(0.1f)] public float enemyHealthMultiplier = 1f;
    [Min(0.1f)] public float enemySpeedMultiplier = 1f;
    [Min(0.1f)] public float spawnRateMultiplier = 1f;

    [Header("Special Rules")]
    public bool hasEliteEnemies;
    public bool hasExplosiveEnemies;
    public bool hasHoldZoneEvent;
    public bool hasExtraChest;

    [Header("Reward")]
    public UpgradeRarity guaranteedRewardRarity;
    [Range(0f, 1f)] public float bonusRareChance;
}