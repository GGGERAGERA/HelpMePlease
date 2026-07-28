using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(CircleCollider2D))]
public sealed class EvacuationCorridorEvent : WorldEvent
{
    private const float StartRadius = 2.5f;
    private const int PositionAttempts = 32;
    private const int StartVisualSegments = 48;

    public Vector3 StartPosition { get; private set; }
    public Vector3 EndPosition { get; private set; }
    public bool IsPlayerInside { get; private set; }
    public float Progress
    {
        get
        {
            float pathLength = Vector2.Distance(
                StartPosition,
                EndPosition
            );

            if (pathLength <= Mathf.Epsilon)
                return 0f;

            float remaining = Vector2.Distance(
                transform.position,
                EndPosition
            );
            return Mathf.Clamp01(1f - remaining / pathLength);
        }
    }
    public override Vector3 RewardPosition => hasRewardPosition
        ? rewardPosition
        : base.RewardPosition;

    [Header("Corridor")]
    [SerializeField, Min(0.1f)] private float corridorWidth = 3f;
    [SerializeField, Min(0.1f)] private float corridorLength = 8f;
    [SerializeField, Min(0.1f)] private float moveSpeed = 1.5f;
    [SerializeField, Min(0.1f)] private float travelDistance = 10f;

    [Header("Outside Damage")]
    [SerializeField, Min(0f)] private float outsideDamage = 5f;
    [SerializeField, Min(0.1f)] private float outsideDamageInterval = 1f;

    [Header("Visual")]
    [SerializeField] private Material lineMaterial;

    [Header("Scene")]
    [SerializeField] private GameplayAreaService gameplayArea;

    private CircleCollider2D startCollider;
    private BoxCollider2D corridorCollider;
    private LineRenderer startFill;
    private LineRenderer startOutline;
    private LineRenderer corridorFill;
    private LineRenderer corridorOutline;
    private Transform player;
    private PlayerHealth playerHealth;
    private Vector3 rewardPosition;
    private float outsideDamageTimer;
    private bool wasPlayerInside;
    private bool corridorActive;
    private bool hasRewardPosition;

    public override void Initialize(WorldEventSpawner spawner)
    {
        base.Initialize(spawner);
        HUDManager.Instance?.ShowWorldEventMarker(
            transform,
            "EVACUATION"
        );
    }

    private void Awake()
    {
        startCollider = GetComponent<CircleCollider2D>();
        startCollider.isTrigger = true;
        startCollider.radius = StartRadius;

        corridorCollider = gameObject.AddComponent<BoxCollider2D>();
        corridorCollider.isTrigger = true;
        corridorCollider.enabled = false;

        BuildStartVisual();
        BuildCorridorVisual();
    }

    private void Start()
    {
        ResolveSceneReferences();
        FindPlayer();
    }

    private void Update()
    {
        if (!corridorActive ||
            !IsStarted ||
            IsCompleted ||
            Time.timeScale == 0f)
        {
            return;
        }

        if (MoveCorridorToEnd())
        {
            CompleteCorridor();
            return;
        }

        UpdatePlayerInsideState();
        UpdateOutsideDamage();
    }

    protected override bool CanStartFrom(Vector2 playerPosition)
    {
        return Vector2.Distance(transform.position, playerPosition) <=
            StartRadius;
    }

    protected override void OnEventStarted()
    {
        ResolveSceneReferences();
        ConfigureCorridor();

        if (!TryConfigurePath())
        {
            FailCorridor();
            return;
        }

        DisableStartPoint();
        EnableCorridor();
        FindPlayer();
        UpdatePlayerInsideState();
        wasPlayerInside = IsPlayerInside;
        outsideDamageTimer = 0f;
        corridorActive = true;

        RunMessageService.Instance?.ShowCustom(
            "ЭВАКУАЦИОННЫЙ КОРИДОР",
            "Следуйте внутри безопасной зоны до точки эвакуации"
        );
    }

