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
    [SerializeField, Min(0.005f)] private float lineWidth = 0.035f;

    private GravityAnomalySiteController gravitySite;
    private TrajectorySlot[] slots;
    private EnemyHealth[] nearestTargets;
    private float[] nearestDistances;
    private Vector3[] pathPoints;
    private Transform player;
    private Material lineMaterial;
    private float nextTargetRefresh;
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

    private void Update()
    {
        bool siteActive = gravitySite != null &&
            gravitySite.IsOrbitalGravityActive &&
            gravitySite.ActiveOrbitZone != null;

        if (siteActive && !wasSiteActive)
        {
            previewEnabled = true;
            nextTargetRefresh = 0f;
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

        RenderTrajectories(gravitySite.ActiveOrbitZone);
    }

    private void BuildPool()
    {
        maxTargets = Mathf.Max(1, maxTargets);
        segments = Mathf.Max(2, segments);
        slots = new TrajectorySlot[maxTargets];
        nearestTargets = new EnemyHealth[maxTargets];
        nearestDistances = new float[maxTargets];
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
            nearestTargets[i] = null;
            nearestDistances[i] = float.PositiveInfinity;
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

            float distance = (position - playerPosition).sqrMagnitude;
            InsertNearest(enemy, distance);
        }

        ActiveTargetCount = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            AssignTarget(slots[i], nearestTargets[i]);
            if (slots[i].Target != null)
                ActiveTargetCount++;
        }
    }

    private void InsertNearest(EnemyHealth enemy, float distance)
    {
        for (int i = 0; i < maxTargets; i++)
        {
            if (distance >= nearestDistances[i])
                continue;

            for (int j = maxTargets - 1; j > i; j--)
            {
                nearestTargets[j] = nearestTargets[j - 1];
                nearestDistances[j] = nearestDistances[j - 1];
            }

            nearestTargets[i] = enemy;
            nearestDistances[i] = distance;
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

    private void RenderTrajectories(GravityZone gravityZone)
    {
        float deltaTime = Mathf.Max(0f, Time.deltaTime);
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
            UpdateObservedVelocity(slot, currentPosition, deltaTime);
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
