using UnityEngine;

public sealed class RunLevelManager : MonoBehaviour
{
    public static RunLevelManager Instance { get; private set; }

    public int CurrentLevel => RunStateManager.Instance != null
        ? RunStateManager.Instance.CurrentLevel
        : 1;

    private void Awake()
    {
        Instance = this;
    }

    public int GetNextLevelNumber()
    {
        return CurrentLevel + 1;
    }
}