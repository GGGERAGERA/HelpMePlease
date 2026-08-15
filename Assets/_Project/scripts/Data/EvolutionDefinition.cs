using UnityEngine;

public enum EvolutionRuntimeType
{
    None = 0,
    GravityHybrid = 1,
    ArcHybrid = 2,
    BeamHybrid = 3
}

[CreateAssetMenu(
    fileName = "New EvolutionDefinition",
    menuName = "Game/Run Build/Evolution Definition")]
public sealed class EvolutionDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField] private EvolutionRuntimeType runtimeType;
    [SerializeField, Range(0.05f, 2f)]
    private float payloadFireRateMultiplier = 0.7f;

    [Header("Gravity Overdrive")]
    [SerializeField, Range(1f, 5f)]
    private float overdriveFireRateMultiplier = 2.5f;
    [SerializeField, Range(1f, 180f)] private float overdriveAngularStep = 47f;
    [SerializeField, Range(0f, 30f)] private float overdriveDirectionJitter = 3f;

    [Header("Overdrive Topology")]
    [SerializeField, Range(0.1f, 2f)] private float payloadRangeMultiplier = 1f;
    [SerializeField, Range(1, 4)] private int overdriveBranchCount = 2;
    [SerializeField, Range(1, 12)] private int branchTargets = 3;
    [SerializeField] private float[] beamEmissionPoints = { 0.25f, 0.5f, 0.75f };
    [SerializeField, Min(0.1f)] private float beamPointTargetRadius = 5f;

    [Header("Performance Safety")]
    [SerializeField, Range(1, 32)] private int maxPayloadAttacksPerTick = 12;
    [SerializeField, Range(1, 32)] private int maxPayloadSegments = 6;

    public string Id => id ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? name
        : displayName;
    public Sprite Icon => icon;
    public EvolutionRuntimeType RuntimeType => runtimeType;
    public float PayloadFireRateMultiplier => Mathf.Clamp(
        payloadFireRateMultiplier, 0.05f, 2f);
    public float OverdriveFireRateMultiplier => Mathf.Clamp(
        overdriveFireRateMultiplier, 1f, 5f);
    public float OverdriveAngularStep => Mathf.Clamp(
        overdriveAngularStep, 1f, 180f);
    public float OverdriveDirectionJitter => Mathf.Clamp(
        overdriveDirectionJitter, 0f, 30f);
    public float PayloadRangeMultiplier => Mathf.Clamp(payloadRangeMultiplier, 0.1f, 2f);
    public int OverdriveBranchCount => Mathf.Clamp(overdriveBranchCount, 1, 4);
    public int BranchTargets => Mathf.Clamp(branchTargets, 1, 12);
    public float[] BeamEmissionPoints => beamEmissionPoints ?? System.Array.Empty<float>();
    public float BeamPointTargetRadius => Mathf.Max(0.1f, beamPointTargetRadius);
    public int MaxPayloadAttacksPerTick => Mathf.Clamp(maxPayloadAttacksPerTick, 1, 32);
    public int MaxPayloadSegments => Mathf.Clamp(maxPayloadSegments, 1, 32);
}
