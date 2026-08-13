using UnityEngine;

public abstract class WorldLootRewardDefinition : ScriptableObject
{
    [SerializeField] private string rewardId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField, Min(0.01f)] private float weight = 1f;

    public string RewardId => rewardId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? name
        : displayName;
    public Sprite Icon => icon;
    public float Weight => Mathf.Max(0.01f, weight);

    public abstract bool Apply();
}
