using System.Collections.Generic;
using UnityEngine;

public static class AnomalyPowerRuntime
{
    public static void ApplyRunLoadout(GameObject player)
    {
        RunStateManager runState = RunStateManager.Instance;

        if (player == null || runState == null)
            return;

        IReadOnlyList<AnomalyPowerType> powers = runState.AnomalyPowers;

        for (int i = 0; i < powers.Count; i++)
            EnsurePower(player, powers[i]);
    }

    public static void EnsurePower(GameObject player, AnomalyPowerType power)
    {
        if (player == null)
            return;

        switch (power)
        {
            case AnomalyPowerType.GravityOrb:
                EnsureComponent<GravityOrbRuntime>(player);
                break;
            case AnomalyPowerType.ArcNode:
                EnsureComponent<ArcNodeRuntime>(player);
                break;
            case AnomalyPowerType.RedBeam:
                EnsureComponent<RedBeamRuntime>(player);
                break;
        }
    }

    private static void EnsureComponent<T>(GameObject player)
        where T : Component
    {
        if (player.GetComponent<T>() == null)
            player.AddComponent<T>();
    }
}

internal static class AnomalyPowerVisuals
{
    public static Material CreateMaterial(string name)
    {
        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        return shader != null
            ? new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            }
            : null;
    }

    public static LineRenderer CreateLine(
        Transform parent,
        string name,
        Color color,
        float width,
        int positionCount,
        Material material)
    {
        GameObject lineObject = new(name);
        lineObject.transform.SetParent(parent, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = positionCount;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.numCapVertices = 5;
        line.sortingLayerName = "Effects";
        line.sortingOrder = 30;

        if (material != null)
            line.sharedMaterial = material;

        return line;
    }
}

internal sealed class GravityOrbRuntime : MonoBehaviour
{
    private const float OrbitRadius = 3f;
    private const float DegreesPerSecond = 145f;
    private const float VisualRadius = 0.78f;
    private const float CoreRadius = 0.38f;
    private const float GlowRadius = 0.96f;
    private const float TrailHistorySeconds = 0.32f;
    private const float ContactRadius = 0.72f;
    private const float ContactDamage = 65f;
    private const float TargetCooldown = 0.42f;

    private readonly Dictionary<EnemyHealth, float> nextHitTimes = new();
    private readonly List<EnemyHealth> enemySnapshot = new();
    private Transform orb;
    private Material material;
    private float angle;

    private void Awake()
    {
        GameObject orbObject = new("Gravity Orb");
        orbObject.transform.SetParent(transform, false);
        orb = orbObject.transform;
        orb.position = (Vector2)transform.position + Vector2.right *
            OrbitRadius;
        material = AnomalyPowerVisuals.CreateMaterial(
            "Gravity Orb Runtime Material"
        );

        LineRenderer glow = AnomalyPowerVisuals.CreateLine(
            orb,
            "Gravity Orb Glow",
            new Color(0.45f, 0.18f, 1f, 0.25f),
            0.22f,
            33,
            material
        );
        ConfigureRing(glow, GlowRadius);
        glow.sortingOrder = 34;

        LineRenderer ring = AnomalyPowerVisuals.CreateLine(
            orb,
            "Gravity Orb Outer Ring",
            new Color(0.55f, 0.15f, 1f, 1f),
            0.16f,
            33,
            material
        );
        ring.endColor = new Color(0.1f, 0.85f, 1f, 1f);
        ConfigureRing(ring, VisualRadius);
        ring.sortingOrder = 36;

        LineRenderer core = AnomalyPowerVisuals.CreateLine(
            orb,
            "Gravity Orb Core",
            new Color(0.72f, 0.4f, 1f, 1f),
            CoreRadius * 2f,
            2,
            material
        );
        core.useWorldSpace = false;
        core.numCapVertices = 10;
        core.SetPosition(0, new Vector3(-0.025f, 0f, 0f));
        core.SetPosition(1, new Vector3(0.025f, 0f, 0f));
        core.sortingOrder = 37;

        GameObject trailObject = new("Gravity Orb Trail");
        trailObject.transform.SetParent(orb, false);
        TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
        trail.time = TrailHistorySeconds;
        trail.minVertexDistance = 0.08f;
        trail.startWidth = 0.34f;
        trail.endWidth = 0.02f;
        trail.numCapVertices = 5;
        trail.numCornerVertices = 4;
        trail.startColor = new Color(0.7f, 0.2f, 1f, 0.75f);
        trail.endColor = new Color(0.05f, 0.7f, 1f, 0f);
        trail.sortingLayerName = "Effects";
        trail.sortingOrder = 35;

        if (material != null)
            trail.sharedMaterial = material;

        trail.Clear();
    }

