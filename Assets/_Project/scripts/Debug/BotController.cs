#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using Subject42.Combat.OrbitalStation;
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

    private WorldEvent objective;
    private bool objectiveCompleted;
    private EnemyProjectile[] goldenProjectiles = Array.Empty<EnemyProjectile>();
    public void ObserveGoldenProjectiles() => goldenProjectiles = UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
    public void OnObjectiveCompleted() => objectiveCompleted = true;
    public void TickGoldenPath(float elapsed, EnemyHealth boss, WorldEventSpawner events, OrbitalRunState orbital)
    {
        Vector2 position = movement.transform.position;
        if (objective != null && objective.IsCompleted && !objective.IsFailed) objectiveCompleted = true;
        Vector2? target = null;
        if (boss != null && !boss.IsDead)
        {
            Vector2 delta = (Vector2)boss.transform.position - position;
            Vector2 tangent = new(-delta.y, delta.x);
            float swordRadius = orbital.Modules.Where(m => m.ModuleType == OrbitalModuleKind.LaserSword)
                .Select(m => orbital.Rings.First(r => r.StableRingId == m.StableRingId).Radius).DefaultIfEmpty(0f).Max();
            float combatRadius = swordRadius > 0f ? Mathf.Max(2.5f, swordRadius + .25f) : 4.5f;
            Vector2 desired = delta.normalized * Mathf.Clamp((delta.magnitude - combatRadius) * 2f, -2f, 2f) + tangent.normalized;
            if (!area.IsInsidePlayableArea(position, 10f))
                desired = ((Vector2)area.PlayableArea.bounds.center - position).normalized * 4f - delta.normalized;
            direction = SafeGoldenDirection(position, desired);
            State = "Fighting boss with production ORBITAL weapons";
            return;
        }
        if (!objectiveCompleted && events != null)
        {
            if (objective == null || objective.IsFailed)
                objective = events.SpawnedEvents.Where(e => e != null && !e.IsCompleted)
                    .OrderBy(e => e is CaptureZoneEvent ? 0 : 1)
                    .ThenBy(e => ((Vector2)e.transform.position - position).sqrMagnitude).FirstOrDefault();
            if (objective != null)
            {
                if (objective.CanInteract) objective.Interact();
                target = objective.transform.position;
                if (objective is CaptureZoneEvent capture && capture.IsStarted)
                {
                    Vector2 radial = position - (Vector2)capture.transform.position;
                    float radius = capture.CaptureRadius * .65f;
                    Vector2 tangent = new(-radial.y, radial.x);
                    Vector2 desired = tangent.normalized + radial.normalized * Mathf.Clamp(radius - radial.magnitude, -1.5f, 1.5f);
                    if (radial.sqrMagnitude < .1f) desired = Vector2.right;
                    direction = SafeGoldenDirection(position, desired);
                    State = "Holding capture zone while circling";
                    return;
                }
                var markers = new System.Collections.Generic.List<TacticalMapMarkerDescriptor>();
                objective.CollectTacticalMapMarkers(markers);
                var targets = markers.Where(m => m.Kind == TacticalMapMarkerKind.Target).OrderBy(m => (m.Position - position).sqrMagnitude).ToArray();
                if (targets.Length > 0) target = targets[0].Position;
            }
        }
        if (target.HasValue)
        {
            Vector2 delta = target.Value - position;
            Vector2 avoid = Vector2.zero;
            foreach (var enemy in EnemyHealth.ActiveInstances)
            {
                if (enemy == null || enemy.IsDead) continue;
                Vector2 away = position - (Vector2)enemy.transform.position;
                if (away.magnitude < 2.5f) avoid += away.normalized * (1f - away.magnitude / 2.5f);
            }
            Vector2 desired = delta.magnitude > .6f ? delta.normalized + Vector2.ClampMagnitude(avoid, .6f) : Vector2.ClampMagnitude(avoid, .6f);
            direction = ChooseSafeHeading(position, desired + AvoidEarlyExit(position));
            State = "Completing objective: " + objective.EventDisplayName;
            return;
        }
        Tick(elapsed);
        direction = ChooseSafeHeading(position, direction);
    }

    private Vector2 SafeGoldenDirection(Vector2 position, Vector2 desired)
    {
        Vector2 avoidance = Vector2.zero;
        foreach (var enemy in EnemyHealth.ActiveInstances)
        {
            if (enemy == null || enemy.IsDead) continue;
            Vector2 away = position - (Vector2)enemy.transform.position;
            float radius = enemy.IsBoss ? 1.2f : 4f;
            if (away.magnitude < radius) avoidance += away.normalized * Mathf.Pow(1f - away.magnitude / radius, 2) * 5f;
        }
        Vector2 edge = area.IsInsidePlayableArea(position, 8f) ? Vector2.zero
            : ((Vector2)area.PlayableArea.bounds.center - position).normalized * 3f;
        return ChooseSafeHeading(position, desired + Vector2.ClampMagnitude(avoidance, 3f) + edge + AvoidEarlyExit(position));
    }

    // Fixed set of headings and short lookahead; no search tree, world edits or combat shortcuts.
    private Vector2 ChooseSafeHeading(Vector2 position, Vector2 desired)
    {
        Vector2 best = desired.normalized;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI / 8f;
            Vector2 candidate = new(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 displacement = movement.DebugForecastDisplacement(candidate, .35f);
            Vector2 future = position + displacement;
            float score = Vector2.Dot(candidate, desired.normalized) * 2f + Vector2.Dot(candidate, direction) * .4f;
            if (!area.IsInsidePlayableArea(future, 1f)) score -= 40f;
            if (!objectiveCompleted)
                foreach (var exit in ProductionSectorExit.ActiveExits)
                    if (exit != null && exit.IsAvailable && Vector2.Distance(future, exit.transform.position) < exit.GetComponent<CircleCollider2D>().radius + 1.5f)
                        score -= 100f;
            if (!objectiveCompleted && objective is CaptureZoneEvent capture && capture.IsStarted)
                score -= Mathf.Max(0f, Vector2.Distance(future, capture.transform.position) - capture.CaptureRadius * .85f) * 2f;
            foreach (var enemy in EnemyHealth.ActiveInstances)
            {
                if (enemy == null || enemy.IsDead) continue;
                Vector2 current = enemy.transform.position;
                if ((current - position).sqrMagnitude > 100f) continue;
                var body = enemy.GetComponent<Rigidbody2D>();
                Vector2 enemyVelocity = body != null ? body.linearVelocity : Vector2.zero;
                Vector2 offset = current - position;
                Vector2 relative = enemyVelocity - displacement / .35f;
                float closestTime = relative.sqrMagnitude > .01f ? Mathf.Clamp(-Vector2.Dot(offset, relative) / relative.sqrMagnitude, 0f, .5f) : 0f;
                float distance = (offset + relative * closestTime).magnitude;
                // A pursuer turns towards the player; extrapolating only its old velocity
                // wrongly rates cutting across its path as safe.
                Vector2 chasing = Vector2.MoveTowards(current, future, Mathf.Max(4f, enemyVelocity.magnitude) * .35f);
                if (!enemy.IsBoss) distance = Mathf.Min(distance, Vector2.Distance(future, chasing));
                float clearance = enemy.IsBoss ? 1.1f : 3.2f;
                score -= Mathf.Max(0f, clearance - distance) * 12f;
                if (enemy.IsBoss)
                    foreach (var collider in enemy.GetComponents<Collider2D>())
                    {
                        Vector2 relativeFuture = future - enemyVelocity * .35f;
                        float gap = Vector2.Distance(relativeFuture, collider.ClosestPoint(relativeFuture));
                        score -= Mathf.Max(0f, .9f - gap) * 80f;
                    }
            }
            foreach (var projectile in goldenProjectiles)
            {
                if (projectile == null || !projectile.isActiveAndEnabled) continue;
                Vector2 offset = (Vector2)projectile.transform.position - position;
                if (offset.sqrMagnitude > 144f) continue;
                Vector2 relative = projectile.DebugTravelVelocity - displacement / .35f;
                float time = relative.sqrMagnitude > .01f ? Mathf.Clamp(-Vector2.Dot(offset, relative) / relative.sqrMagnitude, 0f, .6f) : 0f;
                float distance = (offset + relative * time).magnitude;
                score -= Mathf.Max(0f, 1.3f - distance) * 18f;
            }
            if (score > bestScore) { bestScore = score; best = candidate; }
        }
        return best;
    }

    private Vector2 AvoidEarlyExit(Vector2 position)
    {
        if (objectiveCompleted) return Vector2.zero;
        Vector2 steer = Vector2.zero;
        foreach (var exit in ProductionSectorExit.ActiveExits)
        {
            if (exit == null || !exit.IsAvailable) continue;
            Vector2 away = position - (Vector2)exit.transform.position;
            float clearance = exit.GetComponent<CircleCollider2D>().radius + 3f;
            if (away.magnitude < clearance + 3f)
                steer += away.normalized * Mathf.Clamp01((clearance + 3f - away.magnitude) / 3f) * 6f +
                    new Vector2(-away.y, away.x).normalized;
        }
        return steer;
    }
}
#endif
