using System;
using System.Collections.Generic;
using UnityEngine;

public interface IBunkerSelectionSource
{
    event Action Changed;
    void Prepare();
    BunkerSelectionWindowModel BuildModel();
    void Confirm(string entryId);
}

public sealed class BunkerSelectionWindowModel
{
    public string Title;
    public string SectionTitle;
    public string EmptyText;
    public string ConfirmText;
    public string SelectedId;
    public bool CloseOnConfirm = true;
    public bool CardsOnly;
    public bool ShowConfirmButton = true;
    public readonly List<BunkerSelectionEntryModel> Entries = new();
    public readonly List<BunkerSelectionUnlockModel> Unlocks = new();
    public BunkerStationProgressModel Station;
}

public sealed class BunkerSelectionEntryModel
{
    public string Id;
    public string DisplayName;
    public string Category;
    public Sprite Icon;
    public Color IconColor = Color.white;
    public string Feature;
    public string Description;
    public string LockReason;
    public bool Locked;
    public bool Enabled = true;
    public bool CanConfirm = true;
    public int RequiredStationLevel = 1;
    public BunkerProgressionModel Progression;
    public readonly List<BunkerSelectionStatModel> Stats = new();
}

public readonly struct BunkerSelectionUnlockModel
{
    public readonly string DisplayName;
    public readonly int RequiredStationLevel;

    public BunkerSelectionUnlockModel(string displayName, int requiredStationLevel)
    {
        DisplayName = displayName;
        RequiredStationLevel = Mathf.Max(1, requiredStationLevel);
    }
}

public readonly struct BunkerSelectionStatModel
{
    public readonly string Label;
    public readonly string Value;

    public BunkerSelectionStatModel(string label, string value)
    {
        Label = label;
        Value = value;
    }
}

public class BunkerProgressionModel
{
    public string TargetId;
    public string Title;
    public string LevelPrefix = "УРОВЕНЬ";
    public int Level;
    public int MaxLevel;
    public int Progress;
    public int RequiredProgress;
    public int Cost;
    public int AvailableCurrency;
    public string BonusText;
    public string ContextText;
    public string LockReason;
    public string ButtonText = "УЛУЧШИТЬ";
    public bool Locked;
    public bool SupportsPartialInvestment;
    public Func<bool> CanUpgrade;
    public Action Upgrade;
    public Action<int> Invest;
}

public sealed class BunkerStationProgressModel : BunkerProgressionModel
{
}
