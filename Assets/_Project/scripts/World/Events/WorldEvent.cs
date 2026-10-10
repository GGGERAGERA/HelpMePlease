using UnityEngine;

public abstract class WorldEvent : Interactable, ITacticalMapMarkerProvider
{
    [SerializeField] private string eventId;
    [SerializeField] private string eventTag;
    public string EventId => eventId;
    public string EventTag => eventTag;
    public WorldEvent SourcePrefab { get; private set; }
    public void BindSource(WorldEvent prefab) => SourcePrefab = prefab;

    [Header("Presentation")]
    [SerializeField] private string eventDisplayName = "WORLD EVENT";
    [SerializeField, TextArea(1, 2)] private string eventDescription;

    [Header("Orchestration")]
    [SerializeField] private bool availableInProduction = true;
    [SerializeField] private bool allowedInSite;
    [SerializeField] private bool requiresHoldPointFeature;

    protected WorldEventSpawner owner;

    private bool cleanupPerformed;
    private bool debugCleanup;
    private HUDManager eventMarkerHud;
    private bool hasSitePlacementBounds;
    private Vector2 sitePlacementCenter;
    private Vector2 sitePlacementSize;
    public bool IsCompleted { get; private set; }
    public bool IsFailed { get; private set; }
    public bool IsStarted { get; private set; }
    public string EventDisplayName => eventDisplayName;
    public string EventDescription => eventDescription;
    public bool AllowedInSite => allowedInSite;
    public bool AvailableInProduction => availableInProduction;
    public bool RequiresHoldPointFeature => requiresHoldPointFeature;
    public virtual bool UsesStandardSpawnPressure => true;
    public virtual WorldEventRewardResult? CompletionReward => null;
    public virtual SiteEnvironmentCompletionPolicy EnvironmentCompletionPolicy => SiteEnvironmentCompletionPolicy.CollapseWithObjective;
    public virtual bool TryPreparePlacement(WorldEventPlacementContext context, out string error) { error = null; return true; }
    public virtual void CollectReservedFootprints(System.Collections.Generic.List<Rect> footprints) { }
    public virtual bool TryValidateConfiguration(out string error) { error = null; return true; }
    public virtual void Cancel() => FailEvent();
    public virtual Vector3 RewardPosition => transform.position;
    protected bool IsDebugCleanup => debugCleanup;
    public override bool CanInteract
    {
        get
        {
            if (IsCompleted || IsStarted ||
                owner == null || !owner.CanStartEvent(this))
            {
                return false;
            }

            Transform player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
            return player != null && CanStartFrom(player.position);
        }
    }

    public virtual void Initialize(WorldEventSpawner spawner)
    {
        owner = spawner;
        IsCompleted = false;
        IsFailed = false;
        IsStarted = false;
        cleanupPerformed = false;
        debugCleanup = false;
        HideEventMarker();
    }

    public void ConfigureSitePlacement(Vector2 center, Vector2 size)
    {
        sitePlacementCenter = center;
        sitePlacementSize = new Vector2(
            Mathf.Max(0f, size.x),
            Mathf.Max(0f, size.y)
        );
        hasSitePlacementBounds = sitePlacementSize.x > 0f &&
            sitePlacementSize.y > 0f;
    }

    protected bool HasSitePlacementBounds => hasSitePlacementBounds;
    internal Rect? SiteStartBounds => hasSitePlacementBounds ? new Rect(sitePlacementCenter - sitePlacementSize * .5f, sitePlacementSize) : null;

    protected bool IsInsideSitePlacement(
        Vector2 position,
        float padding = 0f)
    {
        if (!hasSitePlacementBounds)
            return true;

        Vector2 half = sitePlacementSize * 0.5f -
            Vector2.one * Mathf.Max(0f, padding);
        Vector2 offset = position - sitePlacementCenter;
        return half.x >= 0f && half.y >= 0f &&
            Mathf.Abs(offset.x) <= half.x &&
            Mathf.Abs(offset.y) <= half.y;
    }

    public sealed override void Interact()
    {
        if (!CanInteract)
            return;

        owner.TryStartProductionEvent(this);
    }

    public void StartEvent()
    {
        if (IsStarted || owner == null ||
            !owner.TryStartEvent(this))
        {
            return;
        }

        IsStarted = true;
        owner.NotifyEventStarted(this);
        OnEventStarted();

        if (!IsCompleted)
        {
            RunMessageService.Instance?.ShowCustom(
                EventDisplayName,
                EventDescription
            );
        }
    }

    protected virtual bool CanStartFrom(Vector2 playerPosition)
    {
        return true;
    }

    protected virtual void OnEventStarted()
    {
    }

    protected void ShowEventMarker(Transform target, string label)
    {
        if (target == null)
            return;

        HUDManager hud = HUDManager.Instance;

        if (hud == null)
            return;

        if (eventMarkerHud != hud) HideEventMarker();
        hud.ShowWorldEventMarker(this, target, label);
        eventMarkerHud = hud;
    }

    protected void HideEventMarker()
    {
        if (eventMarkerHud != null) eventMarkerHud.HideWorldEventMarker(this);
        eventMarkerHud = null;
    }

    protected void CompleteEvent()
    {
        if (IsCompleted)
            return;

        IsCompleted = true;
        HideEventMarker();
        CleanupOnce();

        WorldEventSpawner eventOwner = owner;
        owner = null;
        eventOwner?.NotifyEventCompleted(this);
    }

    protected void FailEvent()
    {
        if (IsCompleted)
            return;

        IsCompleted = true;
        IsFailed = true;
        HideEventMarker();
        CleanupOnce();

        WorldEventSpawner eventOwner = owner;
        owner = null;
        eventOwner?.NotifyEventFailed(this);
    }

    protected virtual void CleanupEvent()
    {
    }

    private void CleanupOnce()
    {
        if (cleanupPerformed)
            return;

        cleanupPerformed = true;
        CleanupEvent();
    }


    internal void DisposeForOwnerReset()
    {
        if (IsCompleted) return;
        IsCompleted = true; IsFailed = true; debugCleanup = true;
        HideEventMarker(); CleanupOnce();
        WorldEventSpawner eventOwner = owner; owner = null;
        eventOwner?.ReleaseEventWithoutResult(this);
    }

    private void OnDestroy()
    {
        if (!IsCompleted)
            FailEvent();
        else
            CleanupOnce();

        HideEventMarker();
    }

    public virtual void CollectTacticalMapMarkers(
        System.Collections.Generic.List<TacticalMapMarkerDescriptor> markers)
    {
        if (markers == null || IsCompleted)
            return;

        markers.Add(new TacticalMapMarkerDescriptor(
            IsStarted
                ? TacticalMapMarkerKind.Objective
                : TacticalMapMarkerKind.Event,
            transform.position
        ));
    }
}
