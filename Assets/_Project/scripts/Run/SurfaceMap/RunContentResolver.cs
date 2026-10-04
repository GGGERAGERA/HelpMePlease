using System;
using System.Collections.Generic;
using UnityEngine;

// Gameplay contract: no sector definitions, map services or UI references.
public sealed class RunContentObjective
{
    public string Id { get; }
    public WorldEvent RequiredEvent { get; }
    public string EventTag { get; }
    public RunContentObjective(string id, WorldEvent requiredEvent, string eventTag)
    { Id = id; RequiredEvent = requiredEvent; EventTag = eventTag; }
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
    public static ResolvedRunContent Empty { get; } = new(new(), new(), 1, 1, 1, 1);
    internal ResolvedRunContent(List<WorldEvent> events, List<RunContentObjective> objectives, float xp, float gold, float threat, float pressure)
    { GuaranteedEvents = Array.AsReadOnly(events.ToArray()); Objectives = Array.AsReadOnly(objectives.ToArray());
      Experience = xp; Gold = gold; ThreatGrowth = threat; SpawnPressure = pressure; }
}
public static class RunContentResolver
{
    public static RunConfig Resolve(RunConfig basis, IEnumerable<(string objectiveId, SurfaceSectorContent content)> activeContent)
    {
        var events = new List<WorldEvent>(); var objectives = new List<RunContentObjective>();
        float xp = 1, gold = 1, threat = 1, pressure = 1;
        foreach (var entry in activeContent)
        {
            var c = entry.content;
            if (c.requiredEvent != null && !events.Contains(c.requiredEvent)) events.Add(c.requiredEvent);
            objectives.Add(new RunContentObjective(entry.objectiveId, c.requiredEvent, c.requiredEventTag));
            xp *= Mathf.Max(.1f, c.experienceMultiplier); gold *= Mathf.Max(.1f, c.goldMultiplier);
            threat *= Mathf.Max(0, c.threatGrowthMultiplier); pressure *= Mathf.Max(.1f, c.spawnPressureMultiplier);
        }
        return basis.WithContent(new ResolvedRunContent(events, objectives, xp, gold, threat, pressure));
    }
}
