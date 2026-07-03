using UnityEngine;

public sealed class BunkerPurchasedObject : MonoBehaviour
{
    [SerializeField] private string itemId;
    [SerializeField] private GameObject targetRoot;

    private const string KeyPrefix = "BunkerShop_";

    private void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        bool purchased = PlayerPrefs.GetInt(KeyPrefix + itemId, 0) == 1;

        if (targetRoot != null)
            targetRoot.SetActive(purchased);
    }
}