using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public sealed class GravityTrajectoryPreview : MonoBehaviour
{
    private sealed class TrajectorySlot
    {
        public EnemyHealth Target;
        public EnemyMovement Movement;
        public LineRenderer Line;
        public Vector2 LastPosition;
        public Vector2 ObservedVelocity;
        public bool HasMotionSample;
    }

    [SerializeField, Range(1, 8)] private int maxTargets = 5;
    [SerializeField, Range(0.2f, 2f)] private float predictionTime = 1.5f;
    [SerializeField, Range(2, 24)] private int segments = 16;
    [SerializeField, Min(0.05f)] private float targetRefreshInterval = 0.2f;
    [SerializeField, Range(3f, 15f)] private float maxThreatDistance = 9f;
    [SerializeField, Range(1f, 5f)] private float threatClosestApproach = 3f;
    [SerializeField, Min(0.016f)] private float trajectoryRenderInterval = 0.033f;
    [SerializeField, Min(0.005f)] private float lineWidth = 0.035f;

    private GravityAnomalySiteController gravitySite;
    private TrajectorySlot[] slots;
    private EnemyHealth[] threatTargets;
    private float[] threatScores;
    private Vector3[] pathPoints;
    private Transform player;
    private Material lineMaterial;
    private float nextTargetRefresh;
    private float nextTrajectoryRender;
    private float lastTrajectoryRender;
    private bool previewEnabled = true;
    private bool wasSiteActive;
    private int predictionTimeIndex = 2;

    private static readonly float[] PredictionTimes =
        { 0.75f, 1.25f, 1.5f, 2f };

    public bool PreviewEnabled => previewEnabled;
    public int ActiveTargetCount { get; private set; }
    public int MaxTargets => maxTargets;
    public float PredictionTime => predictionTime;

    public void Configure(GravityAnomalySiteController site)
    {
        gravitySite = site;
        BuildPool();
        HideAll();
    }

    public void SetPreviewEnabled(bool enabled)
    {
        previewEnabled = enabled;
        if (!enabled)
            HideAll();
        else
            nextTargetRefresh = 0f;
    }

    public void SetPredictionTime(float seconds)
    {
        int closestIndex = 0;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < PredictionTimes.Length; i++)
        {
            float distance = Mathf.Abs(PredictionTimes[i] - seconds);
            if (distance >= closestDistance)
                continue;
            closestDistance = distance;
            closestIndex = i;
        }
        predictionTimeIndex = closestIndex;
        predictionTime = PredictionTimes[closestIndex];
        nextTrajectoryRender = 0f;
    }

    private void Update()
    {
        bool siteActive = gravitySite != null &&
            gravitySite.IsOrbitalGravityActive &&
            gravitySite.ActiveOrbitZone != null;

        if (siteActive && !wasSiteActive)
        {
            previewEnabled = true;
            nextTargetRefresh = 0f;
            nextTrajectoryRender = 0f;
            lastTrajectoryRender = Time.unscaledTime;
        }

        wasSiteActive = siteActive;

        if (siteActive && Input.GetKeyDown(KeyCode.T))
        {
            previewEnabled = !previewEnabled;
            if (!previewEnabled)
                HideAll();
            else
                nextTargetRefresh = 0f;
        }

        if (siteActive && Input.GetKeyDown(KeyCode.Y))
        {
            predictionTimeIndex =
                (predictionTimeIndex + 1) % PredictionTimes.Length;
            predictionTime = PredictionTimes[predictionTimeIndex];
        }

        if (!siteActive || !previewEnabled)
        {
            HideAll();
            return;
        }

        ResolvePlayer();
        if (player == null)
        {
            HideAll();
            return;
        }

        if (Time.unscaledTime >= nextTargetRefresh)
        {
            RefreshTargets(gravitySite.ActiveOrbitZone);
            nextTargetRefresh = Time.unscaledTime + targetRefreshInterval;
        }

        if (Time.unscaledTime >= nextTrajectoryRender)
        {
            float renderDelta = Mathf.Max(
                0.0001f,
                Time.unscaledTime - lastTrajectoryRender
            );
            lastTrajectoryRender = Time.unscaledTime;
            nextTrajectoryRender =
                Time.unscaledTime + trajectoryRenderInterval;
            RenderTrajectories(
                gravitySite.ActiveOrbitZone,
                renderDelta
            );
        }
    }

    private void BuildPool()
    {
        maxTargets = Mathf.Max(1, maxTargets);
        segments = Mathf.Max(2, segments);
        slots = new TrajectorySlot[maxTargets];
        threatTargets = new EnemyHealth[maxTargets];
        threatScores = new float[maxTargets];
        pathPoints = new Vector3[segments + 1];

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
        {
            lineMaterial = new Material(shader)
            {
                name = "Gravity Trajectory Preview Material",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.55f, 1f, 1f), 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.75f, 0f),
                new GradientAlphaKey(0.08f, 1f)
            }
        );

        for (int i = 0; i < slots.Length; i++)
        {
            GameObject lineObject = new($"Gravity Trajectory {i + 1}");
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = pathPoints.Length;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth * 0.45f;
            line.colorGradient = gradient;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.sortingLayerName = "Foreground";
            line.sortingOrder = 20;
            if (lineMaterial != null)
                line.sharedMaterial = lineMaterial;
            line.enabled = false;
            slots[i] = new TrajectorySlot { Line = line };
        }
    }

    private void ResolvePlayer()
    {
        if (player != null)
            return;
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    private void RefreshTargets(GravityZone gravityZone)
    {
        for (int i = 0; i < maxTargets; i++)
        {
            threatTargets[i] = null;
            threatScores[i] = float.PositiveInfinity;
        }

        Vector2 playerPosition = player.position;
        foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
        {
            if (enemy == null || enemy.IsDead ||
                !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector2 position = enemy.transform.position;
            if (!gravityZone.DebugContainsWorldPosition(position))
                continue;

            Vector2 playerOffset = playerPosition - position;
            float distanceSquared = playerOffset.sqrMagnitude;
            if (distanceSquared > maxThreatDistance * maxThreatDistance)
                continue;

            EnemyMovement movement = enemy.GetComponent<EnemyMovement>();
            if (movement == null)
                continue;

            if (!TryCalculateThreatScore(
                    enemy,
                    movement,
                    gravityZone,
                    position,
                    playerPosition,
                    distanceSquared,
                    out float score))
            {
                continue;
            }

            InsertThreat(enemy, score);
        }

        ActiveTargetCount = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            AssignTarget(slots[i], threatTargets[i]);
            if (slots[i].Target != null)
                ActiveTargetCount++;
        }
    }

    private bool TryCalculateThreatScore(
        EnemyHealth enemy,
        EnemyMovement movement,
        GravityZone gravityZone,
        Vector2 startPosition,
        Vector2 playerPosition,
        float startDistanceSquared,
        out float score)
    {
        score = float.PositiveInfinity;
        Vector2 observedVelocity = GetCandidateVelocity(enemy, playerPosition);
        Vector2 currentGravity =
            gravityZone.GetDebugPredictedExternalVelocity(
                startPosition,
                movement
            );
        Vector2 baseVelocity = observedVelocity - currentGravity;
        Vector2 predictedPosition = startPosition;
        float predictionStep = predictionTime / segments;
        float closestDistanceSquared = startDistanceSquared;
        float timeToClosest = 0f;

        for (int point = 1; point <= segments; point++)
        {
            Vector2 predictedGravity =
                gravityZone.GetDebugPredictedExternalVelocity(
                    predictedPosition,
                    movement
                );
            predictedPosition +=
                (baseVelocity + predictedGravity) * predictionStep;
            float distanceSquared =
                (predictedPosition - playerPosition).sqrMagnitude;
            if (distanceSquared >= closestDistanceSquared)
                continue;

            closestDistanceSquared = distanceSquared;
            timeToClosest = point * predictionStep;
        }

        Vector2 toPlayer = playerPosition - startPosition;
        Vector2 initialVelocity = baseVelocity + currentGravity;
        float approach = initialVelocity.sqrMagnitude > 0.01f &&
            toPlayer.sqrMagnitude > 0.01f
            ? Vector2.Dot(initialVelocity.normalized, toPlayer.normalized)
            : 0f;
        float closestDistance = Mathf.Sqrt(closestDistanceSquared);
        bool directThreat = closestDistance <= threatClosestApproach;
        bool approachingThreat = approach >= 0.25f &&
            closestDistance <= threatClosestApproach + 2f &&
            closestDistanceSquared < startDistanceSquared - 0.25f;
        if (!directThreat && !approachingThreat)
            return false;

        float currentDistance = Mathf.Sqrt(startDistanceSquared);
        score = closestDistance * 2f +
            timeToClosest * 0.7f +
            currentDistance * 0.25f -
            Mathf.Max(0f, approach) * 1.5f;
        return true;
    }

    private Vector2 GetCandidateVelocity(
        EnemyHealth enemy,
        Vector2 playerPosition)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            TrajectorySlot slot = slots[i];
            if (slot.Target == enemy && slot.HasMotionSample &&
                slot.ObservedVelocity.sqrMagnitude > 0.01f)
            {
                return slot.ObservedVelocity;
            }
        }

        Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
        if (body != null && body.linearVelocity.sqrMagnitude > 0.01f)
            return body.linearVelocity;

        Vector2 toPlayer = playerPosition - (Vector2)enemy.transform.position;
        return toPlayer.sqrMagnitude > 0.01f
            ? toPlayer.normalized * 2f
            : Vector2.zero;
    }

    private void InsertThreat(EnemyHealth enemy, float score)
    {
        for (int i = 0; i < maxTargets; i++)
        {
            if (score >= threatScores[i])
                continue;

            for (int j = maxTargets - 1; j > i; j--)
            {
                threatTargets[j] = threatTargets[j - 1];
                threatScores[j] = threatScores[j - 1];
            }

            threatTargets[i] = enemy;
            threatScores[i] = score;
            return;
        }
    }

    private static void AssignTarget(
        TrajectorySlot slot,
        EnemyHealth target)
    {
        if (slot.Target == target)
            return;

        slot.Target = target;
        slot.Movement = target != null
            ? target.GetComponent<EnemyMovement>()
            : null;
        slot.HasMotionSample = false;
        slot.ObservedVelocity = Vector2.zero;
        slot.Line.enabled = target != null;
    }

    private void RenderTrajectories(
        GravityZone gravityZone,
        float sampleDeltaTime)
    {
        float predictionStep = predictionTime / segments;

        for (int i = 0; i < slots.Length; i++)
        {
            TrajectorySlot slot = slots[i];
            EnemyHealth enemy = slot.Target;
            if (enemy == null || enemy.IsDead || slot.Movement == null ||
                !gravityZone.DebugContainsWorldPosition(
                    enemy.transform.position))
            {
                slot.Line.enabled = false;
                continue;
            }

            Vector2 currentPosition = enemy.transform.position;
            UpdateObservedVelocity(
                slot,
                currentPosition,
                sampleDeltaTime
            );
            Vector2 currentGravity =
                gravityZone.GetDebugPredictedExternalVelocity(
                    currentPosition,
                    slot.Movement
                );
            Vector2 baseVelocity = slot.ObservedVelocity - currentGravity;
            Vector2 predictedPosition = currentPosition;
            pathPoints[0] = predictedPosition;

            for (int point = 1; point < pathPoints.Length; point++)
            {
                Vector2 predictedGravity =
                    gravityZone.GetDebugPredictedExternalVelocity(
                        predictedPosition,
                        slot.Movement
                    );
                predictedPosition +=
                    (baseVelocity + predictedGravity) * predictionStep;
                pathPoints[point] = predictedPosition;
            }

            slot.Line.enabled = true;
            slot.Line.SetPositions(pathPoints);
        }
    }

    private static void UpdateObservedVelocity(
        TrajectorySlot slot,
        Vector2 position,
        float deltaTime)
    {
        if (!slot.HasMotionSample || deltaTime <= 0.0001f)
        {
            slot.LastPosition = position;
            slot.HasMotionSample = true;
            return;
        }

        Vector2 instantaneous = (position - slot.LastPosition) / deltaTime;
        instantaneous = Vector2.ClampMagnitude(instantaneous, 20f);
        float blend = 1f - Mathf.Exp(-12f * deltaTime);
        slot.ObservedVelocity = Vector2.Lerp(
            slot.ObservedVelocity,
            instantaneous,
            blend
        );
        slot.LastPosition = position;
    }

    private void HideAll()
    {
        ActiveTargetCount = 0;
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].Line.enabled = false;
            slots[i].Target = null;
            slots[i].Movement = null;
            slots[i].HasMotionSample = false;
        }
    }

    private void OnDestroy()
    {
        if (lineMaterial != null)
            Destroy(lineMaterial);
    }
}
#endif
