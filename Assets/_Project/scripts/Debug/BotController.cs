#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

// No Unity lifecycle or physics ownership: a session supplies observations and consumes intent.
public sealed class BotController : IDisposable
{
    private readonly CharacterMovement2D movement;
    private readonly GameplayAreaService area;
    private readonly Func<Vector2> intent;
    private Vector2 direction;
    public string State { get; private set; } = "Surviving";

    public BotController(CharacterMovement2D movement, GameplayAreaService area)
    {
        if (movement.MovementIntent != null)
            throw new InvalidOperationException("Movement intent already has an owner.");
        this.movement = movement;
        this.area = area;
        intent = () => direction;
        movement.MovementIntent = intent;
    }

    public void Pause(string state) { direction = Vector2.zero; State = state; }

    public void Tick(float elapsed)
    {
        Vector2 position = movement.transform.position;
        Vector2 avoidance = Vector2.zero;
        foreach (var enemy in EnemyHealth.ActiveInstances)
        {
            if (enemy == null || enemy.IsDead) continue;
            Vector2 away = position - (Vector2)enemy.transform.position;
            float distance = away.magnitude;
            if (distance < 5f)
                avoidance += away.normalized * Mathf.Pow(1f - distance / 5f, 2f) * 3f;
        }
        avoidance = Vector2.ClampMagnitude(avoidance, 3f);

        // Ordinary exploration has no timed win. After 60 gameplay seconds seek the real exit.
        bool exiting = elapsed >= 60f;
        Vector2 attraction = Vector2.zero;
        float nearest = 12f * 12f;
        if (!exiting)
        {
            foreach (var pickup in ExperiencePickup.DebugActive)
            {
                if (pickup == null) continue;
                Vector2 delta = (Vector2)pickup.transform.position - position;
                if (delta.sqrMagnitude < nearest && area.IsInsidePlayableArea(pickup.transform.position))
                { nearest = delta.sqrMagnitude; attraction = delta.normalized * 1.3f; }
            }
        }
        else
        {
            nearest = float.PositiveInfinity;
            foreach (var exit in ProductionSectorExit.ActiveExits)
            {
                if (exit == null || !exit.IsMapVisible) continue;
                Vector2 delta = (Vector2)exit.transform.position - position;
                if (delta.sqrMagnitude < nearest)
                { nearest = delta.sqrMagnitude; attraction = delta.normalized * 2f; }
            }
        }
        Vector2 center = area.PlayableArea.bounds.center;
        Vector2 bias = area.IsInsidePlayableArea(position, 3f)
            ? (center - position).normalized * .12f : (center - position).normalized * 3f;
        Vector2 wander = new Vector2(Mathf.Cos(elapsed * .7f), Mathf.Sin(elapsed * .53f)) * .45f;
        direction = Vector2.ClampMagnitude(avoidance + attraction + bias + wander, 1f);
        State = exiting ? "Seeking sector exit" : attraction.sqrMagnitude > 0f ? "Collecting XP" : "Surviving";
    }

    public void Dispose()
    {
        direction = Vector2.zero;
        if (movement != null && movement.MovementIntent == intent) movement.MovementIntent = null;
    }
}
#endif
