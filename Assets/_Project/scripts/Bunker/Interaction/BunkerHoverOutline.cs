using UnityEngine;

public sealed class BunkerHoverOutline : MonoBehaviour, IBunkerHoverable
{
    [SerializeField] private bool autoFindRenderers = true;
    [SerializeField] private SpriteRenderer[] targetRenderers;
    [SerializeField] private Material outlineMaterialFallback;
    [SerializeField] private bool useBoundsOutlineFallback;
    [SerializeField, Min(0f)] private float boundsOutlinePadding = 0.08f;
    [SerializeField, Min(0.01f)] private float boundsOutlineWidth = 0.06f;
    [SerializeField] private Color boundsOutlineColor = Color.white;
    [SerializeField] private Collider2D boundsOutlineCollider;

    private Material[] materials;
    private LineRenderer boundsOutlineRenderer;
    private Material boundsOutlineMaterial;

    private void Awake()
    {
        if (autoFindRenderers)
            targetRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        materials = new Material[targetRenderers.Length];

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] == null)
                continue;

            Material sharedMaterial = targetRenderers[i].sharedMaterial;
            if (outlineMaterialFallback != null &&
                !useBoundsOutlineFallback &&
                !SupportsOutline(sharedMaterial))
            {
                materials[i] = new Material(outlineMaterialFallback);
                targetRenderers[i].sharedMaterial = materials[i];
            }
            else
            {
                materials[i] = targetRenderers[i].material;
            }
        }

        if (useBoundsOutlineFallback)
            CreateBoundsOutlineRenderer();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[BunkerHoverOutline] {name} " +
            $"renderers={targetRenderers.Length}"
        );
#endif

        SetHovered(false);
    }

    public void SetHovered(bool hovered)
    {
        if (materials == null)
            return;

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];

            if (material == null)
                continue;

            if (hovered)
                material.EnableKeyword("_OUTLINE_ON");
            else
                material.DisableKeyword("_OUTLINE_ON");

            if (material.HasProperty("_EnableOutline"))
                material.SetFloat("_EnableOutline", hovered ? 1f : 0f);
        }

        if (boundsOutlineRenderer != null)
            boundsOutlineRenderer.enabled = hovered;
    }

    private void CreateBoundsOutlineRenderer()
    {
        if (outlineMaterialFallback == null)
        {
            Debug.LogError($"[BunkerHoverOutline] {name} requires an outline material fallback.", this);
            return;
        }

        bool hasBounds = boundsOutlineCollider != null;
        Bounds bounds = hasBounds ? boundsOutlineCollider.bounds : default;
        int highestSortingLayerId = 0;
        int highestSortingLayerValue = int.MinValue;
        int highestSortingOrder = int.MinValue;
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            SpriteRenderer source = targetRenderers[i];
            if (source == null || source.sprite == null)
                continue;

            if (boundsOutlineCollider == null && !hasBounds)
            {
                bounds = source.bounds;
                hasBounds = true;
            }
            else if (boundsOutlineCollider == null)
                bounds.Encapsulate(source.bounds);

            int layerValue = SortingLayer.GetLayerValueFromID(source.sortingLayerID);
            if (layerValue > highestSortingLayerValue)
            {
                highestSortingLayerValue = layerValue;
                highestSortingLayerId = source.sortingLayerID;
                highestSortingOrder = source.sortingOrder;
            }
            else if (layerValue == highestSortingLayerValue)
                highestSortingOrder = Mathf.Max(highestSortingOrder, source.sortingOrder);
        }

        if (!hasBounds)
            return;

        var outlineObject = new GameObject("Bounds HoverOutline");
        outlineObject.layer = gameObject.layer;
        outlineObject.transform.SetParent(transform, false);
        boundsOutlineRenderer = outlineObject.AddComponent<LineRenderer>();
        boundsOutlineRenderer.useWorldSpace = true;
        boundsOutlineRenderer.loop = false;
        boundsOutlineRenderer.positionCount = 5;
        boundsOutlineRenderer.widthMultiplier = boundsOutlineWidth;
        boundsOutlineRenderer.startColor = boundsOutlineColor;
        boundsOutlineRenderer.endColor = boundsOutlineColor;
        boundsOutlineRenderer.numCornerVertices = 2;
        boundsOutlineRenderer.numCapVertices = 2;
        boundsOutlineRenderer.sortingLayerID = highestSortingLayerId;
        boundsOutlineRenderer.sortingOrder = Mathf.Min(highestSortingOrder + 1, short.MaxValue);

        float minX = bounds.min.x - boundsOutlinePadding;
        float minY = bounds.min.y - boundsOutlinePadding;
        float maxX = bounds.max.x + boundsOutlinePadding;
        float maxY = bounds.max.y + boundsOutlinePadding;
        float z = bounds.min.z - 0.1f;
        boundsOutlineRenderer.SetPositions(new[]
        {
            new Vector3(minX, minY, z),
            new Vector3(minX, maxY, z),
            new Vector3(maxX, maxY, z),
            new Vector3(maxX, minY, z),
            new Vector3(minX, minY, z)
        });

        boundsOutlineMaterial = new Material(outlineMaterialFallback);
        boundsOutlineMaterial.DisableKeyword("_OUTLINE_ON");
        if (boundsOutlineMaterial.HasProperty("_EnableOutline"))
            boundsOutlineMaterial.SetFloat("_EnableOutline", 0f);
        if (boundsOutlineMaterial.HasProperty("_Brightness"))
            boundsOutlineMaterial.SetFloat("_Brightness", 1f);
        if (boundsOutlineMaterial.HasProperty("_Color"))
            boundsOutlineMaterial.SetColor("_Color", boundsOutlineColor);
        boundsOutlineRenderer.sharedMaterial = boundsOutlineMaterial;
        boundsOutlineRenderer.enabled = false;
    }

    private static bool SupportsOutline(Material material)
    {
        return material != null &&
               material.HasProperty("_EnableOutline");
    }

    private void OnDestroy()
    {
        if (materials != null)
        {
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null)
                    Destroy(materials[i]);
            }
        }

        if (boundsOutlineMaterial != null)
            Destroy(boundsOutlineMaterial);
        if (boundsOutlineRenderer != null)
            Destroy(boundsOutlineRenderer.gameObject);
    }
}
