using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class FootballArenaLayout : MonoBehaviour
{
    private const float MinRatioTotal = 0.0001f;
    private const float MinScale = 0.0001f;
    private const float ChangeEpsilon = 0.0001f;

    [Header("Arena")]
    [SerializeField] private BoxCollider2D arenaBounds;

    [Header("Zones")]
    [SerializeField] private BoxCollider2D ballsZone;
    [SerializeField] private BoxCollider2D anomalyZone;
    [SerializeField] private BoxCollider2D targetsZone;

    [Header("Zone Visuals")]
    [SerializeField] private SpriteRenderer ballsVisual;
    [SerializeField] private SpriteRenderer anomalyVisual;
    [SerializeField] private SpriteRenderer targetsVisual;

    [Header("Zone Ratios")]
    [Range(0.05f, 0.9f)]
    [SerializeField] private float ballsRatio = 0.20f;
    [Range(0.05f, 0.9f)]
    [SerializeField] private float anomalyRatio = 0.40f;
    [Range(0.05f, 0.9f)]
    [SerializeField] private float targetsRatio = 0.40f;

    [Header("Optional Content Roots")]
    [Tooltip("Leave empty when the zone itself is the content root.")]
    [SerializeField] private Transform ballsContent;
    [SerializeField] private Transform anomalyContent;
    [SerializeField] private Transform targetsContent;

    [Header("Optional Player Boundary")]
    [SerializeField] private BoxCollider2D playerTopBoundary;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    private Vector2 lastArenaSize;
    private Vector2 lastArenaOffset;
    private Matrix4x4 lastArenaLocalToWorld;
    private float lastBallsRatio;
    private float lastAnomalyRatio;
    private float lastTargetsRatio;
    private bool hasInputSnapshot;

    public Bounds ArenaWorldBounds => arenaBounds != null ? arenaBounds.bounds : default;
    public Bounds BallsWorldBounds => ballsZone != null ? ballsZone.bounds : default;
    public Bounds AnomalyWorldBounds => anomalyZone != null ? anomalyZone.bounds : default;
    public Bounds TargetsWorldBounds => targetsZone != null ? targetsZone.bounds : default;

    private void Awake()
    {
        ApplyLayoutInternal(false);
    }

    private void OnEnable()
    {
        ApplyLayoutInternal(false);
    }

    private void Update()
    {
        if (Application.isPlaying)
            return;

        if (LayoutInputsChanged())
        {
            ApplyLayoutInternal(false);
            return;
        }

#if UNITY_EDITOR
        if (SynchronizeVisualsFromColliders())
            MarkVisualsAndSceneDirty();
#else
        SynchronizeVisualsFromColliders();
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // The serialized Inspector change already owns the Undo operation.
        // Reapplying from that value also makes Ctrl+Z rebuild the previous layout.
        ApplyLayoutInternal(false);
    }
#endif

    [ContextMenu("Apply Layout Now")]
    public void ApplyLayout()
    {
        ApplyLayoutInternal(true);
    }

    private void ApplyLayoutInternal(bool recordEditorUndo)
    {
        if (!HasRequiredReferences())
        {
            hasInputSnapshot = false;
            return;
        }

        float ratioTotal = ballsRatio + anomalyRatio + targetsRatio;
        if (ratioTotal <= MinRatioTotal)
        {
            hasInputSnapshot = false;
            return;
        }

        float normalizedBalls = ballsRatio / ratioTotal;
        float normalizedAnomaly = anomalyRatio / ratioTotal;
        float normalizedTargets = targetsRatio / ratioTotal;
        Vector2 arenaSize = arenaBounds.size;
        Vector2 arenaOffset = arenaBounds.offset;
        float ballsHeight = arenaSize.y * normalizedBalls;
        float anomalyHeight = arenaSize.y * normalizedAnomaly;
        float targetsHeight = arenaSize.y * normalizedTargets;
        float arenaBottom = arenaOffset.y - arenaSize.y * 0.5f;

#if UNITY_EDITOR
        if (!Application.isPlaying && recordEditorUndo)
            RecordLayoutUndo();
#endif

        bool changed = false;
        changed |= ConfigureZone(
            ballsZone,
            ballsVisual,
            new Vector2(arenaOffset.x, arenaBottom + ballsHeight * 0.5f),
            new Vector2(arenaSize.x, ballsHeight));
        changed |= ConfigureZone(
            anomalyZone,
            anomalyVisual,
            new Vector2(arenaOffset.x, arenaBottom + ballsHeight + anomalyHeight * 0.5f),
            new Vector2(arenaSize.x, anomalyHeight));
        changed |= ConfigureZone(
            targetsZone,
            targetsVisual,
            new Vector2(arenaOffset.x, arenaBottom + ballsHeight + anomalyHeight + targetsHeight * 0.5f),
            new Vector2(arenaSize.x, targetsHeight));

        changed |= PositionContentRoot(ballsContent, ballsZone);
        changed |= PositionContentRoot(anomalyContent, anomalyZone);
        changed |= PositionContentRoot(targetsContent, targetsZone);
        changed |= ConfigurePlayerBoundary(
            arenaSize,
            new Vector2(arenaOffset.x, arenaBottom + ballsHeight));

        CacheLayoutInputs();

#if UNITY_EDITOR
        if (!Application.isPlaying && changed)
            MarkLayoutDirty();
#endif
    }

    private bool HasRequiredReferences()
    {
        return arenaBounds != null && ballsZone != null && anomalyZone != null && targetsZone != null;
    }

    private bool ConfigureZone(
        BoxCollider2D zone,
        SpriteRenderer visual,
        Vector2 centerInArena,
        Vector2 sizeInArena)
    {
        Transform zoneTransform = zone.transform;
        Vector3 worldCenter = arenaBounds.transform.TransformPoint(centerInArena);
        Quaternion worldRotation = arenaBounds.transform.rotation;
        bool changed = SetWorldPose(zoneTransform, worldCenter, worldRotation);
        Vector2 colliderSize = ConvertArenaSizeToZoneSize(zoneTransform, sizeInArena);

        changed |= SetCollider(zone, colliderSize, true);
        changed |= ConfigureVisual(visual, zone);
        return changed;
    }

    private Vector2 ConvertArenaSizeToZoneSize(Transform zoneTransform, Vector2 sizeInArena)
    {
        Vector3 arenaScale = arenaBounds.transform.lossyScale;
        Vector3 zoneScale = zoneTransform.lossyScale;
        return new Vector2(
            sizeInArena.x * Mathf.Abs(arenaScale.x) / Mathf.Max(Mathf.Abs(zoneScale.x), MinScale),
            sizeInArena.y * Mathf.Abs(arenaScale.y) / Mathf.Max(Mathf.Abs(zoneScale.y), MinScale));
    }

    private static bool SetWorldPose(Transform target, Vector3 position, Quaternion rotation)
    {
        if (Approximately(target.position, position) && Approximately(target.rotation, rotation))
            return false;

        target.SetPositionAndRotation(position, rotation);
        return true;
    }

    private static bool SetCollider(BoxCollider2D collider, Vector2 size, bool isTrigger)
    {
        bool changed = false;
        if (!Approximately(collider.offset, Vector2.zero))
        {
            collider.offset = Vector2.zero;
            changed = true;
        }

        if (!Approximately(collider.size, size))
        {
            collider.size = size;
            changed = true;
        }

        if (collider.isTrigger != isTrigger)
        {
            collider.isTrigger = isTrigger;
            changed = true;
        }

        return changed;
    }

    private bool SynchronizeVisualsFromColliders()
    {
        bool changed = false;
        changed |= ConfigureVisual(ballsVisual, ballsZone);
        changed |= ConfigureVisual(anomalyVisual, anomalyZone);
        changed |= ConfigureVisual(targetsVisual, targetsZone);
        return changed;
    }

    private static bool ConfigureVisual(SpriteRenderer visual, BoxCollider2D zone)
    {
        if (visual == null || zone == null)
            return false;

        bool changed = false;
        Transform visualTransform = visual.transform;
        Vector2 size = zone.size;
        Vector3 desiredLocalPosition = GetVisualLocalPosition(visual, zone);
        if (!Approximately(visualTransform.localPosition, desiredLocalPosition))
        {
            visualTransform.localPosition = desiredLocalPosition;
            changed = true;
        }

        if (!Approximately(visualTransform.localRotation, Quaternion.identity))
        {
            visualTransform.localRotation = Quaternion.identity;
            changed = true;
        }

        if (!Approximately(visualTransform.localScale, Vector3.one))
        {
            visualTransform.localScale = Vector3.one;
            changed = true;
        }

        if (visual.drawMode != SpriteDrawMode.Sliced)
        {
            visual.drawMode = SpriteDrawMode.Sliced;
            changed = true;
        }

        if (!Approximately(visual.size, size))
        {
            visual.size = size;
            changed = true;
        }

        return changed;
    }

    private static Vector3 GetVisualLocalPosition(SpriteRenderer visual, BoxCollider2D zone)
    {
        Vector2 pivotCorrection = Vector2.zero;
        Sprite sprite = visual.sprite;
        if (sprite != null && sprite.rect.width > 0f && sprite.rect.height > 0f)
        {
            Vector2 normalizedPivot = new(
                sprite.pivot.x / sprite.rect.width,
                sprite.pivot.y / sprite.rect.height);
            pivotCorrection = new Vector2(
                (normalizedPivot.x - 0.5f) * zone.size.x,
                (normalizedPivot.y - 0.5f) * zone.size.y);
        }

        Vector2 center = zone.offset + pivotCorrection;
        return new Vector3(center.x, center.y, 0f);
    }

    private static bool PositionContentRoot(Transform contentRoot, BoxCollider2D zone)
    {
        if (contentRoot == null || zone == null || contentRoot == zone.transform)
            return false;

        if (contentRoot.IsChildOf(zone.transform))
        {
            Vector3 localPosition = contentRoot.localPosition;
            Vector3 centeredPosition = new Vector3(0f, 0f, localPosition.z);
            if (Approximately(localPosition, centeredPosition))
                return false;

            contentRoot.localPosition = centeredPosition;
            return true;
        }

        Vector3 worldPosition = zone.transform.position;
        worldPosition.z = contentRoot.position.z;
        if (Approximately(contentRoot.position, worldPosition))
            return false;

        contentRoot.position = worldPosition;
        return true;
    }

    private bool ConfigurePlayerBoundary(Vector2 arenaSize, Vector2 centerInArena)
    {
        if (playerTopBoundary == null)
            return false;

        const float thickness = 0.2f;
        Transform boundaryTransform = playerTopBoundary.transform;
        Vector3 worldCenter = arenaBounds.transform.TransformPoint(centerInArena);
        bool changed = SetWorldPose(boundaryTransform, worldCenter, arenaBounds.transform.rotation);
        Vector2 boundarySize = ConvertArenaSizeToZoneSize(
            boundaryTransform,
            new Vector2(arenaSize.x, thickness));

        changed |= SetCollider(playerTopBoundary, boundarySize, false);
        return changed;
    }

    private bool LayoutInputsChanged()
    {
        if (!HasRequiredReferences())
            return hasInputSnapshot;

        return !hasInputSnapshot ||
               !Approximately(lastArenaSize, arenaBounds.size) ||
               !Approximately(lastArenaOffset, arenaBounds.offset) ||
               !Approximately(lastArenaLocalToWorld, arenaBounds.transform.localToWorldMatrix) ||
               !Mathf.Approximately(lastBallsRatio, ballsRatio) ||
               !Mathf.Approximately(lastAnomalyRatio, anomalyRatio) ||
               !Mathf.Approximately(lastTargetsRatio, targetsRatio);
    }

    private void CacheLayoutInputs()
    {
        lastArenaSize = arenaBounds.size;
        lastArenaOffset = arenaBounds.offset;
        lastArenaLocalToWorld = arenaBounds.transform.localToWorldMatrix;
        lastBallsRatio = ballsRatio;
        lastAnomalyRatio = anomalyRatio;
        lastTargetsRatio = targetsRatio;
        hasInputSnapshot = true;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos)
            return;

        DrawZoneGizmo(ballsZone);
        DrawZoneGizmo(anomalyZone);
        DrawZoneGizmo(targetsZone);
    }

    private static void DrawZoneGizmo(BoxCollider2D zone)
    {
        if (zone == null)
            return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = zone.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(zone.offset, zone.size);
        Gizmos.matrix = previousMatrix;
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return (a - b).sqrMagnitude <= ChangeEpsilon * ChangeEpsilon;
    }

    private static bool Approximately(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude <= ChangeEpsilon * ChangeEpsilon;
    }

    private static bool Approximately(Quaternion a, Quaternion b)
    {
        return Mathf.Abs(Quaternion.Dot(a, b)) >= 1f - ChangeEpsilon;
    }

    private static bool Approximately(Matrix4x4 a, Matrix4x4 b)
    {
        for (int i = 0; i < 16; i++)
        {
            if (Mathf.Abs(a[i] - b[i]) > ChangeEpsilon)
                return false;
        }

        return true;
    }

