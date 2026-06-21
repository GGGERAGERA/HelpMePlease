using UnityEngine;

public class HoverOutlineRaycast : MonoBehaviour
{
    [SerializeField] private GameObject outlineObject;
    private SpriteRenderer outlineRenderer;
    private Camera mainCam;

    private void Awake()
    {
        if (outlineObject == null)
            outlineObject = transform.Find("Lamp1Outline")?.gameObject;

        outlineRenderer = outlineObject?.GetComponent<SpriteRenderer>();
        mainCam = Camera.main;
        HideOutline();
    }

    private void Update()
    {
        if (mainCam == null) return;

        Vector2 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

        if (hit.collider != null && hit.transform == transform)
            ShowOutline();
        else
            HideOutline();
    }

    private void ShowOutline()
    {
        if (outlineObject == null) return;
        if (outlineRenderer != null) outlineRenderer.enabled = true;
    }

    private void HideOutline()
    {
        if (outlineObject == null) return;
        if (outlineRenderer != null) outlineRenderer.enabled = false;
    }
}