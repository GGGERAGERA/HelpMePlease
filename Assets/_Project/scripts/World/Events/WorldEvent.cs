using UnityEngine;

public abstract class WorldEvent : MonoBehaviour
{
    protected WorldEventSpawner owner;

    public bool IsCompleted { get; private set; }

    public virtual void Initialize(WorldEventSpawner spawner)
    {
        owner = spawner;
        IsCompleted = false;
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

        owner?.NotifyEventFailed(this);
        owner = null;
    }

    public virtual void ApplyDifficultyMultiplier(float multiplier)
    {
    }
}
