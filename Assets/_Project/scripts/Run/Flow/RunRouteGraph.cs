using System;
using System.Collections.Generic;
using UnityEngine;

// Keep the existing serialized IDs in RouteLocationWeight.
public enum LocationType { Clearance = 0, Minefield = 1, UnknownSignal = 5 }
public enum RewardProfile { Standard }
public enum SurpriseProfile { None, ForgottenCache, Ambush, EmergencyProtocol, FieldFault, MedStation }

[Serializable]
public sealed class RouteLocationWeight
{
    [SerializeField] private LocationType locationType;
    [SerializeField] private float[] weightsByWasteland;
    [SerializeField] private WorldRuleType[] incompatibleWeather;

    public LocationType Type => locationType;
    internal float Weight(int wasteland, WorldRuleType weather)
    {
        if (incompatibleWeather != null && Array.IndexOf(incompatibleWeather, weather) >= 0) return 0f;
        if (weightsByWasteland == null || weightsByWasteland.Length == 0) return 1f;
        float weight = weightsByWasteland[Math.Min(wasteland - 1, weightsByWasteland.Length - 1)];
        return float.IsFinite(weight) ? Mathf.Max(0f, weight) : 0f;
    }
}

public sealed class RouteNode
{
    public int SectorNumber { get; }
    public int Level => RunRoute.LevelOf(SectorNumber);
    public int Row { get; }
    internal int RowCount { get; }
    public LocationType LocationType { get; }
    public WorldRuleData WorldRule { get; }
    public RewardProfile RewardProfile { get; }
    public SurpriseProfile SurpriseProfile { get; }
    internal SurfaceSectorDefinition Expedition { get; }
    public IReadOnlyList<RouteNode> Next { get; internal set; } = Array.Empty<RouteNode>();

    internal RouteNode(int sector, int row, int rowCount, LocationType type, WorldRuleData rule,
        RewardProfile reward, SurpriseProfile surprise, SurfaceSectorDefinition expedition)
    {
        SectorNumber = sector;
        Row = row;
        RowCount = rowCount;
        LocationType = type;
        WorldRule = rule;
        RewardProfile = reward;
        SurpriseProfile = surprise;
        Expedition = expedition;
    }

    public string LocationLabelKey => LocationType switch
    {
        LocationType.Minefield => "route.node.minefield",
        LocationType.UnknownSignal => "route.node.unknown",
        _ => "route.node.combat"
    };
}

public sealed class RunRouteGraph
{
    private const int MaxRows = 3;
    private static readonly SurpriseProfile[] surprises = (SurpriseProfile[])Enum.GetValues(typeof(SurpriseProfile));
    private readonly RouteNode[][] columns;
    public int Seed { get; }
    public IReadOnlyList<RouteNode> Entries => columns[0];

    private RunRouteGraph(int seed, RouteNode[][] columns) { Seed = seed; this.columns = columns; }
    public IReadOnlyList<RouteNode> NodesAt(int sectorNumber) => columns[sectorNumber - 1];

    internal bool ContainsEntry(RouteNode node) => Array.IndexOf(columns[0], node) >= 0;

