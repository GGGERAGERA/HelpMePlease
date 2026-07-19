using UnityEngine;

public sealed class BunkerHoverOutline : MonoBehaviour, IBunkerHoverable
{
    [SerializeField] private bool autoFindRenderers = true;
    [SerializeField] private SpriteRenderer[] targetRenderers;

    private Material[] materials;

    private void Awake()
    {
        if (autoFindRenderers)
            targetRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        materials = new Material[targetRenderers.Length];

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] == null)
                continue;

            materials[i] = targetRenderers[i].material;
        }

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
        }
    }

    private void OnDestroy()
    {
        if (materials == null)
            return;

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null)
                Destroy(materials[i]);
        }
    }
}