    private bool TryConfigurePath()
    {
        if (gameplayArea == null)
            return false;

        Vector3 eventPosition = transform.position;
        float halfLength = corridorLength * 0.5f;
        float halfWidth = corridorWidth * 0.5f;
        float edgePadding = Mathf.Sqrt(
            halfLength * halfLength + halfWidth * halfWidth
        );
        int firstDirectionIndex = Random.Range(0, 4);

        for (int attempt = 0; attempt < PositionAttempts; attempt++)
        {
            Vector3 candidate;

            if (attempt == 0)
            {
                candidate = eventPosition;
            }
            else if (!gameplayArea.TryGetSpawnPosition(
                         eventPosition,
                         0f,
                         travelDistance,
                         1,
                         edgePadding,
                         out candidate))
            {
                continue;
            }

            for (int directionOffset = 0;
                 directionOffset < 4;
                 directionOffset++)
            {
                int directionIndex =
                    (firstDirectionIndex + directionOffset) % 4;
                Vector2 direction = GetDirection(directionIndex);
                ApplyCorridorOrientation(direction);

                Vector3 end = candidate +
                    (Vector3)(direction * travelDistance);

                if (!IsCorridorInsidePlayableArea(candidate) ||
                    !IsCorridorInsidePlayableArea(end))
                {
                    continue;
                }

                StartPosition = candidate;
                EndPosition = end;
                transform.position = StartPosition;
                return true;
            }
        }

        return false;
    }

    private static Vector2 GetDirection(int directionIndex)
    {
        return directionIndex switch
        {
            0 => Vector2.up,
            1 => Vector2.down,
            2 => Vector2.left,
            _ => Vector2.right
        };
    }

    private void ApplyCorridorOrientation(Vector2 direction)
    {
        bool movesVertically = Mathf.Abs(direction.y) > 0f;
        transform.rotation = movesVertically
            ? Quaternion.Euler(0f, 0f, 90f)
            : Quaternion.identity;
    }

