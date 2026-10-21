using UnityEngine;
using Subject42.Combat.OrbitalStation;

/// <summary>Shared production station content, independent of a bunker scene.</summary>
[CreateAssetMenu(menuName = "Subject42/Bunker/Selection Catalog")]
public sealed class BunkerSelectionCatalog : ScriptableObject
{
    [System.Serializable]
    public sealed class ModulePresentation
    {
        public OrbitalModuleKind Kind;
        public string NameKey;
        public string CategoryKey;
        public string DescriptionKey;
        public Sprite Icon;
        public Color IconColor = Color.white;
        public Color AccentColor = Color.white;
    }

    public CharacterData[] characters;
    public ModulePresentation[] modules;
    public BunkerSelectionSourceHub.UpgradePresentation[] upgrades;
    public AnomalyStabilizerData[] anomalies;
    public Sprite anomalyIcon;
}