    private static void ConfigureRing(LineRenderer ring, float radius)
    {
        ring.useWorldSpace = false;
        ring.loop = true;

        for (int i = 0; i < ring.positionCount; i++)
        {
            float radians = Mathf.PI * 2f * i / ring.positionCount;
            ring.SetPosition(i, new Vector3(
                Mathf.Cos(radians) * radius,
                Mathf.Sin(radians) * radius,
                0f
            ));
        }
    }

    private void Update()
    {
        angle = Mathf.Repeat(
            angle + DegreesPerSecond * Time.deltaTime,
            360f
        );
        Vector2 offset = new(
            Mathf.Cos(angle * Mathf.Deg2Rad),
            Mathf.Sin(angle * Mathf.Deg2Rad)
        );
        orb.position = (Vector2)transform.position + offset * OrbitRadius;

        enemySnapshot.Clear();

        foreach (EnemyHealth activeEnemy in EnemyHealth.ActiveInstances)
            enemySnapshot.Add(activeEnemy);

        foreach (EnemyHealth enemy in enemySnapshot)
        {
            if (enemy == null || enemy.IsDead ||
                Vector2.Distance(enemy.transform.position, orb.position) >
                ContactRadius)
            {
                continue;
            }

            if (nextHitTimes.TryGetValue(enemy, out float nextHit) &&
                Time.time < nextHit)
            {
                continue;
            }

            enemy.TakeDamage(ContactDamage, orb.position, false);
            nextHitTimes[enemy] = Time.time + TargetCooldown;
        }
    }

    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }
}

internal sealed class ArcNodeRuntime : MonoBehaviour
{
    private const float DischargeInterval = 0.65f;
    private const float AcquisitionRadius = 9f;
    private const float JumpRadius = 4.2f;
    private const int MaxTargets = 4;
    private const float DamagePerTarget = 70f;

    private readonly List<EnemyHealth> targets = new(MaxTargets);
    private static readonly Vector2 NodeOffset = new(-1.45f, 1.05f);
    private Material material;
    private LineRenderer line;
    private Transform nodeVisual;
    private float nextDischarge;
    private float hideLineAt;

    private void Awake()
    {
        material = AnomalyPowerVisuals.CreateMaterial(
            "Arc Node Runtime Material"
        );
        BuildNodeVisual();
        line = AnomalyPowerVisuals.CreateLine(
            transform,
            "Arc Node Discharge",
            new Color(0.3f, 0.8f, 1f, 1f),
            0.11f,
            MaxTargets + 1,
            material
        );
        line.enabled = false;
    }

    private void Update()
    {
        UpdateNodeVisual();

        if (line.enabled && Time.time >= hideLineAt)
            line.enabled = false;
        else if (line.enabled && nodeVisual != null)
            line.SetPosition(0, nodeVisual.position);

        if (Time.time < nextDischarge)
            return;

        nextDischarge = Time.time + DischargeInterval;
        BuildChain();

        if (targets.Count == 0)
            return;

        line.positionCount = targets.Count + 1;
        line.SetPosition(0, nodeVisual != null
            ? nodeVisual.position
            : transform.position);

        for (int i = 0; i < targets.Count; i++)
        {
            EnemyHealth enemy = targets[i];
            Vector3 hitPoint = enemy.transform.position;
            line.SetPosition(i + 1, hitPoint);
            enemy.TakeDamage(DamagePerTarget, hitPoint, false);
        }

        line.enabled = true;
        hideLineAt = Time.time + 0.12f;
    }

    private void BuildChain()
    {
        targets.Clear();
        Vector2 origin = transform.position;

        for (int targetIndex = 0; targetIndex < MaxTargets; targetIndex++)
        {
            EnemyHealth best = null;
            float bestDistance = float.PositiveInfinity;
            float radius = targetIndex == 0
                ? AcquisitionRadius
                : JumpRadius;

            foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
            {
                if (enemy == null || enemy.IsDead || targets.Contains(enemy))
                    continue;

                float distance = Vector2.Distance(
                    origin,
                    enemy.transform.position
                );

                if (distance <= radius && distance < bestDistance)
                {
                    best = enemy;
                    bestDistance = distance;
                }
            }

            if (best == null)
                break;

            targets.Add(best);
            origin = best.transform.position;
        }
    }

