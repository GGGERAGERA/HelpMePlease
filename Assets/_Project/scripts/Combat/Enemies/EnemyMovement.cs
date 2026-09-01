using UnityEngine;

public static class EnemyDebugAiFreeze
{
    public static bool IsFrozen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessionState()
    {
        IsFrozen = false;
    }

    public static void SetFrozen(bool frozen)
    {
        IsFrozen = frozen;
    }
}

public abstract class EnemyMovement : MonoBehaviour, IAnomalyExternalVelocity
{
    private readonly AnomalyExternalVelocityStack
        anomalyExternalVelocity = new();

    protected Vector2 AnomalyExternalVelocity =>
        anomalyExternalVelocity.Value;
    public Component ExternalVelocityComponent => this;

    public abstract void SetSpeedMultiplier(float multiplier);
    public abstract void SetAnomalySpeedMultiplier(float multiplier);
    public abstract void SetWorldRuleSpeedMultiplier(float multiplier);
    public abstract void SetWorldRuleExternalVelocity(Vector2 velocity);
    public abstract void ApplyKnockback(Vector2 direction, float force);
    public abstract void StopAfterHit();

    private int crowdSeed;
    private float nextNeighbourSampleTime;
    private Vector2 cachedSeparation;
    private Vector2 cachedCohesion;
    private Vector2 smoothedSteeringDirection;

    protected void InitializeCrowdSteering()
    {
        crowdSeed = StableHash(gameObject.name, GetInstanceID());
        nextNeighbourSampleTime = Time.time + Positive01(crowdSeed) *
            CrowdSteeringRuntime.NeighbourRefreshInterval;
        smoothedSteeringDirection = Vector2.zero;
        CrowdSteeringRuntime.Register(this);
    }

    protected void ReleaseCrowdSteering()
    {
        cachedSeparation = Vector2.zero;
        cachedCohesion = Vector2.zero;
        smoothedSteeringDirection = Vector2.zero;
    }

    protected Vector2 ApplyCrowdSteering(Vector2 productionDirection,
        Vector2 playerPosition, float deltaTime)
    {
        CrowdSteeringValues settings = CrowdSteeringRuntime.Values;
        if (!settings.Enabled || settings.GlobalStrength <= 0f ||
            productionDirection.sqrMagnitude <= .0001f)
        {
            smoothedSteeringDirection = productionDirection;
            return productionDirection;
        }

        Vector2 position = transform.position;
        if (Time.time >= nextNeighbourSampleTime)
        {
            CrowdSteeringRuntime.SampleNeighbours(this, position,
                out cachedSeparation, out cachedCohesion);
            nextNeighbourSampleTime = Time.time +
                Mathf.Max(.02f, CrowdSteeringRuntime.NeighbourRefreshInterval);
        }

        Vector2 radial = position - playerPosition;
        if (radial.sqrMagnitude > .0001f)
            radial.Normalize();
        float stableSide = Positive01(crowdSeed ^ unchecked((int)0x2C9277B5)) < .5f ? -1f : 1f;
        float directionBias = Mathf.Clamp(settings.OrbitDirectionBias, -1f, 1f);
        float orbitSign = Mathf.Abs(directionBias) < .001f
            ? stableSide
            : Mathf.Lerp(stableSide, Mathf.Sign(directionBias), Mathf.Abs(directionBias));
        Vector2 orbit = new(-radial.y, radial.x);
        orbit *= orbitSign;

        float offsetAngle = Positive01(crowdSeed ^ unchecked((int)0x68E31DA4)) * Mathf.PI * 2f;
        Vector2 personalOffset = new(Mathf.Cos(offsetAngle), Mathf.Sin(offsetAngle));
        Vector2 offsetDirection = playerPosition +
            personalOffset * settings.TargetOffsetRadius - position;
        if (offsetDirection.sqrMagnitude > .0001f)
            offsetDirection.Normalize();

        float noisePhase = Positive01(crowdSeed ^ unchecked((int)0x51F15EED)) * 100f;
        float noise = Mathf.PerlinNoise(noisePhase,
            Time.time * settings.WanderFrequency) * 2f - 1f;
        Vector2 wanderDirection = new Vector2(
            -productionDirection.y,
            productionDirection.x);
        Vector2 wander = wanderDirection * noise;

        Vector2 combined = productionDirection * settings.DirectPressure +
            settings.GlobalStrength * (
                cachedSeparation * settings.SeparationStrength +
                cachedCohesion * settings.CohesionStrength +
                orbit * settings.OrbitStrength +
                offsetDirection * settings.TargetOffsetStrength +
                wander * settings.WanderStrength);

        Vector2 targetDirection = combined.sqrMagnitude > .0001f
            ? combined.normalized
            : productionDirection;
        if (smoothedSteeringDirection.sqrMagnitude <= .0001f)
            smoothedSteeringDirection = productionDirection;
        float blend = 1f - Mathf.Exp(-8f * Mathf.Max(0f, deltaTime));
        smoothedSteeringDirection = Vector2.Lerp(
            smoothedSteeringDirection, targetDirection, blend).normalized;

        if (CrowdSteeringRuntime.ShouldDebugDraw(this, playerPosition))
        {
            Vector2 origin = position;
            Debug.DrawLine(origin, origin + smoothedSteeringDirection * 1.5f, Color.white);
            Debug.DrawLine(origin, origin + productionDirection, Color.cyan);
            Debug.DrawLine(origin, origin + cachedSeparation, Color.red);
            Debug.DrawLine(origin, origin + orbit, Color.yellow);
            Debug.DrawLine(playerPosition, playerPosition +
                personalOffset * settings.TargetOffsetRadius, Color.magenta);
        }
        return smoothedSteeringDirection;
    }

    private static int StableHash(string text, int instanceId)
    {
        unchecked
        {
            int hash = 17;
            if (text != null)
                for (int i = 0; i < text.Length; i++) hash = hash * 31 + text[i];
            return hash * 31 + instanceId;
        }
    }

    private static float Positive01(int value)
    {
        unchecked
        {
            uint x = (uint)value;
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return (x & 0x00FFFFFF) / 16777215f;
        }
    }

    public void SetAnomalyExternalVelocity(
        Object source,
        Vector2 velocity)
    {
        anomalyExternalVelocity.Set(source, velocity);
    }

    public void RemoveAnomalyExternalVelocity(Object source)
    {
        anomalyExternalVelocity.Remove(source);
    }

    protected void ClearAnomalyExternalVelocities()
    {
        anomalyExternalVelocity.Clear();
    }
}
