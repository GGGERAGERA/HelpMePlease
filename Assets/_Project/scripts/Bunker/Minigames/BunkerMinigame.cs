using UnityEngine;

public enum BunkerMinigameState
{
    Idle,
    Running,
    Success,
    Failed
}

public abstract class BunkerMinigame : MonoBehaviour
{
    public BunkerMinigameState State { get; private set; } =
        BunkerMinigameState.Idle;

    public bool CanStart => isActiveAndEnabled && State == BunkerMinigameState.Idle;
    public bool IsRunning => State == BunkerMinigameState.Running;

    public void StartGame()
    {
        if (!CanStart)
            return;

        Debug.Log($"[Minigame] Start: {GetType().Name}");
        State = BunkerMinigameState.Running;
        OnGameStarted();
    }

    public void CompleteGame()
    {
        if (!IsRunning)
            return;

        State = BunkerMinigameState.Success;
        OnGameCompleted();
    }

    public void FailGame()
    {
        if (!IsRunning)
            return;

        State = BunkerMinigameState.Failed;
        OnGameFailed();
    }

    public void ResetGame()
    {
        State = BunkerMinigameState.Idle;
        OnGameReset();
    }

    protected void AllowRestart()
    {
        State = BunkerMinigameState.Idle;
    }

    protected virtual void OnGameStarted() { }
    protected virtual void OnGameCompleted() { }
    protected virtual void OnGameFailed() { }
    protected virtual void OnGameReset() { }
}