#if UNITY_EDITOR
    private void RecordLayoutUndo()
    {
        Object[] objects =
        {
            ballsZone.transform, ballsZone,
            anomalyZone.transform, anomalyZone,
            targetsZone.transform, targetsZone,
            ballsVisual != null ? ballsVisual.transform : null, ballsVisual,
            anomalyVisual != null ? anomalyVisual.transform : null, anomalyVisual,
            targetsVisual != null ? targetsVisual.transform : null, targetsVisual,
            ballsContent,
            anomalyContent,
            targetsContent,
            playerTopBoundary != null ? playerTopBoundary.transform : null,
            playerTopBoundary
        };

        Undo.RecordObjects(RemoveNullObjects(objects), "Apply Football Arena Layout");
    }

    private static Object[] RemoveNullObjects(Object[] objects)
    {
        int validCount = 0;
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                validCount++;
        }

        Object[] result = new Object[validCount];
        int targetIndex = 0;
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                result[targetIndex++] = objects[i];
        }

        return result;
    }

    private void MarkLayoutDirty()
    {
        EditorUtility.SetDirty(ballsZone.transform);
        EditorUtility.SetDirty(ballsZone);
        EditorUtility.SetDirty(anomalyZone.transform);
        EditorUtility.SetDirty(anomalyZone);
        EditorUtility.SetDirty(targetsZone.transform);
        EditorUtility.SetDirty(targetsZone);

        MarkVisualDirty(ballsVisual);
        MarkVisualDirty(anomalyVisual);
        MarkVisualDirty(targetsVisual);

        if (gameObject.scene.IsValid() && gameObject.scene.isLoaded)
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private void MarkVisualsAndSceneDirty()
    {
        MarkVisualDirty(ballsVisual);
        MarkVisualDirty(anomalyVisual);
        MarkVisualDirty(targetsVisual);

        if (gameObject.scene.IsValid() && gameObject.scene.isLoaded)
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private static void MarkVisualDirty(SpriteRenderer visual)
    {
        if (visual == null)
            return;

        EditorUtility.SetDirty(visual.transform);
        EditorUtility.SetDirty(visual);
    }
#endif
}
