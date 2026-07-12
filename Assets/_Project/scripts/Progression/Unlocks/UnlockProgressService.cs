using UnityEngine;

public sealed class UnlockProgressService : MonoBehaviour
{
    public static UnlockProgressService Instance { get; private set; }
    
    [SerializeField] private UnlockRegistry registry;

    private const string UnlockKeyPrefix = "Unlock_";
    private const string ProgressKeyPrefix = "UnlockProgress_";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool IsUnlocked(UnlockableContentData content)
    {
        if (content == null)
            return true;

        if (content.unlockedByDefault)
            return true;

        return PlayerPrefs.GetInt(GetUnlockKey(content.id), 0) == 1;
    }

    public int GetProgress(UnlockableContentData content)
    {
        if (content == null || content.condition == null)
            return 0;

        return PlayerPrefs.GetInt(GetProgressKey(content.id), 0);
    }

    public void AddProgress(UnlockableContentData content, int amount = 1)
    {
        if (content == null)
            return;

        if (IsUnlocked(content))
            return;

        if (content.condition == null)
            return;

        int current = GetProgress(content);
        int next = Mathf.Max(0, current + amount);

        PlayerPrefs.SetInt(GetProgressKey(content.id), next);

        if (next >= content.condition.requiredAmount)
            Unlock(content);

        PlayerPrefs.Save();
    }

    public void Unlock(UnlockableContentData content)
    {
        if (content == null)
            return;

        if (string.IsNullOrWhiteSpace(content.id))
        {
            Debug.LogWarning("[UnlockProgressService] Content has empty id.");
            return;
        }

        PlayerPrefs.SetInt(GetUnlockKey(content.id), 1);
        PlayerPrefs.Save();

        Debug.Log($"[UnlockProgressService] Unlocked: {content.displayName} ({content.id})");
    }

    public void ResetUnlock(UnlockableContentData content)
    {
        if (content == null)
            return;

        PlayerPrefs.DeleteKey(GetUnlockKey(content.id));
        PlayerPrefs.DeleteKey(GetProgressKey(content.id));
        PlayerPrefs.Save();
    }

    private string GetUnlockKey(string id)
    {
        return UnlockKeyPrefix + id;
    }

    private string GetProgressKey(string id)
    {
        return ProgressKeyPrefix + id;
    }

    public void AddProgressByCondition(
    UnlockConditionType conditionType,
    string targetId,
    int amount = 1
)
    {
        if (registry == null)
        {
            Debug.LogWarning("[UnlockProgressService] Registry is missing.");
            return;
        }

        foreach (UnlockableContentData content in registry.Contents)
        {
            if (content == null || content.condition == null)
                continue;

            if (IsUnlocked(content))
                continue;

            if (content.condition.type != conditionType)
                continue;

            if (content.condition.targetId != targetId)
                continue;

            AddProgress(content, amount);
        }
    }
#if UNITY_EDITOR
    public void DebugAddKilledTupiks(int amount)
    {
        AddProgressByCondition(
            UnlockConditionType.KillEnemyType,
            "Tupik",
            amount
        );
    }

    public void DebugCompleteDarkLevel()
    {
        AddProgressByCondition(
            UnlockConditionType.CompleteLevelModifier,
            "Darkness",
            1
        );
    }

    public void DebugCompleteRainLevel()
    {
        AddProgressByCondition(
            UnlockConditionType.CompleteLevelModifier,
            "Rain",
            1
        );
    }

    public void DebugUnlockAll()
    {
        if (registry == null)
        {
            Debug.LogWarning("[UnlockProgressService] Registry is missing.");
            return;
        }

        foreach (UnlockableContentData content in registry.Contents)
        {
            if (content == null)
                continue;

            Unlock(content);
        }

        Debug.Log("[UnlockProgressService] DEBUG unlocked all content.");
    }

    public void DebugResetAll()
    {
        if (registry == null)
        {
            Debug.LogWarning("[UnlockProgressService] Registry is missing.");
            return;
        }

        foreach (UnlockableContentData content in registry.Contents)
        {
            if (content == null)
                continue;

            ResetUnlock(content);
        }

        Debug.Log("[UnlockProgressService] DEBUG reset all unlock progress.");
    }
#endif
}