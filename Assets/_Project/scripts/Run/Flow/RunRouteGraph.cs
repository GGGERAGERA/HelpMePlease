using System;
using System.Collections.Generic;

public enum RouteDifficulty { Easy, Medium, Hard }
public enum RewardProfile { WeaponLevel, ModuleLevel }

public sealed class RouteNode
{
    public int SectorNumber { get; }
    public int Row { get; }
    internal int RowCount { get; }
    public WorldRuleData WorldRule { get; }
    public RewardProfile RewardProfile { get; }
    public RouteDifficulty Difficulty { get; }
    public int DurationSeconds => Difficulty switch { RouteDifficulty.Easy => 30, RouteDifficulty.Hard => 90, _ => 60 };
    internal float SpawnPressureMultiplier => Difficulty switch { RouteDifficulty.Easy => .9f, RouteDifficulty.Hard => 1.1f, _ => 1f };
    internal SurfaceSectorDefinition Expedition { get; }
    public IReadOnlyList<RouteNode> Next { get; internal set; } = Array.Empty<RouteNode>();

    internal RouteNode(int sector, int row, int rowCount, WorldRuleData rule,
        RewardProfile reward, RouteDifficulty difficulty, SurfaceSectorDefinition expedition)
    {
        SectorNumber = sector;
        Row = row;
        RowCount = rowCount;
        WorldRule = rule;
        RewardProfile = reward;
        Difficulty = difficulty;
        Expedition = expedition;
    }
}

public sealed class RunRouteGraph
{
    private const int MaxRows = 3;
    private readonly RouteNode[][] columns;
    public int Seed { get; }
    public IReadOnlyList<RouteNode> Entries => columns[0];

    private RunRouteGraph(int seed, RouteNode[][] columns) { Seed = seed; this.columns = columns; }
    public IReadOnlyList<RouteNode> NodesAt(int sectorNumber) => columns[sectorNumber - 1];

    internal bool ContainsEntry(RouteNode node) => Array.IndexOf(columns[0], node) >= 0;

    internal static RunRouteGraph Generate(int seed, WorldRuleData[] worldRules,
        WorldRuleData initialRule, bool fixedEntryRule, IReadOnlyList<SurfaceSectorDefinition> expeditions = null)
    {
        var random = new System.Random(seed);
        var nodeRandom = new System.Random(seed ^ 0x3E18621D);
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
            int difficultyOffset = nodeRandom.Next(MaxRows);
            int rewardOffset = nodeRandom.Next(2);
            var usedWeather = new HashSet<WorldRuleType>();
            for (int row = 0; row < nodes.Length; row++)
            {
                var expedition = entries?[row];
                var rule = expedition != null ? expedition.WorldRule ?? initialRule :
                    opening && initialRule != null && (fixedEntryRule || row == 0) ? initialRule :
                    ChooseWeather(weather, usedWeather, random);
                usedWeather.Add(rule != null ? rule.RuleType : WorldRuleType.None);
                var difficulty = (RouteDifficulty)((difficultyOffset + row) % MaxRows);
                var reward = (RewardProfile)((rewardOffset + row) % 2);
                nodes[row] = new RouteNode(sector, row, nodes.Length, rule, reward, difficulty, expedition);
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
}