    internal static RunRouteGraph Generate(int seed, WorldRuleData[] worldRules, RouteLocationWeight[] weights,
        WorldRuleData initialRule, bool fixedEntryRule, IReadOnlyList<SurfaceSectorDefinition> expeditions = null,
        float surpriseChance = .2f)
    {
        var random = new System.Random(seed);
        var incidentRandom = new System.Random(seed ^ 0x3E18621D);
        float incidentChance = float.IsFinite(surpriseChance) ? Mathf.Clamp01(surpriseChance) : .2f;
        var weather = new List<WorldRuleData>();
        var ruleTypes = new HashSet<WorldRuleType>();
        foreach (var rule in worldRules ?? Array.Empty<WorldRuleData>())
            if (rule != null && ruleTypes.Add(rule.RuleType)) weather.Add(rule);
        if (initialRule != null && ruleTypes.Add(initialRule.RuleType)) weather.Add(initialRule);
        if (weather.Count == 0) weather.Add(null);

        var columns = new RouteNode[RunRoute.TotalSectors][];
        for (int sector = RunRoute.FirstSector; sector <= RunRoute.TotalSectors; sector++)
        {
            bool opening = sector == RunRoute.FirstSector;
            var previous = opening ? Array.Empty<RouteNode>() : columns[sector - 2];
            var entries = opening && expeditions?.Count > 0 ? expeditions : null;
            var nodes = new RouteNode[Math.Min(entries?.Count ?? MaxRows, MaxRows)];
            var usedTypes = new HashSet<LocationType>();
            var usedWeather = new HashSet<WorldRuleType>();
            for (int row = 0; row < nodes.Length; row++)
            {
                var expedition = entries?[row];
                var rule = expedition != null ? expedition.WorldRule ?? initialRule :
                    opening && initialRule != null && (fixedEntryRule || row == 0) ? initialRule :
                    ChooseWeather(weather, usedWeather, random);
                var ruleType = rule != null ? rule.RuleType : WorldRuleType.None;
                var type = expedition?.RequiresScan == true ? LocationType.UnknownSignal :
                    ChooseLocation(weights, RunRoute.LevelOf(sector), ruleType, usedTypes, previous, row, random);
                usedWeather.Add(ruleType);
                usedTypes.Add(type);
                var incident = type == LocationType.UnknownSignal || incidentRandom.NextDouble() < incidentChance
                    ? surprises[incidentRandom.Next(1, surprises.Length)] : SurpriseProfile.None;
                nodes[row] = new RouteNode(sector, row, nodes.Length, type, rule,
                    RewardProfile.Standard, incident, expedition);
            }

            foreach (var parent in previous)
            {
                var next = new List<RouteNode>(MaxRows);
                foreach (var node in nodes)
                    if (Connects(parent, node.Row)) next.Add(node);
                parent.Next = next.AsReadOnly();
            }
            columns[sector - 1] = nodes;
        }
        return new RunRouteGraph(seed, columns);
    }

    private static bool Connects(RouteNode parent, int row)
    {
        if (parent.RowCount == 1) return true;
        int lane = parent.Row * (MaxRows - 1) / (parent.RowCount - 1);
        return Math.Abs(lane - row) <= 1;
    }

    private static WorldRuleData ChooseWeather(List<WorldRuleData> pool, HashSet<WorldRuleType> used, System.Random random)
    {
        var unused = pool.FindAll(rule => !used.Contains(rule != null ? rule.RuleType : WorldRuleType.None));
        var options = unused.Count > 0 ? unused : pool;
        return options[random.Next(options.Count)];
    }

    private static LocationType ChooseLocation(RouteLocationWeight[] weights, int wasteland, WorldRuleType weather,
        HashSet<LocationType> used, RouteNode[] previous, int row, System.Random random)
    {
        var options = new List<(LocationType type, float weight)>();
        int best = int.MaxValue;
        double total = 0;
        foreach (LocationType type in Enum.GetValues(typeof(LocationType)))
        {
            var setting = weights == null ? null : Array.Find(weights, option => option != null && option.Type == type);
            float weight = setting?.Weight(wasteland, weather) ?? 1f;
            if (weight <= 0f) continue;
            // Prefer a distinct fork, then avoid repeats on its incoming roads.
            int penalty = used.Contains(type) ? MaxRows + 1 : 0;
            foreach (var parent in previous)
                if (parent.LocationType == type && Connects(parent, row)) penalty++;
            if (penalty > best) continue;
            if (penalty < best) { options.Clear(); total = 0; best = penalty; }
            options.Add((type, weight));
            total += weight;
        }
        double roll = random.NextDouble() * total;
        foreach (var option in options)
        {
            roll -= option.weight;
            if (roll < 0) return option.type;
        }
        return LocationType.Clearance;
    }
}
