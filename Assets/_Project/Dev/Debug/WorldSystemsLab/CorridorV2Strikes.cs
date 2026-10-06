#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
public enum CorridorV2StrikePattern { Single, SideGap, CrossBlock, Chase }
// Reuses the project's rocket telegraph, flight, impact, damage and pools.
public sealed class CorridorV2Strikes : System.IDisposable
{
    private readonly RocketAttackRunner runner;
    private readonly CorridorV2Settings settings;
    private readonly CorridorV2Route route;
    private float timer, chaseTimer;
    private int nextPattern, chaseIndex = -1;
    private bool crossFromLeft;
    private float chaseStart;
    public CorridorV2StrikePattern Pattern { get; private set; }
    public int Launched { get; private set; }
    public int Pending => runner.PendingCount;
    private float Radius => Mathf.Clamp(settings.strikeRadius, .5f, route.HalfWidth * .36f);
    public CorridorV2Strikes(MonoBehaviour owner, CorridorV2Route route, CorridorV2Settings settings,
        GameObject rocket, GameObject marker, ParticleSystem explosion)
    {
        this.route = route; this.settings = settings;
        runner = new RocketAttackRunner(owner, rocket, marker, explosion);
        timer = settings.strikeInterval;
    }
    public void Tick(float delta, Vector2 player, bool active, bool finalPush, System.Func<bool> combatAllowed)
    {
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
        Pattern = (CorridorV2StrikePattern)(nextPattern++ % 4);
        StartPattern(Pattern, player);
        timer = Mathf.Max(.5f, settings.strikeInterval) * (finalPush ? settings.finalStrikeIntervalMultiplier : 1f);
    }
    public void StartPattern(CorridorV2StrikePattern pattern, Vector2 player)
    {
        Pattern = pattern;
        float along = route.Project(player, out _);
        float ahead = Mathf.Max(7f, 6f * (settings.strikeTelegraphTime + settings.strikeFallTime));
        Vector2 center = route.Point(Mathf.Min(along + ahead, route.Length - 2), out Vector2 forward);
        Vector2 side = new(-forward.y, forward.x);
        float edge = Mathf.Max(.5f, route.HalfWidth - Radius);
        switch (pattern)
        {
            case CorridorV2StrikePattern.Single: Launch(center); break;
            case CorridorV2StrikePattern.SideGap:
                Launch(center - side * edge); Launch(center + side * edge); break;
            case CorridorV2StrikePattern.CrossBlock:
                crossFromLeft = !crossFromLeft;
                float sign = crossFromLeft ? -1 : 1;
                Launch(center + side * edge * sign);
                Launch(center + side * Mathf.Max(0f, edge - Radius * 1.5f) * sign);
                break;
            case CorridorV2StrikePattern.Chase:
                chaseStart = Mathf.Max(1, along - 5f); chaseIndex = 0; chaseTimer = 0; break;
        }
    }
    private void Launch(Vector2 position)
    {
        if (runner.Launch(position, null, Mathf.Max(.6f, settings.strikeTelegraphTime),
            Mathf.Max(.1f, settings.strikeFallTime), Radius)) Launched++;
    }
    public void Cancel() => runner.Cancel();
    public void Dispose() => runner.Dispose();
}
#endif
