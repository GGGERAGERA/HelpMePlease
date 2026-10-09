using System;
using System.Collections.Generic;
using UnityEngine;

// Gameplay contract: no sector definitions, map services or UI references.
public sealed class RunContentObjective
{
    public string Id { get; }
    public WorldEvent RequiredEvent { get; }
    public string EventTag { get; }
    internal float Experience { get; }
    internal float Gold { get; }
    internal float ThreatGrowth { get; }
    internal float SpawnPressure { get; }
    public RunContentObjective(string id, WorldEvent requiredEvent, string eventTag)
        : this(id, requiredEvent, eventTag, 1, 1, 1, 1) { }
    internal RunContentObjective(string id, WorldEvent requiredEvent, string eventTag,
        float experience, float gold, float threatGrowth, float spawnPressure)
    {
        Id = id; RequiredEvent = requiredEvent; EventTag = eventTag;
        Experience = Mathf.Max(.1f, experience); Gold = Mathf.Max(.1f, gold);
        ThreatGrowth = Mathf.Max(0, threatGrowth); SpawnPressure = Mathf.Max(.1f, spawnPressure);
    }
    public bool Matches(WorldEvent source, string tag) =>
        (RequiredEvent != null || !string.IsNullOrEmpty(EventTag)) &&
        (RequiredEvent == null || RequiredEvent == source) &&
        (string.IsNullOrEmpty(EventTag) || EventTag == tag);
}
public sealed class ResolvedRunContent
{
    public IReadOnlyList<WorldEvent> GuaranteedEvents { get; }
    public IReadOnlyList<RunContentObjective> Objectives { get; }
    public float Experience { get; }
    public float Gold { get; }
    public float ThreatGrowth { get; }
    public float SpawnPressure { get; }
    public static ResolvedRunContent Empty { get; } = FromObjectives(new());
    private ResolvedRunContent(List<WorldEvent> events, List<RunContentObjective> objectives, float xp, float gold, float threat, float pressure)
    { GuaranteedEvents = Array.AsReadOnly(events.ToArray()); Objectives = Array.AsReadOnly(objectives.ToArray());
      Experience = xp; Gold = gold; ThreatGrowth = threat; SpawnPressure = pressure; }

    internal static ResolvedRunContent FromObjectives(List<RunContentObjective> objectives)
    {
        var events = new List<WorldEvent>();
        float xp = 1, gold = 1, threat = 1, pressure = 1;
        foreach (var objective in objectives)
        {
            if (objective.RequiredEvent != null && !events.Contains(objective.RequiredEvent)) events.Add(objective.RequiredEvent);
            xp *= objective.Experience; gold *= objective.Gold;
            threat *= objective.ThreatGrowth; pressure *= objective.SpawnPressure;
        }
        return new ResolvedRunContent(events, objectives, xp, gold, threat, pressure);
    }
    internal ResolvedRunContent WithoutUnavailableEvents()
    {
        var available = new List<RunContentObjective>();
        foreach (var objective in Objectives)
            if (objective.RequiredEvent == null || objective.RequiredEvent.AvailableInProduction) available.Add(objective);
        return available.Count == Objectives.Count ? this : FromObjectives(available);
    }
}
public static class RunContentResolver
{
    public static RunConfig Resolve(RunConfig basis, IEnumerable<(string objectiveId, SurfaceSectorContent content)> activeContent)
    {
        var objectives = new List<RunContentObjective>();
        foreach (var entry in activeContent)
        {
            var c = entry.content;
            if (c == null || !c.AvailableInProduction) continue;
            objectives.Add(new RunContentObjective(entry.objectiveId, c.requiredEvent, c.requiredEventTag,
                c.experienceMultiplier, c.goldMultiplier, c.threatGrowthMultiplier, c.spawnPressureMultiplier));
        }
        return basis.WithContent(ResolvedRunContent.FromObjectives(objectives));
    }
}