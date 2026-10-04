using UnityEngine;

/// <summary>Shared production station content, independent of a bunker scene.</summary>
[CreateAssetMenu(menuName = "Subject42/Bunker/Selection Catalog")]
public sealed class BunkerSelectionCatalog : ScriptableObject
{
    public CharacterData[] characters;
    public WeaponData[] weapons;
    public BunkerSelectionSourceHub.UpgradePresentation[] upgrades;
    public AnomalyStabilizerData[] anomalies;
}