    private void BuildNodeVisual()
    {
        GameObject nodeObject = new("Arc Node Satellite");
        nodeObject.transform.SetParent(transform, false);
        nodeVisual = nodeObject.transform;

        LineRenderer glow = AnomalyPowerVisuals.CreateLine(
            nodeVisual,
            "Arc Node Glow",
            new Color(0.12f, 0.65f, 1f, 0.28f),
            0.18f,
            25,
            material
        );
        ConfigureNodeRing(glow, 0.53f);
        glow.sortingOrder = 35;

        LineRenderer star = AnomalyPowerVisuals.CreateLine(
            nodeVisual,
            "Arc Node Star",
            new Color(0.2f, 0.92f, 1f, 1f),
            0.11f,
            12,
            material
        );
        star.useWorldSpace = false;
        star.loop = true;
        star.sortingOrder = 38;
        star.endColor = new Color(0.18f, 0.48f, 1f, 1f);

        for (int i = 0; i < star.positionCount; i++)
        {
            float radians = Mathf.PI * 2f * i / star.positionCount;
            float radius = i % 2 == 0 ? 0.42f : 0.22f;
            star.SetPosition(i, new Vector3(
                Mathf.Cos(radians) * radius,
                Mathf.Sin(radians) * radius,
                0f
            ));
        }

        LineRenderer core = AnomalyPowerVisuals.CreateLine(
            nodeVisual,
            "Arc Node Core",
            new Color(0.72f, 0.96f, 1f, 1f),
            0.3f,
            2,
            material
        );
        core.useWorldSpace = false;
        core.numCapVertices = 8;
        core.SetPosition(0, new Vector3(-0.02f, 0f, 0f));
        core.SetPosition(1, new Vector3(0.02f, 0f, 0f));
        core.sortingOrder = 39;
        UpdateNodeVisual();
    }

    private static void ConfigureNodeRing(LineRenderer ring, float radius)
    {
        ring.useWorldSpace = false;
        ring.loop = true;
        for (int i = 0; i < ring.positionCount; i++)
        {
            float radians = Mathf.PI * 2f * i / ring.positionCount;
            ring.SetPosition(i, new Vector3(
                Mathf.Cos(radians) * radius,
                Mathf.Sin(radians) * radius,
                0f
            ));
        }
    }

    private void UpdateNodeVisual()
    {
        if (nodeVisual == null)
            return;

        float phase = Time.time * 2.2f;
        Vector2 drift = new(
            Mathf.Cos(phase) * 0.1f,
            Mathf.Sin(phase * 1.15f) * 0.14f
        );
        nodeVisual.position = (Vector2)transform.position +
            NodeOffset + drift;
        nodeVisual.Rotate(0f, 0f, 72f * Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }
}

internal sealed class RedBeamRuntime : MonoBehaviour
{
    private enum State
    {
        Waiting,
        Telegraph,
        Firing
    }

    private const float Cooldown = 3f;
    private const float TelegraphDuration = 0.32f;
    private const float BeamDuration = 0.22f;
    private const float BeamRange = 18f;
    private const float BeamHalfWidth = 1.05f;
    private const float BeamDamage = 120f;
    private static readonly Vector2 EmitterOffset = new(1.45f, 1.02f);

    private Material material;
    private LineRenderer line;
    private Transform emitterVisual;
    private State state;
    private float stateUntil;
    private Vector2 direction = Vector2.right;

    private void Awake()
    {
        material = AnomalyPowerVisuals.CreateMaterial(
            "Red Beam Runtime Material"
        );
        BuildEmitterVisual();
        line = AnomalyPowerVisuals.CreateLine(
            transform,
            "Red Beam",
            new Color(1f, 0.12f, 0.08f, 0.75f),
            0.08f,
            2,
            material
        );
        line.enabled = false;
        stateUntil = Time.time + Cooldown;
    }

    private void Update()
    {
        UpdateEmitterVisual();
        if (state != State.Waiting && line != null && line.enabled)
            UpdateBeamVisualPosition();

        if (Time.time < stateUntil)
            return;

        switch (state)
        {
            case State.Waiting:
                direction = SelectDirection();
                state = State.Telegraph;
                stateUntil = Time.time + TelegraphDuration;
                ShowLine(0.08f, new Color(1f, 0.2f, 0.1f, 0.7f));
                break;
            case State.Telegraph:
                Fire();
                state = State.Firing;
                stateUntil = Time.time + BeamDuration;
                ShowLine(
                    BeamHalfWidth * 2f,
                    new Color(1f, 0.05f, 0.02f, 0.8f)
                );
                break;
            default:
                line.enabled = false;
                state = State.Waiting;
                stateUntil = Time.time + Cooldown;
                break;
        }
    }

