using UnityEngine;
public sealed class CorridorCollapse
{
    private readonly CorridorRoute route;
    private readonly CorridorSettings settings;
    private float cooldown;
    public float Distance { get; private set; }
    public bool UnderPressure { get; private set; }
    public CorridorCollapse(CorridorRoute route, CorridorSettings settings) { this.route = route; this.settings = settings; }
    public void Tick(float deltaTime, bool active, bool finalPush)
    {
        cooldown -= deltaTime;
        if (active) Distance = Mathf.Min(route.Length, Distance + deltaTime * settings.collapseSpeed *
            (finalPush ? settings.finalCollapseMultiplier : 1));
    }
    public bool TryPressure(Vector2 position, bool active, out float damage)
    {
        damage = settings.collapseDamage;
        UnderPressure = active && route.Project(position) <= Distance + .5f;
        return UnderPressure && damage > 0 && cooldown <= 0;
    }
    public void DamageApplied() => cooldown = Mathf.Max(.65f, settings.collapseDamageInterval);
}