    private bool MoveCorridorToEnd()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            EndPosition,
            moveSpeed * Time.deltaTime
        );

        return Vector2.SqrMagnitude(
            transform.position - EndPosition
        ) <= 0.0001f;
    }

    private void ConfigureCorridor()
    {
        corridorCollider.size = new Vector2(
            Mathf.Max(0.1f, corridorLength),
            Mathf.Max(0.1f, corridorWidth)
        );

        float halfLength = corridorLength * 0.5f;
        float halfWidth = corridorWidth * 0.5f;

        corridorFill.startWidth = corridorWidth;
        corridorFill.endWidth = corridorWidth;
        corridorFill.SetPosition(
            0,
            new Vector3(-halfLength, 0f, 0f)
        );
        corridorFill.SetPosition(
            1,
            new Vector3(halfLength, 0f, 0f)
        );

        corridorOutline.SetPosition(
            0,
            new Vector3(-halfLength, -halfWidth, 0f)
        );
        corridorOutline.SetPosition(
            1,
            new Vector3(-halfLength, halfWidth, 0f)
        );
        corridorOutline.SetPosition(
            2,
            new Vector3(halfLength, halfWidth, 0f)
        );
        corridorOutline.SetPosition(
            3,
            new Vector3(halfLength, -halfWidth, 0f)
        );
    }

    private bool IsCorridorInsidePlayableArea(Vector3 center)
    {
        if (gameplayArea == null || gameplayArea.PlayableArea == null)
            return true;

        Vector2 along = transform.right * (corridorLength * 0.5f);
        Vector2 across = transform.up * (corridorWidth * 0.5f);
        Vector2 center2D = center;

        return gameplayArea.IsInsidePlayableArea(
                center2D + along + across
            ) &&
            gameplayArea.IsInsidePlayableArea(
                center2D + along - across
            ) &&
            gameplayArea.IsInsidePlayableArea(
                center2D - along + across
            ) &&
            gameplayArea.IsInsidePlayableArea(
                center2D - along - across
            );
    }

    private void UpdatePlayerInsideState()
    {
        if (player == null || playerHealth == null)
            FindPlayer();

        IsPlayerInside = player != null &&
            corridorCollider != null &&
            corridorCollider.enabled &&
            corridorCollider.OverlapPoint(player.position);

        if (!IsPlayerInside && wasPlayerInside)
        {
            RunMessageService.Instance?.ShowCustom(
                "ВНЕ КОРИДОРА",
                "Вернитесь в безопасную зону",
                2f
            );
        }

        if (IsPlayerInside)
            outsideDamageTimer = 0f;

        wasPlayerInside = IsPlayerInside;
    }

    private void UpdateOutsideDamage()
    {
        if (IsPlayerInside ||
            playerHealth == null ||
            playerHealth.IsDead)
        {
            return;
        }

        outsideDamageTimer += Time.deltaTime;

        if (outsideDamageTimer < outsideDamageInterval)
            return;

        outsideDamageTimer -= outsideDamageInterval;
        playerHealth.TakeDamage(outsideDamage, Vector2.zero);
    }

    private void DisableStartPoint()
    {
        if (startCollider != null)
            startCollider.enabled = false;
        if (startFill != null)
            startFill.enabled = false;
        if (startOutline != null)
            startOutline.enabled = false;

        HUDManager.Instance?.HideWorldEventMarker();
    }

    private void EnableCorridor()
    {
        corridorCollider.enabled = true;
        corridorFill.enabled = true;
        corridorOutline.enabled = true;
    }

    private void CompleteCorridor()
    {
        transform.position = EndPosition;
        rewardPosition = EndPosition;
        hasRewardPosition = true;
        CleanupEventVisuals();
        CompleteEvent();
    }

    private void FailCorridor()
    {
        if (IsCompleted)
            return;

        CleanupEventVisuals();
        FailEvent();
        Destroy(gameObject);
    }

    private void CleanupEventVisuals()
    {
        corridorActive = false;
        outsideDamageTimer = 0f;
        IsPlayerInside = false;
        wasPlayerInside = false;

        if (startCollider != null)
            startCollider.enabled = false;
        if (corridorCollider != null)
            corridorCollider.enabled = false;
        if (startFill != null)
            startFill.enabled = false;
        if (startOutline != null)
            startOutline.enabled = false;
        if (corridorFill != null)
            corridorFill.enabled = false;
        if (corridorOutline != null)
            corridorOutline.enabled = false;

        HUDManager.Instance?.HideWorldEventMarker();
    }

    private void FindPlayer()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
        {
            player = null;
            playerHealth = null;
            return;
        }

        player = playerObject.transform;
        playerHealth = playerObject.GetComponent<PlayerHealth>();
    }

    private void ResolveSceneReferences()
    {
        if (gameplayArea == null)
            gameplayArea = GameplayAreaService.Instance;

        if (gameplayArea == null)
            gameplayArea = FindFirstObjectByType<GameplayAreaService>();
    }

    private void BuildStartVisual()
    {
        startFill = CreateLineRenderer(
            "EvacuationStartFill",
            -1,
            new Color(0.08f, 0.55f, 0.8f, 0.1f)
        );
        startFill.positionCount = 2;
        startFill.numCapVertices = 32;
        startFill.startWidth = StartRadius * 2f;
        startFill.endWidth = StartRadius * 2f;
        startFill.SetPosition(0, new Vector3(-0.001f, 0f, 0f));
        startFill.SetPosition(1, new Vector3(0.001f, 0f, 0f));

        startOutline = CreateLineRenderer(
            "EvacuationStartOutline",
            0,
            new Color(0.2f, 0.9f, 1f, 0.9f)
        );
        startOutline.loop = true;
        startOutline.positionCount = StartVisualSegments;
        startOutline.startWidth = 0.14f;
        startOutline.endWidth = 0.14f;

        for (int i = 0; i < StartVisualSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / StartVisualSegments;
            startOutline.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * StartRadius,
                    Mathf.Sin(angle) * StartRadius,
                    0f
                )
            );
        }
    }

    private void BuildCorridorVisual()
    {
        corridorFill = CreateLineRenderer(
            "EvacuationCorridorFill",
            0,
            new Color(0.08f, 0.65f, 0.8f, 0.2f)
        );
        corridorFill.positionCount = 2;
        corridorFill.numCapVertices = 0;
        corridorFill.enabled = false;

        corridorOutline = CreateLineRenderer(
            "EvacuationCorridorOutline",
            1,
            new Color(0.2f, 0.95f, 1f, 0.95f)
        );
        corridorOutline.loop = true;
        corridorOutline.positionCount = 4;
        corridorOutline.startWidth = 0.14f;
        corridorOutline.endWidth = 0.14f;
        corridorOutline.enabled = false;
    }

    private LineRenderer CreateLineRenderer(
        string objectName,
        int sortingOrder,
        Color color)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = false;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCornerVertices = 4;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingLayerName = "Midground";
        line.sortingOrder = sortingOrder;
        line.startColor = color;
        line.endColor = color;
        return line;
    }

    private void OnDestroy()
    {
        CleanupEventVisuals();

        if (!IsCompleted)
            FailEvent();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsStarted ? Color.cyan : Color.white;
        Gizmos.matrix = transform.localToWorldMatrix;

        if (IsStarted)
        {
            Gizmos.DrawWireCube(
                Vector3.zero,
                new Vector3(corridorLength, corridorWidth, 0f)
            );
        }
        else
        {
            Gizmos.DrawWireSphere(Vector3.zero, StartRadius);
        }
    }
}
