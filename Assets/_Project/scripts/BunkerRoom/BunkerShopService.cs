using UnityEngine;

public sealed class BunkerShopService : MonoBehaviour
{
    public bool IsPurchased(BunkerShopItemData item)
    {
        return PlayerPrefs.GetInt(GetKey(item), 0) == 1;
    }

    public bool CanBuy(BunkerShopItemData item)
    {
        return item != null &&
               !IsPurchased(item) &&
               CurrencyManager.Instance != null &&
               CurrencyManager.Instance.TotalGold >= item.Price;
    }

    public bool TryBuy(BunkerShopItemData item)
    {
        if (!CanBuy(item))
            return false;

        CurrencyManager.Instance.SpendGold(item.Price);

        PlayerPrefs.SetInt(GetKey(item), 1);
        PlayerPrefs.Save();
        foreach (var purchasedObject in FindObjectsByType<BunkerPurchasedObject>(FindObjectsSortMode.None))
        {
            purchasedObject.Refresh();
        }

        return true;
    }

    private string GetKey(BunkerShopItemData item)
    {
        return $"BunkerShop_{item.Id}";
    }
}