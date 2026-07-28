using UnityEngine;

public abstract class WorldEvent : Interactable
{
    protected WorldEventSpawner owner;

    public bool IsCompleted { get; private set; }
    public bool IsStarted { get; private set; }
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
        IsStarted = false;
    }

    public sealed override void Interact()
    {
        if (!CanInteract || !owner.TryStartEvent(this))
            return;

        IsStarted = true;
        OnEventStarted();
    }

    protected virtual bool CanStartFrom(Vector2 playerPosition)
    {
        return true;
    }

    protected virtual void OnEventStarted()
    {
    }

    protected void CompleteEvent()
    {
        if (IsCompleted)
            return;

        IsCompleted = true;

        owner?.NotifyEventCompleted(this);

        Destroy(gameObject);
    }

    protected void FailEvent()
    {
        if (IsCompleted)
            return;

        IsCompleted = true;
        owner?.NotifyEventFailed(this);
        owner = null;
    }

    public virtual void ApplyDifficultyMultiplier(float multiplier)
    {
    }
}
