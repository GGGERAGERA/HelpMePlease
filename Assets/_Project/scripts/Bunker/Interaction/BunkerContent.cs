using UnityEngine;

public sealed class BunkerContent : MonoBehaviour
{
    private const string KeyPrefix = "BunkerShop_";

    [Header("Data")]
    [SerializeField] private BunkerContentData data;

    [Header("Roots")]
    [SerializeField] private GameObject unlockedRoot;
    [SerializeField] private GameObject lockedRoot;

    public BunkerContentData Data => data;
    public string Id => data != null ? data.Id : string.Empty;

    private void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        bool unlocked = IsUnlocked();

        if (unlockedRoot != null)
            unlockedRoot.SetActive(unlocked);

        if (lockedRoot != null)
            lockedRoot.SetActive(!unlocked);
    }

    public bool IsUnlocked()
    {
        if (data == null)
            return false;

        return PlayerPrefs.GetInt(KeyPrefix + data.Id, 0) == 1;
    }
    private void OnEnable()
    {
        BunkerContext.Instance?.ContentRegistry?.Register(this);
    }

    private void OnDisable()
    {
        BunkerContext.Instance?.ContentRegistry?.Unregister(this);
    }
}