using System.Collections.Generic;
using UnityEngine;

public sealed class GravityOrbPower : MonoBehaviour
{
    // Prototype tuning.
    public const float OrbitRadius = 3f;
    public const float DegreesPerSecond = 145f;
    public const float ContactRadius = 0.72f;
    public const float ContactDamage = 65f;
    public const float TargetCooldown = 0.42f;

    private const float ScanInterval = 0.04f;
    private const int TrailPoints = 16;

    private readonly Collider2D[] overlapResults = new Collider2D[48];
    private readonly Dictionary<EnemyHealth, float> nextDamageTimes = new();
    private readonly List<EnemyHealth> staleTargets = new();

    private GameObject visualRoot;
    private LineRenderer orbRing;
    private LineRenderer trail;
    private float angle;
    private float scanTimer;
    private float cleanupTimer;
    private Vector2 orbPosition;
    private ContactFilter2D contactFilter;
    private float lastContactTime = float.NegativeInfinity;
    private int lastContactHits;
    private int lastContactKills;

    public float LastContactTime => lastContactTime;
    public int LastContactHits => lastContactHits;
    public int LastContactKills => lastContactKills;
    public float LastDamage => ContactDamage;

    private void Awake()
    {
        contactFilter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = false,
            useDepth = false
        };
    }

    private void OnEnable()
    {
        EnsureVisuals();
        visualRoot.SetActive(true);
        angle = 0f;
        scanTimer = 0f;
        ResetTrail();
    }

    private void Update()
    {
        angle = Mathf.Repeat(
            angle + DegreesPerSecond * Time.deltaTime,
            360f
        );
        float radians = angle * Mathf.Deg2Rad;
        orbPosition = (Vector2)transform.position + new Vector2(
            Mathf.Cos(radians),
            Mathf.Sin(radians)
        ) * OrbitRadius;
        visualRoot.transform.position = orbPosition;
        UpdateTrail();

        scanTimer += Time.deltaTime;
        if (scanTimer >= ScanInterval)
        {
            scanTimer = 0f;
            DamageContacts();
        }

        cleanupTimer += Time.deltaTime;
        if (cleanupTimer >= 2f)
        {
            cleanupTimer = 0f;
            CleanupTargets();
        }
    }

    private void DamageContacts()
    {
        int hitsThisScan = 0;
        int killsThisScan = 0;
        int count = Physics2D.OverlapCircle(
            orbPosition,
            ContactRadius,
            contactFilter,
            overlapResults
        );

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = overlapResults[i];
            EnemyHealth enemy = hit != null
                ? hit.GetComponentInParent<EnemyHealth>()
                : null;
            if (enemy == null || enemy.IsDead)
                continue;

            if (nextDamageTimes.TryGetValue(enemy, out float nextTime) &&
                Time.time < nextTime)
            {
                continue;
            }

            nextDamageTimes[enemy] = Time.time + TargetCooldown;
            enemy.TakeDamage(ContactDamage, orbPosition, false);
            lastContactTime = Time.time;
            hitsThisScan++;
            if (enemy.IsDead)
                killsThisScan++;
        }

        if (hitsThisScan > 0)
        {
            lastContactHits = hitsThisScan;
            lastContactKills = killsThisScan;
        }
    }

    private void EnsureVisuals()
    {
        if (visualRoot != null)
            return;

        visualRoot = new GameObject("Gravity Orb Power Visual");
        visualRoot.transform.SetParent(transform, true);
        orbRing = CreateLine("Gravity Orb", false, 32, 0.15f, 36);
        orbRing.loop = true;
        orbRing.startColor = new Color(0.55f, 0.15f, 1f, 1f);
        orbRing.endColor = new Color(0.1f, 0.85f, 1f, 1f);

        for (int i = 0; i < orbRing.positionCount; i++)
        {
            float radians = Mathf.PI * 2f * i / orbRing.positionCount;
            orbRing.SetPosition(i, new Vector3(
                Mathf.Cos(radians) * ContactRadius,
                Mathf.Sin(radians) * ContactRadius,
                0f
            ));
        }

        trail = CreateLine("Gravity Orb Trail", true, TrailPoints, 0.22f, 35);
        trail.startColor = new Color(0.7f, 0.2f, 1f, 0.8f);
        trail.endColor = new Color(0.05f, 0.7f, 1f, 0f);
        trail.startWidth = 0.22f;
        trail.endWidth = 0.02f;
    }

    private LineRenderer CreateLine(
        string objectName,
        bool worldSpace,
        int pointCount,
        float width,
        int sortingOrder)
    {
        GameObject lineObject = new(objectName);
        lineObject.transform.SetParent(visualRoot.transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = worldSpace;
        line.positionCount = pointCount;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 4;
        line.numCornerVertices = 3;
        line.sharedMaterial = WeaponCoreDebugVisual.SharedLineMaterial;
        line.sortingLayerName = "Effects";
        line.sortingOrder = sortingOrder;
        return line;
    }

    private void ResetTrail()
    {
        orbPosition = transform.position;
        if (trail == null)
            return;

        for (int i = 0; i < trail.positionCount; i++)
            trail.SetPosition(i, orbPosition);
    }

    private void UpdateTrail()
    {
        for (int i = trail.positionCount - 1; i > 0; i--)
            trail.SetPosition(i, trail.GetPosition(i - 1));

        trail.SetPosition(0, orbPosition);
    }

    private void CleanupTargets()
    {
        staleTargets.Clear();
        foreach (KeyValuePair<EnemyHealth, float> pair in nextDamageTimes)
        {
            if (pair.Key == null || pair.Key.IsDead || pair.Value < Time.time - 1f)
                staleTargets.Add(pair.Key);
        }

        for (int i = 0; i < staleTargets.Count; i++)
            nextDamageTimes.Remove(staleTargets[i]);
    }

    private void OnDisable()
    {
        if (visualRoot != null)
            visualRoot.SetActive(false);

        nextDamageTimes.Clear();
    }

    private void OnDestroy()
    {
        if (visualRoot != null)
            Destroy(visualRoot);
    }
}
