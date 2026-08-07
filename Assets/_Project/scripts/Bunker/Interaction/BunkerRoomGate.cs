using UnityEngine;

public enum BunkerRoomGateMode
{
    Locked = 0,
    Open = 1,
    Sealed = 2
}

public sealed class BunkerRoomGate : MonoBehaviour, IBunkerInteractable, IBunkerHoverable
{
    [SerializeField] private BunkerRoomGateMode mode;
    [SerializeField] private BunkerStationId requiredStationId;
    [SerializeField, Range(1, 3)] private int requiredStationLevel = 2;
    [SerializeField] private Collider2D blockerCollider;
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Vector2 blockerSize = new(1.5f, 3.8f);
    [SerializeField] private Vector2 occluderOffset;
    [SerializeField] private Vector2 occluderSize = new(8.5f, 6.5f);
    [SerializeField] private string accessMessage = "НЕТ ДОСТУПА";

    private static Sprite massSprite;
    private Collider2D interactionCollider;

    public bool CanInteract => IsClosed;
    public string InteractionText => mode == BunkerRoomGateMode.Sealed
        ? accessMessage
        : $"{requiredStationId}: LEVEL {requiredStationLevel}";

    private bool IsClosed => mode == BunkerRoomGateMode.Sealed ||
        (mode == BunkerRoomGateMode.Locked &&
         BunkerStationProgressionService.GetStoredLevel(requiredStationId) < requiredStationLevel);

    private void Awake()
    {
        EnsureRuntimeParts();
        ApplyConfiguration();
        Refresh();
    }

    private void OnEnable()
    {
        if (BunkerStationProgressionService.Instance != null)
            BunkerStationProgressionService.Instance.StationLevelChanged += HandleStationLevelChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (BunkerStationProgressionService.Instance != null)
            BunkerStationProgressionService.Instance.StationLevelChanged -= HandleStationLevelChanged;
    }

    public void Configure(
        BunkerRoomGateMode gateMode,
        BunkerStationId stationId,
        int stationLevel,
        Vector2 blockerSize,
        Vector2 occluderOffset,
        Vector2 occluderSize,
        string closedMessage = null)
    {
        mode = gateMode;
        requiredStationId = stationId;
        requiredStationLevel = Mathf.Clamp(stationLevel, 1, 3);
        this.blockerSize = blockerSize;
        this.occluderOffset = occluderOffset;
        this.occluderSize = occluderSize;
        if (!string.IsNullOrWhiteSpace(closedMessage))
            accessMessage = closedMessage;
        EnsureRuntimeParts();
        ApplyConfiguration();
        Refresh();
    }

    private void ApplyConfiguration()
    {
        if (blockerCollider is BoxCollider2D box)
            box.size = blockerSize;

        Transform doorwayMass = visualRoot.transform.Find("DoorwayMass");
        doorwayMass.localScale = new Vector3(blockerSize.x + 0.35f, blockerSize.y + 0.35f, 1f);
        doorwayMass.GetComponent<SpriteRenderer>().color = mode == BunkerRoomGateMode.Sealed
            ? new Color(0.004f, 0.001f, 0.004f, 1f)
            : new Color(0.012f, 0.006f, 0.02f, 0.99f);

        Transform roomOccluder = visualRoot.transform.Find("RoomOccluder");
        roomOccluder.localPosition = occluderOffset;
        roomOccluder.localScale = new Vector3(occluderSize.x, occluderSize.y, 1f);
        roomOccluder.GetComponent<SpriteRenderer>().color = mode == BunkerRoomGateMode.Sealed
            ? new Color(0.002f, 0.001f, 0.003f, 1f)
            : new Color(0.005f, 0.004f, 0.009f, 0.985f);

        if (interactionCollider is BoxCollider2D interactionBox)
            interactionBox.size = blockerSize + Vector2.one * 0.5f;
    }

    public void Interact()
    {
        if (!IsClosed)
            return;

        ShowAccessMessage();
    }

    public void SetHovered(bool hovered)
    {
        if (hovered && IsClosed)
            ShowAccessMessage();
    }

    private void ShowAccessMessage()
    {
        if (!IsClosed)
            return;

        if (mode == BunkerRoomGateMode.Sealed)
        {
            BunkerContext.Instance?.Notifications?.ShowWarning(accessMessage);
            return;
        }

        BunkerContext.Instance?.Notifications?.ShowInfo(
            $"Требуется {requiredStationId}: уровень {requiredStationLevel}");
    }

    public void Refresh()
    {
        bool closed = IsClosed;
        if (visualRoot != null)
            visualRoot.SetActive(closed);
        if (blockerCollider != null)
            blockerCollider.enabled = closed;
        if (interactionCollider != null)
            interactionCollider.enabled = closed;
    }

    private void HandleStationLevelChanged(BunkerStationId stationId, int level)
    {
        if (mode == BunkerRoomGateMode.Locked && stationId == requiredStationId)
            Refresh();
    }

    private void EnsureRuntimeParts()
    {
        if (blockerCollider == null)
        {
            BoxCollider2D blocker = GetComponent<BoxCollider2D>();
            if (blocker == null)
                blocker = gameObject.AddComponent<BoxCollider2D>();
            blocker.isTrigger = false;
            blockerCollider = blocker;
        }

        if (visualRoot == null)
        {
            visualRoot = new GameObject("DarkAnomalyMass");
            visualRoot.transform.SetParent(transform, false);
            CreateMassRenderer("RoomOccluder", visualRoot.transform, new Color(0.005f, 0.004f, 0.009f, 0.985f), 118);
            CreateMassRenderer("DoorwayMass", visualRoot.transform, mode == BunkerRoomGateMode.Sealed
                ? new Color(0.018f, 0.004f, 0.012f, 1f)
                : new Color(0.01f, 0.006f, 0.018f, 1f), 120);
        }

        if (interactionCollider == null)
        {
            GameObject interaction = new("Interaction", typeof(BoxCollider2D));
            interaction.layer = 9;
            interaction.transform.SetParent(transform, false);
            BoxCollider2D trigger = interaction.GetComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            interactionCollider = trigger;
            interaction.AddComponent<BunkerInteractableCollider>();
        }
    }

    private static void CreateMassRenderer(string objectName, Transform parent, Color color, int sortingOrder)
    {
        GameObject mass = new(objectName, typeof(SpriteRenderer));
        mass.transform.SetParent(parent, false);
        SpriteRenderer renderer = mass.GetComponent<SpriteRenderer>();
        renderer.sprite = GetMassSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
    }

    private static Sprite GetMassSprite()
    {
        if (massSprite != null)
            return massSprite;

        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
        {
            name = "BunkerGate_RuntimePixel",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        massSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        massSprite.name = "BunkerGate_RuntimeSprite";
        return massSprite;
    }
}
