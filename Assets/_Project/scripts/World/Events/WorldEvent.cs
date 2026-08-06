using UnityEngine;

public abstract class WorldEvent : Interactable
{
    [Header("Presentation")]
    [SerializeField] private string eventDisplayName = "WORLD EVENT";
    [SerializeField, TextArea(1, 2)] private string eventDescription;

    protected WorldEventSpawner owner;

    private bool cleanupPerformed;
    private bool eventMarkerVisible;
    public bool IsCompleted { get; private set; }
    public bool IsFailed { get; private set; }
    public bool IsStarted { get; private set; }
    public string EventDisplayName => eventDisplayName;
    public string EventDescription => eventDescription;
    public virtual Vector3 RewardPosition => transform.position;
    public override bool CanInteract
    {
        get
        {
            if (IsCompleted || IsStarted ||
                owner == null || !owner.CanStartEvent(this))
            {
                return false;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player != null && CanStartFrom(player.transform.position);
        }
    }

    public virtual void Initialize(WorldEventSpawner spawner)
    {
        owner = spawner;
        IsCompleted = false;
        IsFailed = false;
        IsStarted = false;
        cleanupPerformed = false;
        eventMarkerVisible = false;
    }

    public sealed override void Interact()
    {
        if (!CanInteract)
            return;

        owner.TryChooseAndStartEvent(this);
    }

    public void StartSelectedEvent(bool riskMode = false)
    {
        if (IsStarted || owner == null ||
            !owner.TryStartEvent(this))
        {
            return;
        }

        IsStarted = true;
        owner.NotifyEventStarted(this, riskMode);
        OnEventStarted();
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

        hud.ShowWorldEventMarker(target, label);
        eventMarkerVisible = true;
    }

    protected void HideEventMarker()
    {
        if (!eventMarkerVisible)
            return;

        HUDManager.Instance?.HideWorldEventMarker();
        eventMarkerVisible = false;
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

        Destroy(gameObject);
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal void ClearForDebug()
    {
        if (IsCompleted)
            return;

        // Mark the transient instance as terminal before Destroy so OnDestroy
        // cannot route a debug cleanup through the regular failure lifecycle.
        // IsFailed also keeps event-specific cleanup from playing completion
        // presentation; no failure notification is sent to the owner.
        IsCompleted = true;
        IsFailed = true;
        HideEventMarker();
        CleanupOnce();
        owner = null;
        Destroy(gameObject);
    }
#endif

    private void OnDestroy()
    {
        if (!IsCompleted)
            FailEvent();
        else
            CleanupOnce();

        HideEventMarker();
    }

    public virtual void ApplyDifficultyMultiplier(float multiplier)
    {
    }
}
