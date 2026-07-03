using UnityEngine;

public sealed class BunkerHoverOutline : MonoBehaviour, IBunkerHoverable
{
    [SerializeField] private SpriteRenderer[] targetRenderers;
    [SerializeField] private bool autoFindRenderers = true;

    private Material[] materials;

    private void Awake()
    {
        if (autoFindRenderers && (targetRenderers == null || targetRenderers.Length == 0))
            targetRenderers = GetComponentsInChildren<SpriteRenderer>();

        materials = new Material[targetRenderers.Length];

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] == null)
                continue;

            materials[i] = targetRenderers[i].material;
        }

        SetHovered(false);
    }

    public void SetHovered(bool hovered)
    {
        if (materials == null)
            return;

        foreach (Material material in materials)
        {
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

        foreach (Material material in materials)
        {
            if (material != null)
                Destroy(material);
        }
    }
}