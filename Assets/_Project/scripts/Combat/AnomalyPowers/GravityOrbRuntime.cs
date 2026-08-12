using System.Collections.Generic;
using UnityEngine;

internal sealed class GravityOrbRuntime : MonoBehaviour, IAnomalyPowerRuntime
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

    public AnomalyPowerType Type => AnomalyPowerType.GravityOrb;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterRuntime()
    {
        AnomalyPowerRuntimeRegistry.Register(
            AnomalyPowerType.GravityOrb,
            owner => owner.AddComponent<GravityOrbRuntime>()
        );
    }

    public void Activate()
    {
        enabled = true;

        if (orb != null)
            orb.gameObject.SetActive(true);
    }

    public void Deactivate()
    {
        enabled = false;

        if (orb != null)
            orb.gameObject.SetActive(false);
    }

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
