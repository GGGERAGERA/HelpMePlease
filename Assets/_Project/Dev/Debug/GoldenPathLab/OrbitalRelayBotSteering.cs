#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Subject42.Combat.OrbitalStation;
using UnityEngine;

// Read-only orbital tracking. The normal movement/physics and body detector own the contact.
public static class OrbitalRelayBotSteering
{
    public static Vector2 GetDesiredMovement(OrbitalRelayEvent relay, OrbitalStationRuntime station, Vector2 playerPosition)
    {
        if (relay == null || station == null || !station.IsInitialized || relay.ActiveNode == null ||
            relay.Snapshot.Phase == OrbitalRelayPhase.Transition) return Vector2.zero;
        if (!relay.ActiveNode.TryGetContactCircle(out Vector2 target, out _)) return Vector2.zero;
        OrbitalModuleRuntime nearest = null;
        float distance = float.PositiveInfinity;
        foreach (var module in station.Modules)
        {
            if (module.CurrentMount == null) continue;
            float next = (module.WorldPosition - target).sqrMagnitude;
            if (next < distance) { distance = next; nearest = module; }
        }
        if (nearest == null) return Vector2.ClampMagnitude(target - playerPosition, 1f);
        var mount = nearest.CurrentMount;
        var ring = mount.Ring;
        float phase = ring.Phase + mount.LocalPhase;
        const float sample = .025f;
        Vector3 present = ring.Geometry.PositionDegrees(phase, ring.Radius);
        Vector3 future = ring.Geometry.PositionDegrees(phase + ring.RotationSpeed * ring.Direction * sample, ring.Radius);
        Vector2 orbitalVelocity = mount.Transform.parent.TransformVector((future - present) / sample);
        Vector2 desiredVelocity = (target - nearest.WorldPosition) * 8f - orbitalVelocity;
        var movement = station.Owner.Transform.GetComponent<CharacterMovement2D>();
        float speed = movement != null ? movement.speed : 5f;
        return Vector2.ClampMagnitude(desiredVelocity / Mathf.Max(.1f, speed), 1f);
    }
}
#endif
