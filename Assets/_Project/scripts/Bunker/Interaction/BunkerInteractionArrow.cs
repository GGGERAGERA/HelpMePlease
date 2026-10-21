using UnityEngine;

[RequireComponent(typeof(BoxCollider2D), typeof(BunkerInteractableCollider))]
[ExecuteAlways]
public sealed class BunkerInteractionArrow : MonoBehaviour, IBunkerHoverable
{
    [SerializeField] private BunkerInteractionIndicators owner;
    [SerializeField] private MonoBehaviour source;
    private IBunkerInteractable target;
    private IBunkerHoverable sourceHover;
    private SpriteRenderer visual;
    private BoxCollider2D hitbox;
    private UnityEngine.Rendering.Universal.Light2D[] lights;
    public SpriteRenderer Visual => visual;
    public Vector2 ClickPosition => hitbox.bounds.center;
    private Collider2D[] interactionAreas;
    public MonoBehaviour Source => source;
    public bool IsVisible => isActiveAndEnabled && visual != null && visual.enabled;
    public bool IsHovered { get; private set; }
    public bool CanInteractNow
    {
        get
        {
            var player = PlayerRuntimeReference.PlayerTransform;
            return isActiveAndEnabled && owner != null && !owner.IsBlocked && source != null && source.isActiveAndEnabled && target != null &&
                target.CanInteract && player != null &&
                DistanceTo(player.position) <= owner.InteractionDistance;
        }
    }

    public void Bind(BunkerInteractionIndicators indicators, MonoBehaviour behaviour)
    {
        owner = indicators;
        source = behaviour;
        target = (IBunkerInteractable)source;
        sourceHover = source.GetComponent<IBunkerHoverable>();
        // The original prefab owns its appearance and Animator. This adapter only
        // gates visibility and routes the existing collider interaction contract.
        visual = GetComponentInChildren<SpriteRenderer>(true);
        lights = GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true);
        hitbox = GetComponent<BoxCollider2D>();
        hitbox.isTrigger = true;
        gameObject.layer = LayerMask.NameToLayer("BunkerInteractable");
        GetComponent<BunkerInteractableCollider>().ConfigureSource(source.gameObject);
        var proxies = source.GetComponentsInChildren<BunkerInteractableCollider>(true);
        var areas = new System.Collections.Generic.List<Collider2D>(proxies.Length);
        foreach(var proxy in proxies)
        {
            var area = proxy.GetComponent<Collider2D>();
            if(area != null && proxy.GetComponent<BunkerInteractionArrow>() == null) areas.Add(area);
        }
        if(areas.Count == 0 && source.TryGetComponent<Collider2D>(out var ownArea)) areas.Add(ownArea);
        interactionAreas = areas.ToArray();
        SetHovered(false);
        RefreshPresentation();
    }

    public float DistanceTo(Vector2 point)
    {
        float squared = float.PositiveInfinity;
        if(interactionAreas != null)
            foreach(var area in interactionAreas)
                if(area != null && area.enabled && area.gameObject.activeInHierarchy)
                    squared = Mathf.Min(squared,(area.ClosestPoint(point)-point).sqrMagnitude);
        return float.IsPositiveInfinity(squared) ? Vector2.Distance(point,source.transform.position) : Mathf.Sqrt(squared);
    }

    private void OnEnable()
    {
        if(owner != null && source != null) Bind(owner,source);
    }

    private void LateUpdate()
    {
        RefreshPresentation();
    }

    private void RefreshPresentation()
    {
        if(visual == null || hitbox == null) return;
        // Saved scene instances are visible for layout authoring. Gameplay still uses
        // live distance and modal eligibility, without relying on editor preview state.
        bool visible = Application.IsPlaying(gameObject) ? CanInteractNow :
            source != null && source.isActiveAndEnabled;
        // Follow Visual's ORIGINAL animation without moving or scaling its hierarchy.
        hitbox.offset = transform.InverseTransformPoint(visual.bounds.center);
        hitbox.size = (Vector2)visual.sprite.bounds.size * 1.4f * 1.5f;
        if (!visible && IsHovered) SetHovered(false);
        visual.enabled = visible;
        hitbox.enabled = visible;
        foreach(var light in lights) light.enabled = visible;
    }

    public void SetHovered(bool hovered)
    {
        IsHovered = hovered;
        if(Application.IsPlaying(gameObject)) sourceHover?.SetHovered(hovered);
    }

    private void OnDisable() => SetHovered(false);
}
