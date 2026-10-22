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
    public MonoBehaviour Source => source;
    // Presentation state only; this does not prove visibility in the player's rendered frame.
    public bool IsVisible => isActiveAndEnabled && visual != null && visual.enabled;
    public bool IsHovered { get; private set; }
    public bool CanInteractNow => isActiveAndEnabled && owner != null && !owner.IsBlocked &&
        source != null && source.isActiveAndEnabled && target != null && target.CanInteract;

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
        SetHovered(false);
        // LateUpdate applies presentation after all components finish OnEnable.
        // Toggling Light2D here can register it twice during scene/domain reload.
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
        // Presentation and pointer interaction share availability, independent of the player.
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
