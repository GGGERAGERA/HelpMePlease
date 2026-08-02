using UnityEngine;

public abstract class WorldEvent : Interactable
{
    protected WorldEventSpawner owner;

    private bool cleanupPerformed;
    private bool eventMarkerVisible;
    private bool selectedRiskMode;

    public bool IsCompleted { get; private set; }
    public bool IsFailed { get; private set; }
    public bool IsStarted { get; private set; }
    public bool IsStarting { get; private set; }
    public virtual Vector3 RewardPosition => transform.position;
    protected virtual string StartDisplayName => string.Empty;
    protected virtual Color StartAccentColor => Color.white;
    public override bool CanInteract
    {
        get
        {
            if (IsCompleted || IsStarted || IsStarting ||
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
        IsStarting = false;
        selectedRiskMode = false;
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
        if (IsStarted || IsStarting || owner == null ||
            !owner.TryStartEvent(this))
        {
            return;
        }

        selectedRiskMode = riskMode;
        IsStarting = true;

        RunMessageService presentation = RunMessageService.Instance;

        if (presentation == null ||
            string.IsNullOrWhiteSpace(StartDisplayName) ||
            !presentation.ShowWorldEventStart(
                this,
                StartDisplayName,
                StartAccentColor,
                FinishStartPresentation))
        {
            FinishStartPresentation();
        }
    }

    private void FinishStartPresentation()
    {
        if (!IsStarting || IsCompleted)
            return;

        IsStarting = false;
        IsStarted = true;
        owner?.NotifyEventStarted(this, selectedRiskMode);
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

    private void OnDestroy()
    {
        RunMessageService.Instance?.CancelWorldEventStart(this);

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
