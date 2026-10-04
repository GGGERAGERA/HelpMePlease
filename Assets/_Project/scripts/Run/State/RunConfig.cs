using System;
using UnityEngine;

[Serializable]
public sealed class RunPrefabWeight
{
    public UnityEngine.Object prefab;
    [Min(0f)] public float multiplier = 1f;
}

/// <summary>Authored inputs. Never mutated by a running game.</summary>
[Serializable]
public sealed class RunConfigParameters
{
    public ExplorationSectorConfig layoutProfile;
    public EnemySpawnProfile spawnProfile;
    public RunThreatConfig threatProfile;
    [Tooltip("Initial location world rule. Subsequent locations use their selected rule.")]
    public WorldRuleData worldRule;
    public LocalAnomalyData localAnomaly;
    [Range(0f, 100f)] public float initialThreat;
    [Min(0f)] public float threatGrowth = 1f;
    [Min(0.1f)] public float enemyHealth = 1f;
    [Min(0.1f)] public float spawnPressure = 1f;
    [Min(0.1f)] public float experience = 1f;
    [Min(0.1f)] public float gold = 1f;
    [Min(0.1f)] public float eventFrequency = 1f;
    public RunPrefabWeight[] enemyWeights = Array.Empty<RunPrefabWeight>();
    public RunPrefabWeight[] eventWeights = Array.Empty<RunPrefabWeight>();
    public RunPrefabWeight[] anomalyWeights = Array.Empty<RunPrefabWeight>();
    public string biomeId;
    public string themeId;
}

/// <summary>Per-run snapshot consumed by gameplay. Contains no map definition or UI reference.</summary>
public sealed class RunConfig
{
    public ResolvedRunContent Content { get; } = ResolvedRunContent.Empty;
    public static RunConfig Default { get; } = new(null, null, new RunConfigParameters());
    public string MapId { get; }
    public string SectorId { get; }
    public string BiomeId { get; }
    public string ThemeId { get; }
    public ExplorationSectorConfig LayoutProfile { get; }
    public EnemySpawnProfile SpawnProfile { get; }
    public RunThreatConfig ThreatProfile { get; }
    public WorldRuleData WorldRule { get; }
    public LocalAnomalyData LocalAnomaly { get; }
    public float InitialThreat { get; }
    public float ThreatGrowth { get; }
    public float EnemyHealth { get; }
    public float SpawnPressure { get; }
    public float Experience { get; }
    public float Gold { get; }
    public float EventFrequency { get; }
    private readonly RunPrefabWeight[] enemyWeights, eventWeights, anomalyWeights;

    public RunConfig(string mapId, string sectorId, RunConfigParameters parameters)
    {
        var p = parameters ?? throw new ArgumentNullException(nameof(parameters));
        MapId = mapId; SectorId = sectorId; BiomeId = p.biomeId; ThemeId = p.themeId;
        LayoutProfile = p.layoutProfile; SpawnProfile = p.spawnProfile; ThreatProfile = p.threatProfile;
        WorldRule = p.worldRule; LocalAnomaly = p.localAnomaly;
        InitialThreat = Mathf.Clamp(p.initialThreat, 0f, 100f);
        ThreatGrowth = Mathf.Max(0f, p.threatGrowth); EnemyHealth = Mathf.Max(.1f, p.enemyHealth);
        SpawnPressure = Mathf.Max(.1f, p.spawnPressure); Experience = Mathf.Max(.1f, p.experience);
        Gold = Mathf.Max(.1f, p.gold); EventFrequency = Mathf.Max(.1f, p.eventFrequency);
        enemyWeights = Copy(p.enemyWeights); eventWeights = Copy(p.eventWeights); anomalyWeights = Copy(p.anomalyWeights);
    }
    internal RunConfig WithContent(ResolvedRunContent content) => new(this, content);
    private RunConfig(RunConfig basis, ResolvedRunContent content)
    {
        MapId = basis.MapId; SectorId = basis.SectorId; BiomeId = basis.BiomeId; ThemeId = basis.ThemeId;
        LayoutProfile = basis.LayoutProfile; SpawnProfile = basis.SpawnProfile; ThreatProfile = basis.ThreatProfile;
        WorldRule = basis.WorldRule; LocalAnomaly = basis.LocalAnomaly; InitialThreat = basis.InitialThreat;
        ThreatGrowth = basis.ThreatGrowth * content.ThreatGrowth; EnemyHealth = basis.EnemyHealth;
        SpawnPressure = basis.SpawnPressure * content.SpawnPressure; Experience = basis.Experience * content.Experience;
        Gold = basis.Gold * content.Gold; EventFrequency = basis.EventFrequency;
        enemyWeights = basis.enemyWeights; eventWeights = basis.eventWeights; anomalyWeights = basis.anomalyWeights;
        Content = content;
    }
    private static RunPrefabWeight[] Copy(RunPrefabWeight[] source)
    {
        if (source == null) return Array.Empty<RunPrefabWeight>();
        var result = new RunPrefabWeight[source.Length];
        for (int i = 0; i < source.Length; i++)
            if (source[i] != null) result[i] = new RunPrefabWeight { prefab = source[i].prefab, multiplier = Mathf.Max(0f, source[i].multiplier) };
        return result;
    }
    private static float Weight(UnityEngine.Object prefab, RunPrefabWeight[] weights)
    {
        foreach (var entry in weights) if (entry != null && entry.prefab == prefab) return entry.multiplier;
        return 1f;
    }
    public float EnemyWeight(GameObject prefab) => Weight(prefab, enemyWeights);
    public float EventWeight(WorldEvent prefab) => Weight(prefab, eventWeights);
    public float AnomalyWeight(LocalAnomalyData data) => Weight(data, anomalyWeights);
}
