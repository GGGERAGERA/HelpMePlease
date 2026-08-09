using System.Collections.Generic;
using UnityEngine;

public sealed class RedBeamPower : MonoBehaviour
{
    // Prototype tuning.
    public const float Cooldown = 3f;
    public const float TelegraphDuration = 0.32f;
    public const float BeamDuration = 0.22f;
    public const float BeamRange = 18f;
    public const float BeamHalfWidth = 1.05f;
    public const float BeamDamage = 120f;

    private enum BeamState
    {
        Waiting,
        Telegraph,
        Firing
    }

    private readonly List<EnemyHealth> candidates = new(64);
    private GameObject visualRoot;
    private LineRenderer telegraph;
    private LineRenderer beamGlow;
    private LineRenderer beamCore;
    private BeamState state;
    private Vector2 fireDirection = Vector2.right;
    private float stateTimer;

    private void OnEnable()
    {
        EnsureVisuals();
        visualRoot.SetActive(true);
        HideLines();
        state = BeamState.Waiting;
        stateTimer = 0.8f;
    }

    private void Update()
    {
        stateTimer += Time.deltaTime;

        if (state == BeamState.Waiting)
        {
            if (stateTimer >= Cooldown && TryChooseDirection())
                BeginTelegraph();

            return;
        }

        UpdateLinePositions();

        if (state == BeamState.Telegraph &&
            stateTimer >= TelegraphDuration)
        {
            FireBeam();
        }
        else if (state == BeamState.Firing &&
            stateTimer >= BeamDuration)
        {
            HideLines();
            state = BeamState.Waiting;
            stateTimer = 0f;
        }
    }

    private bool TryChooseDirection()
    {
        candidates.Clear();
        Vector2 origin = transform.position;
        float rangeSquared = BeamRange * BeamRange;

        foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
        {
            if (enemy == null || enemy.IsDead)
                continue;

            if (((Vector2)enemy.transform.position - origin).sqrMagnitude <=
                rangeSquared)
            {
                candidates.Add(enemy);
            }
        }

        if (candidates.Count == 0)
            return false;

        int bestScore = 0;
        Vector2 bestDirection = Vector2.right;

        for (int i = 0; i < candidates.Count; i++)
        {
            Vector2 direction =
                ((Vector2)candidates[i].transform.position - origin).normalized;
            int score = CountTargetsOnLine(origin, direction);

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestDirection = direction;
        }

        fireDirection = bestDirection;
        return bestScore > 0;
    }

    private int CountTargetsOnLine(Vector2 origin, Vector2 direction)
    {
        int score = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            Vector2 offset =
                (Vector2)candidates[i].transform.position - origin;
            float forward = Vector2.Dot(offset, direction);
            if (forward <= 0f || forward > BeamRange)
                continue;

            float perpendicular = Mathf.Abs(
                direction.x * offset.y - direction.y * offset.x
            );
            if (perpendicular <= BeamHalfWidth)
                score++;
        }

        return score;
    }

    private void BeginTelegraph()
    {
        state = BeamState.Telegraph;
        stateTimer = 0f;
        telegraph.enabled = true;
        beamGlow.enabled = false;
        beamCore.enabled = false;
        UpdateLinePositions();
    }

    private void FireBeam()
    {
        state = BeamState.Firing;
        stateTimer = 0f;
        telegraph.enabled = false;
        beamGlow.enabled = true;
        beamCore.enabled = true;
        UpdateLinePositions();

        Vector2 origin = transform.position;
        foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
        {
            if (enemy == null || enemy.IsDead)
                continue;

            Vector2 offset = (Vector2)enemy.transform.position - origin;
            float forward = Vector2.Dot(offset, fireDirection);
            if (forward <= 0f || forward > BeamRange)
                continue;

            float perpendicular = Mathf.Abs(
                fireDirection.x * offset.y - fireDirection.y * offset.x
            );
            if (perpendicular > BeamHalfWidth)
                continue;

            Vector2 hitPoint = origin + fireDirection * forward;
            enemy.TakeDamage(BeamDamage, hitPoint, false);
        }
    }

    private void UpdateLinePositions()
    {
        Vector2 start = transform.position;
        Vector2 end = start + fireDirection * BeamRange;
        SetLine(telegraph, start, end);
        SetLine(beamGlow, start, end);
        SetLine(beamCore, start, end);
    }

    private static void SetLine(
        LineRenderer line,
        Vector2 start,
        Vector2 end)
    {
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private void EnsureVisuals()
    {
        if (visualRoot != null)
            return;

        visualRoot = new GameObject("Red Beam Power Visual");
        visualRoot.transform.SetParent(transform, true);
        telegraph = CreateLine(
            "Red Beam Telegraph",
            0.045f,
            new Color(1f, 0.08f, 0.08f, 0.65f),
            38
        );
        beamGlow = CreateLine(
            "Red Beam Glow",
            1.05f,
            new Color(1f, 0.02f, 0.02f, 0.32f),
            38
        );
        beamCore = CreateLine(
            "Red Beam Core",
            0.38f,
            new Color(1f, 0.28f, 0.12f, 1f),
            39
        );
    }

    private LineRenderer CreateLine(
        string objectName,
        float width,
        Color color,
        int sortingOrder)
    {
        GameObject lineObject = new(objectName);
        lineObject.transform.SetParent(visualRoot.transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.numCapVertices = 5;
        line.sharedMaterial = WeaponCoreDebugVisual.SharedLineMaterial;
        line.sortingLayerName = "Effects";
        line.sortingOrder = sortingOrder;
        return line;
    }

    private void HideLines()
    {
        telegraph.enabled = false;
        beamGlow.enabled = false;
        beamCore.enabled = false;
    }

    private void OnDisable()
    {
        if (visualRoot != null)
        {
            HideLines();
            visualRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (visualRoot != null)
            Destroy(visualRoot);
    }
}
