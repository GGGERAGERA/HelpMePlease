using UnityEngine;

public sealed class BunkerShopService : MonoBehaviour
{

    [ContextMenu("Clear Shop Purchases")]
    private void ClearShopPurchases()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Debug.Log("[BunkerShopService] PlayerPrefs cleared.");
    }
    private const string KeyPrefix = "BunkerShop_";

    public bool IsPurchased(BunkerShopItemData item)
    {
        if (item == null)
            return false;

        return PlayerPrefs.GetInt(KeyPrefix + item.Id, 0) == 1;
    }

    public bool CanBuy(BunkerShopItemData item)
    {
        if (item == null)
            return false;

        if (IsPurchased(item))
            return false;

        if (CurrencyManager.Instance == null)
            return false;

        return CurrencyManager.Instance.TotalGold >= item.Price;
    }

    public bool TryBuy(BunkerShopItemData item)
    {
        if (!CanBuy(item))
            return false;

        bool spent = CurrencyManager.Instance.SpendGold(item.Price);

        if (!spent)
            return false;

        PlayerPrefs.SetInt(KeyPrefix + item.Id, 1);
        PlayerPrefs.Save();

        return true;
    }
}