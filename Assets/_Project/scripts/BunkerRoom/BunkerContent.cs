using UnityEngine;

public sealed class BunkerContent : MonoBehaviour
{
    private const string KeyPrefix = "BunkerShop_";

    [Header("Identity")]
    [SerializeField] private string itemId;

    [Header("Roots")]
    [SerializeField] private GameObject unlockedRoot;
    [SerializeField] private GameObject lockedRoot;

    public string ItemId => itemId;

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
        return PlayerPrefs.GetInt(KeyPrefix + itemId, 0) == 1;
    }
}