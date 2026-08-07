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

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? name
        : displayName;
    public string Description => description;
    public int RequiredStationLevel => Mathf.Clamp(requiredStationLevel, 1, 3);
    public AnomalyStabilizerEffectType EffectType => effectType;
    public float Value => value;
}
