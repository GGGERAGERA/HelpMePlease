using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "New AnomalyItemData",
    menuName = "Game/Run Build/Anomaly Item")]
public sealed class AnomalyItemData : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField, Range(1, 3)] private int maxLevel = 3;
    [SerializeField] private AnomalyPowerType powerType;

    public string Id => id ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? name
        : displayName;
    public Sprite Icon => icon;
    public int MaxLevel => Mathf.Clamp(maxLevel, 1, 3);
    public AnomalyPowerType PowerType => powerType;

    public bool Matches(AnomalyItemData other)
    {
        if (other == null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return !string.IsNullOrWhiteSpace(Id) &&
            string.Equals(Id, other.Id, StringComparison.Ordinal);
    }
}
