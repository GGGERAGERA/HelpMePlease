using System;
using System.Collections.Generic;
using UnityEngine;
using Random = GameplayRandom.RuleRandom;

[Serializable]
public sealed class RouteSectorType
{
    [SerializeField] private string labelKey;
    [SerializeField] private WorldRuleData matchingWorldRule;

    public string LabelKey => labelKey;
    public WorldRuleData MatchingWorldRule => matchingWorldRule;
}

public sealed class RouteNode
{
    public int Level { get; }
    public int SectorNumber => (Level - 1) * RunRoute.SectorsPerLevel + RunRoute.FirstSector;
    public int Row { get; }
    internal int RowCount { get; }
    public RouteSectorType Type { get; }
    public WorldRuleData WorldRule { get; }
    internal SurfaceSectorDefinition Expedition { get; }
    public IReadOnlyList<RouteNode> Next { get; internal set; } = Array.Empty<RouteNode>();

    internal RouteNode(int level, int row, int rowCount, RouteSectorType type,
        WorldRuleData worldRule, SurfaceSectorDefinition expedition)
    {
        Level = level;
        Row = row;
        RowCount = rowCount;
        Type = type;
        WorldRule = worldRule;
        Expedition = expedition;
    }
}

public sealed class RunRouteGraph
{
    private const int MaxRows = 3;
    private readonly RouteNode[][] levels;
    public IReadOnlyList<RouteNode> Entries => levels[0];

    private RunRouteGraph(RouteNode[][] levels) => this.levels = levels;
    public IReadOnlyList<RouteNode> NodesAt(int level) => levels[level - 1];

    internal bool ContainsEntry(RouteNode node)
    {
        foreach (var entry in Entries)
            if (entry == node) return true;
        return false;
    }

    internal static RunRouteGraph Generate(WorldRuleData[] worldRules,
        RouteSectorType[] types, WorldRuleData initialRule, bool fixedEntryRule,
        IReadOnlyList<SurfaceSectorDefinition> expeditions = null)
    {
        var pool = new List<WorldRuleData>();
        var ruleIds = new HashSet<string>();
        foreach (var rule in worldRules)
            if (rule != null && rule.RuleType != WorldRuleType.None && ruleIds.Add(rule.Id))
                pool.Add(rule);

        var ordinaryTypes = new List<RouteSectorType>();
        foreach (var type in types)
            if (type.MatchingWorldRule == null)
                ordinaryTypes.Add(type);

        int rows = Mathf.Clamp(pool.Count, 1, MaxRows);
        var levels = new RouteNode[RunRoute.LevelCount][];
        for (int level = 1; level <= RunRoute.LevelCount; level++)
        {
            var options = new List<WorldRuleData>(pool);
            var columnTypes = new List<RouteSectorType>(ordinaryTypes);
            if (level == 1 && initialRule != null)
                options.RemoveAll(rule => rule.Id == initialRule.Id);

            var entryExpeditions = level == 1 ? expeditions : null;
            var nodes = new RouteNode[entryExpeditions?.Count ?? rows];
            for (int row = 0; row < nodes.Length; row++)
            {
                var expedition = entryExpeditions?[row];
                WorldRuleData rule = expedition != null ? expedition.WorldRule ?? initialRule :
                    level == 1 && (fixedEntryRule || row == 0) && initialRule != null
                    ? initialRule : TakeRandom(options);
                RouteSectorType type = expedition?.RequiresScan == true
                    ? Array.Find(types, candidate => candidate.LabelKey == "route.node.unknown")
                    : Array.Find(types, candidate => candidate.MatchingWorldRule != null && candidate.MatchingWorldRule == rule);
                type ??= TakeRandom(columnTypes);
                nodes[row] = new RouteNode(level, row, nodes.Length, type, rule, expedition);
            }

            if (level > 1)
            {
                var previous = levels[level - 2];
                int direction = rows > 1 && Random.Range(0, 2) == 0 ? -1 : 1;
                for (int row = 0; row < previous.Length; row++)
                {
                    if (previous.Length == 1)
                    {
                        previous[row].Next = nodes;
                        continue;
                    }
                    int lane = row * rows / previous.Length;
                    int branch = lane + direction;
                    previous[row].Next = branch >= 0 && branch < rows
                        ? new[] { nodes[lane], nodes[branch] } : new[] { nodes[lane] };
                }
            }
            levels[level - 1] = nodes;
        }
        return new RunRouteGraph(levels);
    }

    private static T TakeRandom<T>(List<T> pool) where T : class
    {
        if (pool.Count == 0) return null;
        int index = Random.Range(0, pool.Count);
        T value = pool[index];
        pool.RemoveAt(index);
        return value;
    }
}
