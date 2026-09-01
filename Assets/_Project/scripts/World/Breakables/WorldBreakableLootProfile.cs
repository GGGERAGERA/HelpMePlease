using UnityEngine;

public enum WorldBreakableRewardKind
{
    GoldPickups,
    WorldEventUpgradeChoices
}

[CreateAssetMenu(
    fileName = "WorldBreakableLootProfile",
    menuName = "Subject42/World/Breakable Loot Profile")]
public sealed class WorldBreakableLootProfile : ScriptableObject
{
    [SerializeField] private WorldBreakableRewardKind rewardKind =
        WorldBreakableRewardKind.GoldPickups;

    [Header("Gold Pickups")]
    [SerializeField, Min(0f)] private float nothingWeight;
    [SerializeField, Min(0f)] private float smallGoldWeight = 1f;
    [SerializeField, Min(0f)] private float largeGoldWeight;
    [SerializeField, Min(1)] private int smallGoldCount = 1;
    [SerializeField, Min(1)] private int largeGoldCount = 3;
    [SerializeField, Min(1)] private int goldValue = 5;

    [Header("World Event Reward")]
    [SerializeField, Min(1)] private int eventChoiceCount = 3;

    public WorldBreakableRewardKind RewardKind => rewardKind;
    public float NothingWeight => Mathf.Max(0f, nothingWeight);
    public float SmallGoldWeight => Mathf.Max(0f, smallGoldWeight);
    public float LargeGoldWeight => Mathf.Max(0f, largeGoldWeight);
    public int SmallGoldCount => Mathf.Max(1, smallGoldCount);
    public int LargeGoldCount => Mathf.Max(1, largeGoldCount);
    public int GoldValue => Mathf.Max(1, goldValue);
    public int EventChoiceCount => Mathf.Max(1, eventChoiceCount);
    public bool GuaranteesReward => rewardKind ==
        WorldBreakableRewardKind.WorldEventUpgradeChoices ||
        (NothingWeight <= 0f && SmallGoldWeight + LargeGoldWeight > 0f);

#if UNITY_EDITOR
    private void OnValidate()
    {
        nothingWeight = Mathf.Max(0f, nothingWeight);
        smallGoldWeight = Mathf.Max(0f, smallGoldWeight);
        largeGoldWeight = Mathf.Max(0f, largeGoldWeight);
        smallGoldCount = Mathf.Max(1, smallGoldCount);
        largeGoldCount = Mathf.Max(1, largeGoldCount);
        goldValue = Mathf.Max(1, goldValue);
        eventChoiceCount = Mathf.Max(1, eventChoiceCount);
    }
#endif
}
