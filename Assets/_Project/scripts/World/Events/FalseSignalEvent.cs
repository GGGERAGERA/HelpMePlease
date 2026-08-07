using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class FalseSignalEvent : WorldEvent
{
    private enum FalseSignalTrap
    {
        Ambush,
        Blackout
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private enum DebugTrapOverride
    {
        Random,
        Ambush,
        Blackout
    }
#endif

    private const int AmbushTurretCount = 2;
    private const float AmbushWarningDuration = 0.5f;
    private const float BlackoutWarningDuration = 0.4f;
    private const float BlackoutDuration = 3f;
    private const float BlackoutGlobalLightMultiplier = 0.15f;
    private const float TurretMinimumSpawnRadius = 3f;
    private const float TurretMaximumSpawnRadius = 5f;
    private const float TurretMinimumPlayerDistance = 2f;
    private const float TurretSpawnClearance = 0.75f;

    [Header("Start")]
    [SerializeField, Min(0.1f)] private float startRadius = 2.5f;
    [SerializeField] private Material lineMaterial;

    [Header("Signals")]
    [SerializeField] private FalseSignalPoint signalPointPrefab;
    [SerializeField, Min(3)] private int signalPointCount = 3;
    [SerializeField, Min(0f)] private float minPointDistance = 4f;
    [SerializeField, Min(0f)] private float maxPointDistance = 12f;
    [SerializeField, Min(0f)] private float minPointSeparation = 4f;
    [SerializeField, Min(0f)] private float pointEdgePadding = 1f;
    [SerializeField, Min(1)] private int positionAttempts = 24;

    [Header("Failure")]
    [SerializeField, Min(1f)] private float timeLimit = 45f;

    [Header("False Signal Wave")]
    [SerializeField, Min(0)] private int falseSignalEnemyCount = 5;
    [SerializeField, Min(0f)] private float minimumEnemyDistanceFromPlayer = 4f;
    [SerializeField, Min(0f)] private float minimumSpawnRadius = 3f;
    [SerializeField, Min(0f)] private float maximumSpawnRadius = 6f;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private GameObject ambushTurretPrefab;

    [Header("Feedback")]
    [SerializeField, Min(0.1f)] private float feedbackPulseDuration = 0.45f;
    [SerializeField, Min(0.05f)] private float successFadeDuration = 0.35f;
    [SerializeField, Min(0f)] private float ambushShakeDuration = 0.18f;
    [SerializeField, Min(0f)] private float ambushShakeMagnitude = 0.06f;
    [SerializeField] private Color falseSignalPulseColor =
        new(0.95f, 0.08f, 0.04f, 1f);

    [Header("Scene")]
    [SerializeField] private GameplayAreaService gameplayArea;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Debug")]
    [SerializeField] private DebugTrapOverride debugTrapOverride;
#endif

    private readonly List<FalseSignalPoint> signalPoints = new();
    private readonly List<GameObject> spawnedAmbushTurrets = new();
    private readonly Dictionary<FalseSignalPoint, WorldEventMarker>
        signalPointMarkers = new();
    private Collider2D startCollider;
    private LineRenderer startVisual;
    private float timeRemaining;
    private Vector3 rewardPosition;
    private bool hasRewardPosition;
    private bool completionPending;
    private WorldRuleVisual worldRuleVisual;
    private float blackoutRemaining;
    private bool blackoutActive;

    public override Vector3 RewardPosition => hasRewardPosition
        ? rewardPosition
        : base.RewardPosition;

    public override void Initialize(WorldEventSpawner spawner)
    {
        base.Initialize(spawner);
        hasRewardPosition = false;
        completionPending = false;
        blackoutRemaining = 0f;
        blackoutActive = false;
        spawnedAmbushTurrets.Clear();
        ShowEventMarker(transform, "FALSE SIGNAL");
    }

    private void Awake()
    {
        startCollider = GetComponent<Collider2D>();
        BuildStartVisual();
    }

    private void Update()
    {
        if (!IsStarted || IsCompleted || completionPending ||
            Time.timeScale == 0f)
        {
            return;
        }

        timeRemaining -= Time.deltaTime;

        UpdateBlackout();

        if (timeRemaining <= 0f)
            FailFalseSignal();
    }

    protected override bool CanStartFrom(Vector2 playerPosition)
    {
        return Vector2.Distance(transform.position, playerPosition) <=
            startRadius;
    }

    protected override void OnEventStarted()
    {
        if (startCollider != null)
            startCollider.enabled = false;
        if (startVisual != null)
            startVisual.enabled = false;

        HideEventMarker();
        ResolveSceneReferences();
        timeRemaining = timeLimit;

        if (!SpawnSignalPoints())
        {
            FailFalseSignal();
            return;
        }

    }

    public void ResolveSignal(FalseSignalPoint signalPoint, bool isReal)
    {
        if (!IsStarted || IsCompleted || completionPending ||
            signalPoint == null)
        {
            return;
        }

        RemoveSignalPointMarker(signalPoint);

        if (!isReal)
        {
            FalseSignalTrap trap = ChooseFalseSignalTrap();
            float warningDuration = trap == FalseSignalTrap.Ambush
                ? AmbushWarningDuration
                : BlackoutWarningDuration;

            signalPoint.BeginTrapWarning(falseSignalPulseColor);
            StartCoroutine(ResolveFalseSignalTrapAfterWarning(
                signalPoint,
                trap,
                warningDuration
            ));
            return;
        }

        rewardPosition = signalPoint.transform.position;
        hasRewardPosition = true;
        signalPoints.Remove(signalPoint);
        Destroy(signalPoint.gameObject);
        completionPending = true;
        FadeRemainingSignalPoints();
        CompleteEvent();
    }

    private void SpawnAmbushTurrets(Vector3 origin)
    {
        if (enemySpawner == null || ambushTurretPrefab == null)
            return;

        float minimumPlayerDistance = Mathf.Max(
            TurretMinimumPlayerDistance,
            minimumEnemyDistanceFromPlayer
        );

        for (int i = 0; i < AmbushTurretCount; i++)
        {
            GameObject turret = enemySpawner.SpawnSpecificEnemyAround(
                ambushTurretPrefab,
                origin,
                TurretMinimumSpawnRadius,
                TurretMaximumSpawnRadius,
                minimumPlayerDistance,
                true,
                TurretSpawnClearance
            );

            if (turret != null)
                spawnedAmbushTurrets.Add(turret);
        }
    }

    private IEnumerator ResolveFalseSignalTrapAfterWarning(
        FalseSignalPoint signalPoint,
        FalseSignalTrap trap,
        float warningDuration)
    {
        yield return new WaitForSeconds(warningDuration);

        if (!IsStarted || IsCompleted)
            yield break;

        Vector3 trapPosition = signalPoint != null
            ? signalPoint.transform.position
            : transform.position;

        signalPoints.Remove(signalPoint);

        if (signalPoint != null)
            Destroy(signalPoint.gameObject);

        switch (trap)
        {
            case FalseSignalTrap.Ambush:
                TriggerAmbush(trapPosition);
                break;

            case FalseSignalTrap.Blackout:
                TriggerBlackout();
                break;
        }
    }

    private void TriggerAmbush(Vector3 origin)
    {
        RunMessageService.Instance?.ShowWorldEventFeedback(
            "ЛОЖНЫЙ СИГНАЛ",
            "ЗАСАДА",
            falseSignalPulseColor,
            feedbackPulseDuration
        );
        CameraShake.Instance?.Shake(
            ambushShakeDuration,
            ambushShakeMagnitude
        );
        enemySpawner?.SpawnAdditionalWave(
            origin,
            falseSignalEnemyCount,
            minimumSpawnRadius,
            maximumSpawnRadius,
            minimumEnemyDistanceFromPlayer
        );
        SpawnAmbushTurrets(origin);
    }

    private void TriggerBlackout()
    {
        RunMessageService.Instance?.ShowWorldEventFeedback(
            "ЛОЖНЫЙ СИГНАЛ",
            "BLACKOUT",
            falseSignalPulseColor,
            feedbackPulseDuration
        );

        blackoutRemaining = BlackoutDuration;
        blackoutActive = true;
        worldRuleVisual?.SetBlackoutGlobalLightMultiplier(
            this,
            BlackoutGlobalLightMultiplier
        );
    }

    private void UpdateBlackout()
    {
        if (!blackoutActive)
            return;

        blackoutRemaining = Mathf.Max(
            0f,
            blackoutRemaining - Time.deltaTime
        );

        if (blackoutRemaining > 0f)
            return;

        RemoveBlackoutModifier();
    }

    private void RemoveBlackoutModifier()
    {
        if (!blackoutActive)
            return;

        blackoutActive = false;
        blackoutRemaining = 0f;
        worldRuleVisual?.RemoveBlackoutGlobalLightMultiplier(this);
    }

    private FalseSignalTrap ChooseFalseSignalTrap()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugTrapOverride == DebugTrapOverride.Ambush)
        {
            debugTrapOverride = DebugTrapOverride.Random;
            return FalseSignalTrap.Ambush;
        }

        if (debugTrapOverride == DebugTrapOverride.Blackout)
        {
            debugTrapOverride = DebugTrapOverride.Random;
            return FalseSignalTrap.Blackout;
        }
#endif

        return Random.value < 0.5f
            ? FalseSignalTrap.Ambush
            : FalseSignalTrap.Blackout;
    }

    public void HandleSignalPointDestroyed(FalseSignalPoint signalPoint)
    {
        if (signalPoint == null)
            return;

        signalPoints.Remove(signalPoint);
        RemoveSignalPointMarker(signalPoint);
    }

    private bool SpawnSignalPoints()
    {
        if (signalPointPrefab == null || gameplayArea == null)
            return false;

        List<Vector3> positions = new();

        for (int i = 0; i < signalPointCount; i++)
        {
            if (!TryGetSignalPosition(positions, out Vector3 position))
                return false;

            positions.Add(position);
        }

        int realSignalIndex = Random.Range(0, positions.Count);

        for (int i = 0; i < positions.Count; i++)
        {
            FalseSignalPoint point = Instantiate(
                signalPointPrefab,
                positions[i],
                Quaternion.identity,
                transform
            );
            point.Initialize(this, i == realSignalIndex);
            signalPoints.Add(point);

            WorldEventMarker marker =
                HUDManager.Instance?.CreateWorldEventMarker(
                    point.transform,
                    "SIGNAL"
                );
            signalPointMarkers[point] = marker;
        }

        return true;
    }

    private bool TryGetSignalPosition(
        List<Vector3> existingPositions,
        out Vector3 position)
    {
        for (int i = 0; i < positionAttempts; i++)
        {
            if (!gameplayArea.TryGetSpawnPosition(
                    transform.position,
                    minPointDistance,
                    maxPointDistance,
                    1,
                    pointEdgePadding,
                    out Vector3 candidate))
            {
                continue;
            }

            bool separated = true;

            for (int j = 0; j < existingPositions.Count; j++)
            {
                if (Vector2.Distance(candidate, existingPositions[j]) <
                    minPointSeparation)
                {
                    separated = false;
                    break;
                }
            }

            if (!separated)
                continue;

            position = candidate;
            return true;
        }

        position = default;
        return false;
    }

    private void ResolveSceneReferences()
    {
        if (gameplayArea == null)
            gameplayArea = GameplayAreaService.Instance;

        if (enemySpawner == null)
            enemySpawner = FindFirstObjectByType<EnemySpawner>();

        if (worldRuleVisual == null)
            worldRuleVisual = FindFirstObjectByType<WorldRuleVisual>();
    }

    private void BuildStartVisual()
    {
        if (lineMaterial == null)
            return;

        const int Segments = 48;
        startVisual = gameObject.AddComponent<LineRenderer>();
        startVisual.sharedMaterial = lineMaterial;
        startVisual.useWorldSpace = false;
        startVisual.loop = true;
        startVisual.positionCount = Segments;
        startVisual.startWidth = 0.12f;
        startVisual.endWidth = 0.12f;
        startVisual.startColor = Color.magenta;
        startVisual.endColor = Color.magenta;
        startVisual.sortingLayerName = "Midground";
        startVisual.sortingOrder = 1;

        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            startVisual.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * startRadius,
                    Mathf.Sin(angle) * startRadius,
                    0f
                )
            );
        }
    }

    private void FailFalseSignal()
    {
        FailEvent();
        Destroy(gameObject);
    }

    private void FadeRemainingSignalPoints()
    {
        for (int i = 0; i < signalPoints.Count; i++)
        {
            FalseSignalPoint point = signalPoints[i];

            if (point == null)
                continue;

            RemoveSignalPointMarker(point);
            point.FadeOutAndDestroy(successFadeDuration);
        }

        signalPoints.Clear();
    }

    protected override void CleanupEvent()
    {
        StopAllCoroutines();
        RemoveBlackoutModifier();
        CleanupSignalPointMarkers();

        for (int i = 0; i < signalPoints.Count; i++)
        {
            if (signalPoints[i] != null)
                Destroy(signalPoints[i].gameObject);
        }

        signalPoints.Clear();

        if (IsDebugCleanup)
        {
            for (int i = 0; i < spawnedAmbushTurrets.Count; i++)
            {
                if (spawnedAmbushTurrets[i] != null)
                    Destroy(spawnedAmbushTurrets[i]);
            }
        }

        spawnedAmbushTurrets.Clear();
    }

    public override void CollectTacticalMapMarkers(
        List<TacticalMapMarkerDescriptor> markers)
    {
        base.CollectTacticalMapMarkers(markers);

        if (markers == null || !IsStarted || IsCompleted)
            return;

        for (int i = 0; i < signalPoints.Count; i++)
        {
            FalseSignalPoint point = signalPoints[i];

            if (point != null)
            {
                markers.Add(new TacticalMapMarkerDescriptor(
                    TacticalMapMarkerKind.Objective,
                    point.transform.position
                ));
            }
        }
    }

    private void RemoveSignalPointMarker(FalseSignalPoint signalPoint)
    {
        if (!signalPointMarkers.TryGetValue(
                signalPoint,
                out WorldEventMarker marker))
        {
            return;
        }

        HUDManager.Instance?.RemoveWorldEventMarker(marker);
        signalPointMarkers.Remove(signalPoint);
    }

    private void CleanupSignalPointMarkers()
    {
        foreach (WorldEventMarker marker in signalPointMarkers.Values)
            HUDManager.Instance?.RemoveWorldEventMarker(marker);

        signalPointMarkers.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, startRadius);
    }
}
