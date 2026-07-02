using UnityEngine;

public class HoverOutlineRaycast : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private bool autoFindRenderers = true;
    [SerializeField] private SpriteRenderer[] targetRenderers;
    
    [Header("Raycast Settings")]
    [SerializeField] private LayerMask raycastLayerMask = ~0;

    private Camera mainCam;
    private Material[] outlineMaterials;
    private bool isHovered;
    private Collider2D cachedCollider; // Кешируем коллайдер, чтобы не искать каждый кадр

    private void Awake()
    {
        mainCam = Camera.main;
        cachedCollider = GetComponent<Collider2D>();

        if (autoFindRenderers && (targetRenderers == null || targetRenderers.Length == 0))
            targetRenderers = GetComponentsInChildren<SpriteRenderer>();

        if (targetRenderers != null && targetRenderers.Length > 0)
        {
            outlineMaterials = new Material[targetRenderers.Length];
            for (int i = 0; i < targetRenderers.Length; i++)
                if (targetRenderers[i] != null) outlineMaterials[i] = targetRenderers[i].material;
        }

        HideOutline();
    }

    private void Update()
    {
        if (mainCam == null || outlineMaterials == null || outlineMaterials.Length == 0 || cachedCollider == null)
            return;

        Vector2 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        
        // ЗАМЕНА RaycastAll на OverlapPoint. Это работает в разы быстрее и НЕ СОЗДАЁТ МУСОРА (аллокаций)
        bool isHitOnThisObject = cachedCollider.OverlapPoint(mousePos);

        if (isHitOnThisObject != isHovered)
        {
            isHovered = isHitOnThisObject;
            if (isHovered) ShowOutline();
            else HideOutline();
        }
    }

    private void ShowOutline()
    {
        for (int i = 0; i < outlineMaterials.Length; i++)
            if (outlineMaterials[i] != null) outlineMaterials[i].EnableKeyword("_OUTLINE_ON");
    }

    private void HideOutline()
    {
        for (int i = 0; i < outlineMaterials.Length; i++)
            if (outlineMaterials[i] != null) outlineMaterials[i].DisableKeyword("_OUTLINE_ON");
    }

    public void SetOutline(bool enabled)
    {
        if (enabled) ShowOutline();
        else HideOutline();
    }

    private void OnDestroy()
    {
        if (outlineMaterials != null)
        {
            for (int i = 0; i < outlineMaterials.Length; i++)
                if (outlineMaterials[i] != null) Destroy(outlineMaterials[i]);
        }
    }
}