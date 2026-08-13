using UnityEngine;

[CreateAssetMenu(
    fileName = "GoldLootReward",
    menuName = "Subject42/World Loot/Gold Reward")]
public sealed class GoldLootRewardDefinition : WorldLootRewardDefinition
{
    [SerializeField, Min(1)] private int amount = 50;

    public int Amount => Mathf.Max(1, amount);

    public override bool Apply()
    {
        CurrencyManager currency = CurrencyManager.Instance;

        if (currency == null)
        {
            Debug.LogError(
                $"[GoldLootReward] CurrencyManager is missing; " +
                $"'{DisplayName}' was not granted.",
                this
            );
            return false;
        }

        currency.AddGold(Amount);
        return true;
    }
}
