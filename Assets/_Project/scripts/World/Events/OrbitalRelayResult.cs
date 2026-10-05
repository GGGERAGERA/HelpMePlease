using System;

public readonly struct WorldEventRewardResult
{
    public readonly int Gold, UpgradeSelections;
    public WorldEventRewardResult(int gold, int upgradeSelections)
    { Gold = gold; UpgradeSelections = upgradeSelections; }
}

public readonly struct OrbitalRelayResult
{
    public readonly bool Success;
    public readonly int BonusActivations, Gold, UpgradeSelections;
    private OrbitalRelayResult(bool success, int activations, int gold)
    { Success = success; BonusActivations = activations; Gold = gold; UpgradeSelections = success ? 1 : 0; }
    public WorldEventRewardResult Reward => new(Gold, UpgradeSelections);
    public static OrbitalRelayResult Calculate(bool stabilized, int bonusActivations, int goldPerActivation) =>
        new(stabilized, Math.Max(0, bonusActivations), stabilized
            ? (int)Math.Min(int.MaxValue, (long)Math.Max(0, bonusActivations) * Math.Max(0, goldPerActivation)) : 0);
}
