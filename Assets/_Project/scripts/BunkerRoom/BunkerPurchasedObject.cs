using UnityEngine;

public sealed class BunkerPurchasedObject : MonoBehaviour
{
    [SerializeField] private string itemId;
    [SerializeField] private GameObject targetRoot;

    private void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        bool purchased = PlayerPrefs.GetInt($"BunkerShop_{itemId}", 0) == 1;

        if (targetRoot != null)
            targetRoot.SetActive(purchased);
    }
}