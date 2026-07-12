using UnityEngine;

public sealed class BunkerShopService : MonoBehaviour
{
    private const string KeyPrefix = "BunkerShop_";

    public bool IsPurchased(BunkerContentData item)
    {
        if (item == null)
            return false;

        return PlayerPrefs.GetInt(KeyPrefix + item.Id, 0) == 1;
    }

    public bool CanBuy(BunkerContentData item)
    {
        if (item == null)
            return false;

        if (IsPurchased(item))
            return false;

        if (CurrencyManager.Instance == null)
            return false;

        return CurrencyManager.Instance.TotalGold >= item.Price;
    }

    public bool TryBuy(BunkerContentData item)
    {
        if (!CanBuy(item))
            return false;

        bool spent = CurrencyManager.Instance.SpendGold(item.Price);

        if (!spent)
            return false;

        PlayerPrefs.SetInt(KeyPrefix + item.Id, 1);
        PlayerPrefs.Save();

        RefreshBunkerContent();

        return true;
    }

    private void RefreshBunkerContent()
    {
        BunkerContentRegistry registry = BunkerContext.Instance != null
            ? BunkerContext.Instance.ContentRegistry
            : null;

        if (registry != null)
        {
            registry.RefreshAll();
            return;
        }

        BunkerContent[] contents =
            FindObjectsByType<BunkerContent>(FindObjectsSortMode.None);

        foreach (var content in contents)
            content.Refresh();
    }
}