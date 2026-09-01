using System.Collections.Generic;
using UnityEngine;

public enum CrowdMovementPreset
{
    Production,
    Direct,
    Spread,
    Swarm,
    Orbit,
    Encircle,
    Chaotic,
    Custom
}

public struct CrowdSteeringValues
{
    public bool Enabled;
    public float GlobalStrength;
    public float DirectPressure;
    public float SeparationRadius;
    public float SeparationStrength;
    public float CohesionRadius;
    public float CohesionStrength;
    public float OrbitStrength;
    public float OrbitDirectionBias;
    public float TargetOffsetRadius;
    public float TargetOffsetStrength;
    public float WanderStrength;
    public float WanderFrequency;
}

/// <summary>
/// Session-only settings for the F1 Crowd Movement Lab. Nothing is serialized,
/// so experimental values can never silently become production defaults.
/// </summary>
public static class CrowdSteeringRuntime
{
    private static readonly List<EnemyMovement> agents = new(256);
    private static CrowdSteeringValues values;

    public static CrowdMovementPreset CurrentPreset { get; private set; }
    public static CrowdSteeringValues Values => values;
    public static bool DebugDraw { get; private set; }
    public static int NeighbourLimit { get; set; } = 12;
    public static float NeighbourRefreshInterval { get; set; } = 0.12f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        agents.Clear();
        DebugDraw = false;
        NeighbourLimit = 12;
        NeighbourRefreshInterval = 0.12f;
        ApplyPreset(CrowdMovementPreset.Production);
    }

    static CrowdSteeringRuntime()
    {
        ApplyPreset(CrowdMovementPreset.Production);
    }

    public static void Register(EnemyMovement agent)
    {
        if (agent != null && !agents.Contains(agent))
            agents.Add(agent);
    }

    public static void Unregister(EnemyMovement agent)
    {
        agents.Remove(agent);
    }

    public static void SetDebugDraw(bool enabled) => DebugDraw = enabled;

    public static void SetEnabled(bool enabled) => Change((ref CrowdSteeringValues v) => v.Enabled = enabled);
    public static void SetGlobalStrength(float value) => Change((ref CrowdSteeringValues v) => v.GlobalStrength = Mathf.Clamp(value, 0f, 4f));
    public static void SetDirectPressure(float value) => Change((ref CrowdSteeringValues v) => v.DirectPressure = Mathf.Clamp(value, 0f, 5f));
    public static void SetSeparationRadius(float value) => Change((ref CrowdSteeringValues v) => v.SeparationRadius = Mathf.Clamp(value, 0f, 12f));
    public static void SetSeparationStrength(float value) => Change((ref CrowdSteeringValues v) => v.SeparationStrength = Mathf.Clamp(value, 0f, 5f));
    public static void SetCohesionRadius(float value) => Change((ref CrowdSteeringValues v) => v.CohesionRadius = Mathf.Clamp(value, 0f, 16f));
    public static void SetCohesionStrength(float value) => Change((ref CrowdSteeringValues v) => v.CohesionStrength = Mathf.Clamp(value, 0f, 5f));
    public static void SetOrbitStrength(float value) => Change((ref CrowdSteeringValues v) => v.OrbitStrength = Mathf.Clamp(value, 0f, 5f));
    public static void SetOrbitDirectionBias(float value) => Change((ref CrowdSteeringValues v) => v.OrbitDirectionBias = Mathf.Clamp(value, -1f, 1f));
    public static void SetTargetOffsetRadius(float value) => Change((ref CrowdSteeringValues v) => v.TargetOffsetRadius = Mathf.Clamp(value, 0f, 12f));
    public static void SetTargetOffsetStrength(float value) => Change((ref CrowdSteeringValues v) => v.TargetOffsetStrength = Mathf.Clamp(value, 0f, 5f));
    public static void SetWanderStrength(float value) => Change((ref CrowdSteeringValues v) => v.WanderStrength = Mathf.Clamp(value, 0f, 5f));
    public static void SetWanderFrequency(float value) => Change((ref CrowdSteeringValues v) => v.WanderFrequency = Mathf.Clamp(value, 0.02f, 4f));

    private delegate void ValuesMutation(ref CrowdSteeringValues target);

    private static void Change(ValuesMutation mutation)
    {
        mutation(ref values);
        CurrentPreset = CrowdMovementPreset.Custom;
    }

    public static void ApplyPreset(CrowdMovementPreset preset)
    {
        values = preset switch
        {
            CrowdMovementPreset.Direct => Make(true, 1f, 1.35f, .8f, .15f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, .3f),
            CrowdMovementPreset.Spread => Make(true, 1.25f, .9f, 3.2f, 2.8f, 0f, 0f, .15f, 0f, 5.5f, 1.8f, .15f, .25f),
            CrowdMovementPreset.Swarm => Make(true, 1.1f, 1f, 2.5f, 1.45f, 5f, .55f, .35f, 0f, 2f, .35f, .75f, .32f),
            CrowdMovementPreset.Orbit => Make(true, 1.35f, .48f, 2.2f, 1.15f, 0f, 0f, 3.4f, .7f, 2f, .25f, .2f, .25f),
            CrowdMovementPreset.Encircle => Make(true, 1.3f, .62f, 2.7f, 1.5f, 4.5f, .18f, 1.65f, 0f, 6.5f, 2.4f, .25f, .22f),
            CrowdMovementPreset.Chaotic => Make(true, 1.45f, .7f, 2.4f, 1.4f, 4f, .2f, 1.8f, 0f, 5f, 1.45f, 2.2f, .75f),
            _ => Make(false, 1f, 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, .25f)
        };
        CurrentPreset = preset;
    }

    private static CrowdSteeringValues Make(bool enabled, float global,
        float direct, float separationRadius, float separation, float cohesionRadius,
        float cohesion, float orbit, float orbitBias, float offsetRadius,
        float offset, float wander, float frequency)
    {
        return new CrowdSteeringValues
        {
            Enabled = enabled, GlobalStrength = global, DirectPressure = direct,
            SeparationRadius = separationRadius, SeparationStrength = separation,
            CohesionRadius = cohesionRadius, CohesionStrength = cohesion,
            OrbitStrength = orbit, OrbitDirectionBias = orbitBias,
            TargetOffsetRadius = offsetRadius, TargetOffsetStrength = offset,
            WanderStrength = wander, WanderFrequency = frequency
        };
    }

    internal static void SampleNeighbours(EnemyMovement owner, Vector2 position,
        out Vector2 separation, out Vector2 cohesion)
    {
        separation = Vector2.zero;
        cohesion = Vector2.zero;
        if (!values.Enabled || values.GlobalStrength <= 0f)
            return;

        float maxRadius = Mathf.Max(values.SeparationRadius, values.CohesionRadius);
        if (maxRadius <= 0f)
            return;

        float separationRadiusSq = values.SeparationRadius * values.SeparationRadius;
        float cohesionRadiusSq = values.CohesionRadius * values.CohesionRadius;
        float maxRadiusSq = maxRadius * maxRadius;
        Vector2 cohesionCenter = Vector2.zero;
        int cohesionCount = 0;
        int accepted = 0;

        for (int i = agents.Count - 1; i >= 0 && accepted < NeighbourLimit; i--)
        {
            EnemyMovement other = agents[i];
            if (other == null)
            {
                agents.RemoveAt(i);
                continue;
            }
            if (other == owner || !other.isActiveAndEnabled)
                continue;

            Vector2 away = position - (Vector2)other.transform.position;
            float distanceSq = away.sqrMagnitude;
            if (distanceSq <= .0001f || distanceSq > maxRadiusSq)
                continue;

            accepted++;
            if (distanceSq <= separationRadiusSq)
            {
                float distance = Mathf.Sqrt(distanceSq);
                separation += away / distance *
                    (1f - distance / Mathf.Max(.001f, values.SeparationRadius));
            }
            if (distanceSq <= cohesionRadiusSq)
            {
                cohesionCenter += (Vector2)other.transform.position;
                cohesionCount++;
            }
        }

        if (separation.sqrMagnitude > 1f)
            separation.Normalize();
        if (cohesionCount > 0)
        {
            cohesion = cohesionCenter / cohesionCount - position;
            if (cohesion.sqrMagnitude > .0001f)
                cohesion.Normalize();
        }
    }

    internal static bool ShouldDebugDraw(EnemyMovement agent, Vector2 playerPosition)
    {
        if (!DebugDraw)
            return false;
        float distanceSq = ((Vector2)agent.transform.position - playerPosition).sqrMagnitude;
        int closer = 0;
        for (int i = 0; i < agents.Count && closer < 16; i++)
        {
            EnemyMovement other = agents[i];
            if (other != null && other != agent && other.isActiveAndEnabled &&
                ((Vector2)other.transform.position - playerPosition).sqrMagnitude < distanceSq)
                closer++;
        }
        return closer < 16;
    }
}