    private Vector2 SelectDirection()
    {
        EnemyHealth nearest = null;
        float bestDistance = float.PositiveInfinity;

        List<EnemyHealth> enemies = new(EnemyHealth.ActiveInstances);

        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy == null || enemy.IsDead)
                continue;

            float distance = Vector2.Distance(
                transform.position,
                enemy.transform.position
            );

            if (distance < bestDistance)
            {
                nearest = enemy;
                bestDistance = distance;
            }
        }

        if (nearest == null)
            return Vector2.right;

        Vector2 offset = nearest.transform.position - transform.position;
        return offset.sqrMagnitude > 0.001f
            ? offset.normalized
            : Vector2.right;
    }

    private void Fire()
    {
        Vector2 origin = transform.position;
        List<EnemyHealth> enemies = new(EnemyHealth.ActiveInstances);

        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy == null || enemy.IsDead)
                continue;

            Vector2 offset = (Vector2)enemy.transform.position - origin;
            float forward = Vector2.Dot(offset, direction);
            float perpendicular = Mathf.Abs(
                direction.x * offset.y - direction.y * offset.x
            );

            if (forward > 0f && forward <= BeamRange &&
                perpendicular <= BeamHalfWidth)
            {
                enemy.TakeDamage(
                    BeamDamage,
                    origin + direction * forward,
                    false
                );
            }
        }
    }

    private void ShowLine(float width, Color color)
    {
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        UpdateBeamVisualPosition();
        line.enabled = true;
    }

    private void UpdateBeamVisualPosition()
    {
        Vector3 origin = emitterVisual != null
            ? emitterVisual.position
            : transform.position;
        Vector3 end = transform.position +
            (Vector3)(direction * BeamRange);
        line.SetPosition(0, origin);
        line.SetPosition(1, end);
    }

    private void BuildEmitterVisual()
    {
        GameObject emitterObject = new("Red Beam Emitter");
        emitterObject.transform.SetParent(transform, false);
        emitterVisual = emitterObject.transform;

        LineRenderer glow = AnomalyPowerVisuals.CreateLine(
            emitterVisual,
            "Red Beam Emitter Glow",
            new Color(1f, 0.02f, 0.02f, 0.22f),
            0.2f,
            29,
            material
        );
        ConfigureEmitterRing(glow, 0.62f);
        glow.sortingOrder = 35;

        LineRenderer ring = AnomalyPowerVisuals.CreateLine(
            emitterVisual,
            "Red Beam Emitter Ring",
            new Color(0.85f, 0.04f, 0.025f, 1f),
            0.1f,
            25,
            material
        );
        ConfigureEmitterRing(ring, 0.44f);
        ring.sortingOrder = 38;

        LineRenderer core = AnomalyPowerVisuals.CreateLine(
            emitterVisual,
            "Red Beam Emitter Core",
            new Color(1f, 0.18f, 0.08f, 1f),
            0.36f,
            2,
            material
        );
        core.useWorldSpace = false;
        core.numCapVertices = 10;
        core.SetPosition(0, new Vector3(-0.025f, 0f, 0f));
        core.SetPosition(1, new Vector3(0.025f, 0f, 0f));
        core.sortingOrder = 39;
        UpdateEmitterVisual();
    }

    private static void ConfigureEmitterRing(
        LineRenderer ring,
        float radius)
    {
        ring.useWorldSpace = false;
        ring.loop = true;
        for (int i = 0; i < ring.positionCount; i++)
        {
            float radians = Mathf.PI * 2f * i / ring.positionCount;
            ring.SetPosition(i, new Vector3(
                Mathf.Cos(radians) * radius,
                Mathf.Sin(radians) * radius,
                0f
            ));
        }
    }

    private void UpdateEmitterVisual()
    {
        if (emitterVisual == null)
            return;

        float phase = Time.time * 1.9f + 1.7f;
        Vector2 drift = new(
            Mathf.Cos(phase) * 0.08f,
            Mathf.Sin(phase * 1.1f) * 0.12f
        );
        emitterVisual.position = (Vector2)transform.position +
            EmitterOffset + drift;
        emitterVisual.Rotate(0f, 0f, -46f * Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }
}
