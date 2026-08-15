using UnityEngine;

public enum EvolutionRuntimeType
{
    None = 0,
    PistolGravity = 1
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

    public string Id => id ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? name
        : displayName;
    public Sprite Icon => icon;
    public EvolutionRuntimeType RuntimeType => runtimeType;
    public float PayloadFireRateMultiplier => Mathf.Clamp(
        payloadFireRateMultiplier, 0.05f, 2f);
}
