using UnityEngine;

[CreateAssetMenu(
    fileName = "BunkerStationProgression",
    menuName = "Bunker/Station Progression")]
public sealed class BunkerStationProgressionData : ScriptableObject
{
    [SerializeField] private BunkerStationId stationId;
    [SerializeField] private string displayName;
    [SerializeField, Range(1, 3)] private int maxLevel = 3;
    [SerializeField, Min(0)] private int level2Cost = 500;
    [SerializeField, Min(0)] private int level3Cost = 1200;
    [SerializeField] private string[] level2Unlocks;
    [SerializeField] private string[] level3Unlocks;

    public BunkerStationId StationId => stationId;
    public string DisplayName => displayName;
    public int MaxLevel => Mathf.Clamp(maxLevel, 1, 3);

    public int GetUpgradeCost(int currentLevel)
    {
        return currentLevel switch
        {
            1 => level2Cost,
            2 => level3Cost,
            _ => 0
        };
    }

    public string[] GetUnlocksForLevel(int level)
    {
        return level switch
        {
            2 => level2Unlocks,
            3 => level3Unlocks,
            _ => System.Array.Empty<string>()
        };
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxLevel = Mathf.Clamp(maxLevel, 1, 3);
        level2Cost = Mathf.Max(0, level2Cost);
        level3Cost = Mathf.Max(0, level3Cost);
    }
#endif
}
