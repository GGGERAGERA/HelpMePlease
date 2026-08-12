using System.Collections.Generic;
using UnityEngine;

internal sealed class RedBeamRuntime : MonoBehaviour, IAnomalyPowerRuntime
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

    public AnomalyPowerType Type => AnomalyPowerType.RedBeam;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterRuntime()
    {
        AnomalyPowerRuntimeRegistry.Register(
            AnomalyPowerType.RedBeam,
            owner => owner.AddComponent<RedBeamRuntime>()
        );
    }

    public void Activate()
    {
        enabled = true;

        if (emitterVisual != null)
            emitterVisual.gameObject.SetActive(true);
    }

    public void Deactivate()
    {
        enabled = false;

        if (emitterVisual != null)
            emitterVisual.gameObject.SetActive(false);

        if (line != null)
            line.enabled = false;
    }

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
