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
    public int SectorNumber { get; }
    public int Column => RunRoute.ColumnOf(SectorNumber);
    public int Row { get; }
    public RouteSectorType Type { get; }
    public WorldRuleData WorldRule { get; }
    public IReadOnlyList<RouteNode> Next { get; internal set; } = Array.Empty<RouteNode>();

    internal RouteNode(int sectorNumber, int row, RouteSectorType type, WorldRuleData worldRule)
    {
        SectorNumber = sectorNumber;
        Row = row;
        Type = type;
        WorldRule = worldRule;
    }
}

public sealed class RunRouteGraph
{
    private const int MaxRows = 3;
    public int Level { get; }
    public int Rows { get; }
    public IReadOnlyList<RouteNode> Entries { get; }

    private RunRouteGraph(int level, RouteNode[] entries)
    {
        Level = level;
        Rows = entries.Length;
        Entries = entries;
    }

    internal static RunRouteGraph Generate(int level, WorldRuleData[] worldRules,
        RouteSectorType[] types, WorldRuleData initialRule, bool fixedEntryRule)
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
        RouteNode[] entries = null;
        RouteNode[] previous = null;
        for (int column = 0; column < RunRoute.SectorsPerLevel; column++)
        {
            var options = new List<WorldRuleData>(pool);
            var columnTypes = new List<RouteSectorType>(ordinaryTypes);
            if (column == 0 && initialRule != null)
                options.RemoveAll(rule => rule.Id == initialRule.Id);

            var nodes = new RouteNode[rows];
            int sector = (level - 1) * RunRoute.SectorsPerLevel + column + 1;
            for (int row = 0; row < rows; row++)
            {
                WorldRuleData rule = column == 0 && (fixedEntryRule || row == 0) && initialRule != null
                    ? initialRule : TakeRandom(options);
                RouteSectorType type = Array.Find(types,
                    candidate => candidate.MatchingWorldRule != null && candidate.MatchingWorldRule == rule);
                type ??= TakeRandom(columnTypes);
                nodes[row] = new RouteNode(sector, row, type, rule);
            }

            if (previous == null) entries = nodes;
            else
            {
                int branchRow = rows > 1 ? Random.Range(0, rows - 1) : -1;
                for (int row = 0; row < rows; row++)
                {
                    // Every lane reaches the end; each boundary also has a fork when there is more than one lane.
                    previous[row].Next = row == branchRow
                        ? new[] { nodes[row], nodes[row + 1] } : new[] { nodes[row] };
                }
            }
            previous = nodes;
        }
        return new RunRouteGraph(level, entries);
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
