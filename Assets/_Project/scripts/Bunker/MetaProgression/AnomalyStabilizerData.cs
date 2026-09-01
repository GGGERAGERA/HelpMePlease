using UnityEngine;

public enum AnomalyStabilizerEffectType
{
    ZoneSize,
    GoldInsideAnomaly,
    StasisPlayerEffect,
    GravityPlayerForce
}

[CreateAssetMenu(
    fileName = "AnomalyStabilizer",
    menuName = "Bunker/Anomaly Stabilizer")]
public sealed class AnomalyStabilizerData : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [TextArea(2, 4)]
    [SerializeField] private string description;
    [SerializeField, Range(1, 3)] private int requiredStationLevel = 1;
    [SerializeField] private AnomalyStabilizerEffectType effectType;
    [SerializeField] private float value = 1f;
    [Header("Permanent Meta Progression")]
    [SerializeField, Range(1, 10)] private int maxMetaLevel = 3;
    [Tooltip("Cost to advance from level N to N+1. Expected size: maxMetaLevel - 1.")]
    [SerializeField] private int[] metaUpgradeCosts;
    [Tooltip("Full effect value at each meta level. Empty entries safely fall back to Value.")]
    [SerializeField] private float[] metaEffectValues;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? name
        : displayName;
    public string Description => description;
    public int RequiredStationLevel => Mathf.Clamp(requiredStationLevel, 1, 3);
    public AnomalyStabilizerEffectType EffectType => effectType;
    public float Value => value;
    public int MaxMetaLevel => Mathf.Clamp(maxMetaLevel, 1, 10);

    public int GetMetaUpgradeCost(int currentLevel)
    {
        if (currentLevel < 1 || currentLevel >= MaxMetaLevel || metaUpgradeCosts == null)
            return 0;
        int index = currentLevel - 1;
        return index < metaUpgradeCosts.Length ? Mathf.Max(0, metaUpgradeCosts[index]) : 0;
    }

    public float GetMetaEffectValue(int level)
    {
        int index = Mathf.Clamp(level, 1, MaxMetaLevel) - 1;
        return metaEffectValues != null && index < metaEffectValues.Length
            ? metaEffectValues[index]
            : value;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxMetaLevel = Mathf.Clamp(maxMetaLevel, 1, 10);
        if (metaUpgradeCosts == null)
            return;
        for (int i = 0; i < metaUpgradeCosts.Length; i++)
            metaUpgradeCosts[i] = Mathf.Max(0, metaUpgradeCosts[i]);
    }
#endif
}
