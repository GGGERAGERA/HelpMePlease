using System;

public enum AnomalyGrantResult
{
    Accepted,
    Upgraded,
    Maxed,
    RequiresReplacement,
    Invalid
}

/// <summary>
/// Runtime data for the single anomaly slot. It intentionally does not store
/// UpgradeData and has no dependency on upgrade-slot replacement UI.
/// </summary>
[Serializable]
public sealed class AnomalyInventory
{
    public const int MaxSlots = 1;

    public event Action Changed;

    public AnomalyItemData CurrentItem { get; private set; }
    public int Level { get; private set; }
    public int Capacity => MaxSlots;
    public bool IsEmpty => CurrentItem == null;

    public AnomalyGrantResult TryGrant(AnomalyItemData item)
    {
        if (item == null)
            return AnomalyGrantResult.Invalid;

        if (CurrentItem == null)
        {
            CurrentItem = item;
            Level = 1;
            Changed?.Invoke();
            return AnomalyGrantResult.Accepted;
        }

        if (!CurrentItem.Matches(item))
            return AnomalyGrantResult.RequiresReplacement;

        if (Level >= CurrentItem.MaxLevel)
            return AnomalyGrantResult.Maxed;

        Level = Math.Min(Level + 1, CurrentItem.MaxLevel);
        Changed?.Invoke();
        return AnomalyGrantResult.Upgraded;
    }

    public void Clear()
    {
        if (CurrentItem == null && Level == 0)
            return;

        CurrentItem = null;
        Level = 0;
        Changed?.Invoke();
    }
}
