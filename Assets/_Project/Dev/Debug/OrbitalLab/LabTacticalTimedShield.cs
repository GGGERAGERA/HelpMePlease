#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

// Preserve seeded manual spawning and lab pause; the timer itself is shared with production.
public sealed class LabTacticalTimedShield : EnemyTacticalTimedShield
{
    EnemyOrbitalLabController lab;
    protected override bool ShouldTick => lab != null && !lab.Paused;
    public static void Attach(GameObject enemy, EnemyDirectionalShield source, EnemyOrbitalLabController owner, float delay)
    {
        if (source == null) return;
        var timer = enemy.AddComponent<LabTacticalTimedShield>();
        timer.lab = owner;
        timer.Initialize(source, delay);
    }
}
#endif
