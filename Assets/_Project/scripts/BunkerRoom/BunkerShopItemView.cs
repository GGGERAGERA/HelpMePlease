using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerShopItemView : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Button buyButton;
    [SerializeField] private GameObject purchasedMark;

    private BunkerShopItemData item;
    private BunkerShopService shopService;

    private void Awake()
    {
        buyButton.onClick.AddListener(Buy);
    }

    public void Setup(BunkerShopItemData itemData, BunkerShopService service)
    {
        item = itemData;
        shopService = service;

        Refresh();
    }

    private void Buy()
    {
        if (shopService == null || item == null)
            return;

        shopService.TryBuy(item);
        Refresh();
    }

    private void Refresh()
    {
        if (item == null)
            return;

        titleText.text = item.Title;
        descriptionText.text = item.Description;
        priceText.text = item.Price.ToString();

        if (iconImage != null)
        {
            iconImage.sprite = item.Icon;
            iconImage.enabled = item.Icon != null;
        }

        bool purchased = shopService != null && shopService.IsPurchased(item);
        bool canBuy = shopService != null && shopService.CanBuy(item);

        buyButton.interactable = canBuy;

        if (purchasedMark != null)
            purchasedMark.SetActive(purchased);
    }
}