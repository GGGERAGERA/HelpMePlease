using UnityEngine;

public sealed class GameplayAreaService : MonoBehaviour
{
    public static GameplayAreaService Instance { get; private set; }

    [Header("Scene Areas")]
    [SerializeField] private Collider2D playableArea;
    [SerializeField] private Collider2D spawnArea;

    public Collider2D PlayableArea => playableArea;
    public Collider2D SpawnArea => spawnArea;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "[GameplayAreaService] More than one service exists in the scene.",
                this
            );
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void ConfigureDebugAreas(
        Collider2D playable,
        Collider2D spawn)
    {
        playableArea = playable;
        spawnArea = spawn;
    }
#endif

    public bool IsInsidePlayableArea(Vector2 position)
    {
        return IsInside(playableArea, position, 0f);
    }

    public bool IsInsidePlayableArea(
        Vector2 position,
        float edgePadding)
    {
        return IsInside(playableArea, position, edgePadding);
    }

    public bool IsInsideSpawnArea(Vector2 position)
    {
        return IsInside(spawnArea, position, 0f);
    }

    public bool IsInsideSpawnArea(
        Vector2 position,
        float edgePadding)
    {
        return IsInside(spawnArea, position, edgePadding);
    }

    public bool TryGetSpawnPosition(
        Vector3 origin,
        float minDistance,
        float maxDistance,
        int attempts,
        out Vector3 position)
    {
        return TryGetSpawnPosition(
            origin,
            minDistance,
            maxDistance,
            attempts,
            0f,
            out position
        );
    }

    public bool TryGetSpawnPosition(
        Vector3 origin,
        float minDistance,
        float maxDistance,
        int attempts,
        float edgePadding,
        out Vector3 position)
    {
        position = default;

        if (spawnArea == null || !spawnArea.enabled)
            return false;

        float minimum = Mathf.Max(0f, minDistance);
        float maximum = Mathf.Max(minimum, maxDistance);
        int sampleCount = Mathf.Max(1, attempts);
        float padding = Mathf.Max(0f, edgePadding);

        for (int i = 0; i < sampleCount; i++)
        {
            Vector2 direction = Random.insideUnitCircle;

            if (direction.sqrMagnitude <= Mathf.Epsilon)
                direction = Vector2.right;
            else
                direction.Normalize();

            float distance = Random.Range(minimum, maximum);
            Vector2 candidate = (Vector2)origin + direction * distance;

            if (IsInside(spawnArea, candidate, padding))
            {
                position = new Vector3(candidate.x, candidate.y, origin.z);
                return true;
            }
        }

        Bounds bounds = spawnArea.bounds;

        for (int i = 0; i < sampleCount; i++)
        {
            Vector2 candidate = new(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y)
            );

            if (IsInside(spawnArea, candidate, padding))
            {
                position = new Vector3(candidate.x, candidate.y, origin.z);
                return true;
            }
        }

        return false;
    }

    private static bool IsInside(
        Collider2D area,
        Vector2 position,
        float edgePadding)
    {
        if (area == null || !area.enabled || !area.OverlapPoint(position))
            return false;

        if (edgePadding <= 0f)
            return true;

        const int PaddingSamples = 12;

        for (int i = 0; i < PaddingSamples; i++)
        {
            float angle = i * Mathf.PI * 2f / PaddingSamples;
            Vector2 offset = new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) * edgePadding;

            if (!area.OverlapPoint(position + offset))
                return false;
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ValidateArea(playableArea, nameof(playableArea));
        ValidateArea(spawnArea, nameof(spawnArea));
    }

    private void ValidateArea(Collider2D area, string fieldName)
    {
        if (area != null && !area.isTrigger)
        {
            Debug.LogWarning(
                $"[GameplayAreaService] {fieldName} must be a trigger.",
                this
            );
        }
    }
#endif
}
