using UnityEngine;
public enum CorridorStrikePattern { Single, SideGap, CrossBlock, Chase }
// Reuses the project's rocket telegraph, flight, impact, damage and pools.
public sealed class CorridorStrikes : System.IDisposable
{
    private readonly RocketAttackRunner runner;
    private readonly System.Collections.Generic.List<CorridorStrikeThreat> threats = new();
    public System.Collections.Generic.IReadOnlyList<CorridorStrikeThreat> Threats => threats.AsReadOnly();
    private bool disposed;
    private readonly CorridorSettings settings;
    private readonly CorridorRoute route;
    private float timer, chaseTimer;
    private int nextPattern, chaseIndex = -1;
    private bool crossFromLeft;
    private float chaseStart;
    public CorridorStrikePattern Pattern { get; private set; }
    public int Launched { get; private set; }
    public int Pending => runner.PendingCount;
    private float Radius => Mathf.Clamp(settings.strikeRadius, .5f, route.HalfWidth * .36f);
    public CorridorStrikes(MonoBehaviour owner, CorridorRoute route, CorridorSettings settings,
        GameObject rocket, GameObject marker, ParticleSystem explosion)
    {
        this.route = route; this.settings = settings;
        runner = new RocketAttackRunner(owner, rocket, marker, explosion);
        timer = settings.strikeInterval;
    }
    public void Tick(float delta, Vector2 player, bool active, bool finalPush, System.Func<bool> combatAllowed)
    {
        if (disposed) return;
        for (int i = threats.Count-1; i >= 0; i--)
        { var t = threats[i]; if (t.Remaining <= delta) threats.RemoveAt(i);
          else threats[i] = new CorridorStrikeThreat(t.Position,t.Radius,t.Remaining-delta); }
        int pending = runner.PendingCount;
        runner.Tick(delta, 14f, Radius, settings.strikeDamage, combatAllowed, true);
        if (runner.PendingCount < pending) CameraShake.Instance?.Shake(.1f, .06f);
        if (!active) return; // First segment is quiet.
        if (chaseIndex >= 0)
        {
            chaseTimer -= delta;
            if (chaseTimer > 0) return;
            Launch(route.Point(Mathf.Clamp(chaseStart + chaseIndex * 2.5f, 1, route.Length - 2), out _));
            chaseTimer = Mathf.Max(.1f, settings.chaseSpacingTime);
            if (++chaseIndex == 3) chaseIndex = -1;
            return;
        }
        timer -= delta;
        if (timer > 0 || runner.PendingCount > 0) return;
        Pattern = (CorridorStrikePattern)(nextPattern++ % 4);
        StartPattern(Pattern, player);
        timer = Mathf.Max(.5f, settings.strikeInterval) * (finalPush ? settings.finalStrikeIntervalMultiplier : 1f);
    }
    public void StartPattern(CorridorStrikePattern pattern, Vector2 player)
    {
        Pattern = pattern;
        float along = route.Project(player, out _);
        float ahead = Mathf.Max(7f, 6f * (settings.strikeTelegraphTime + settings.strikeFallTime));
        Vector2 center = route.Point(Mathf.Min(along + ahead, route.Length - 2), out Vector2 forward);
        Vector2 side = new(-forward.y, forward.x);
        float edge = Mathf.Max(.5f, route.HalfWidth - Radius);
        switch (pattern)
        {
            case CorridorStrikePattern.Single: Launch(center); break;
            case CorridorStrikePattern.SideGap:
                Launch(center - side * edge); Launch(center + side * edge); break;
            case CorridorStrikePattern.CrossBlock:
                crossFromLeft = !crossFromLeft;
                float sign = crossFromLeft ? -1 : 1;
                Launch(center + side * edge * sign);
                Launch(center + side * Mathf.Max(0f, edge - Radius * 1.5f) * sign);
                break;
            case CorridorStrikePattern.Chase:
                chaseStart = Mathf.Max(1, along - 5f); chaseIndex = 0; chaseTimer = 0; break;
        }
    }
    private void Launch(Vector2 position)
    {
        if (runner.Launch(position, null, Mathf.Max(.6f, settings.strikeTelegraphTime),
            Mathf.Max(.1f, settings.strikeFallTime), Radius))
        { Launched++; threats.Add(new CorridorStrikeThreat(position, Radius, settings.strikeTelegraphTime+settings.strikeFallTime)); }
    }
    public void Cancel() { chaseIndex = -1; threats.Clear(); runner.Cancel(); }
    public void Dispose() { if (disposed) return; disposed = true; Cancel(); runner.Dispose(); }
}
