using System.Collections.Generic;
using UnityEngine;

/// <summary>Player-facing summaries of the same resolved config used to launch the expedition.</summary>
public static class SurfaceMapBriefing
{
    public static string Text(string key) => LocalizationService.Instance != null ? LocalizationService.Instance.Get(key) : key;
    public static string Format(string key, params object[] args) => LocalizationService.Instance != null
        ? string.Format(Text(key), args) : string.Join(" / ", args);

    // Account for the rule actually applied on entry.
    public static float EntryPressure(RunConfig config) => config.SpawnPressure * (config.WorldRule != null && config.WorldRule.RuleType != WorldRuleType.None ? config.WorldRule.SpawnPressureMultiplier : 1f);
    public static float EntryHealth(RunConfig config) => config.EnemyHealth * (config.WorldRule != null ? config.WorldRule.EnemyHealthMultiplier : 1f);
    public static string RiskDrivers(RunConfig config)
    {
        var drivers=new List<string>();
        if(!Mathf.Approximately(config.ThreatGrowth,1))drivers.Add(Bonus("map.threat.growth",config.ThreatGrowth));
        if(!Mathf.Approximately(EntryHealth(config),1))drivers.Add(Bonus("map.enemy.hp",EntryHealth(config)));
        if(drivers.Count<2 && !Mathf.Approximately(EntryPressure(config),1))drivers.Add(Bonus("map.spawn.pressure",EntryPressure(config)));
        return string.Join("\n",drivers);
    }

    // Production sites sample this pool. The legacy LocalAnomaly field does not describe their distribution.
    public static List<string> AnomalyBias(RunConfig config)
    {
        var result=new List<string>(); var pool=config.LayoutProfile?.NormalAnomalies;
        if(pool==null || pool.Length==0)return result;
        float total=0; int count=0;
        foreach(var candidate in pool)if(candidate!=null){total+=config.AnomalyWeight(candidate);count++;}
        if(total<=0 || count==0)return result;
        var seen=new HashSet<LocalAnomalyData>();
        foreach(var candidate in pool)
        {
            if(candidate==null || !seen.Add(candidate) || candidate.ZonePrefab==null ||
                config.AnomalyWeight(candidate)/total<=1f/count+.0001f)continue;
            string key="map.anomaly.bias."+candidate.Id, label=Text(key);
            if(label==key)label=Format("map.anomaly.bias",candidate.Presentation.Title);
            result.Add(label);
        }
        return result;
    }

    public static float EntryExperience(RunConfig config) => config.Experience*(config.WorldRule!=null?config.WorldRule.SectorExperienceMultiplier:1f);
    public static bool HasRewardBonus(RunConfig config) => config.Gold>1f || EntryExperience(config)>1f;

    public static string Rewards(RunConfig config)
    {
        // One primary benefit keeps the compact card comparable across all profiles.
        if (!Mathf.Approximately(config.Gold, 1f)) return Bonus("map.gold", config.Gold);
        float entryExperience=EntryExperience(config);
        return !Mathf.Approximately(entryExperience, 1f) ? Bonus("map.experience", entryExperience) : Text("map.reward.standard");
    }

    public static string NodeReward(RunConfig config)
    {
        if (!Mathf.Approximately(config.Gold, 1f)) return Bonus("map.gold", config.Gold);
        return !Mathf.Approximately(config.Experience, 1f) ? Bonus("map.experience", config.Experience) : Text("map.reward.standard");
    }

    private static string Bonus(string key, float multiplier)
    {
        int percent = Mathf.RoundToInt((multiplier - 1f) * 100f);
        return Text(key) + " " + (percent >= 0 ? "+" : "") + percent + "%";
    }

    public static string Feature(RunConfig config)
    {
        if (config.WorldRule != null && config.WorldRule.RuleType != WorldRuleType.None)
            return Format("map.feature.start_rule", config.WorldRule.DisplayName);
        var anomalyBias=AnomalyBias(config);
        if(anomalyBias.Count>0)return string.Join(" / ",anomalyBias);
        if (config.ThreatGrowth < 1f) return Text("map.feature.slow_threat");
        if (EntryPressure(config) > 1f) return Text("map.feature.more_enemies");
        return string.Empty;
    }
}
