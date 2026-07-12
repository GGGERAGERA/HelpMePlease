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
    [SerializeField] private TextMeshProUGUI buyButtonText;
    [SerializeField] private GameObject purchasedMark;

    private BunkerContentData item;
    private BunkerShopService shopService;

    private void Awake()
    {
        if (buyButton != null)
            buyButton.onClick.AddListener(Buy);
    }

    private void OnDestroy()
    {
        if (buyButton != null)
            buyButton.onClick.RemoveListener(Buy);
    }

    public void Setup(BunkerContentData itemData, BunkerShopService service)
    {
        item = itemData;
        shopService = service;
        Refresh();
    }

    private void Buy()
    {
        if (shopService == null || item == null)
            return;

        bool success = shopService.TryBuy(item);

        if (!success)
            return;

        Refresh();
    }

    private void Refresh()
    {
        if (item == null)
            return;

        if (titleText != null) titleText.text = item.Title;
        if (descriptionText != null) descriptionText.text = item.Description;
        if (priceText != null) priceText.text = item.Price.ToString();

        if (iconImage != null)
        {
            iconImage.sprite = item.Icon;
            iconImage.enabled = item.Icon != null;
        }

        bool purchased = shopService != null && shopService.IsPurchased(item);
        bool canBuy = shopService != null && shopService.CanBuy(item);

        if (buyButton != null)
            buyButton.interactable = canBuy;

        if (buyButtonText != null)
            buyButtonText.text = purchased ? "Куплено" : "Купить";

        if (purchasedMark != null)
            purchasedMark.SetActive(purchased);
    }
}