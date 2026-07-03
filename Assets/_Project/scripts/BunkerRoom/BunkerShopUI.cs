using UnityEngine;

public sealed class BunkerShopUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private BunkerShopService shopService;
    [SerializeField] private BunkerShopItemView[] itemViews;
    [SerializeField] private BunkerShopItemData[] items;

    private void Awake()
    {
        Hide();
    }

    public void Show()
    {
        Refresh();
    }

    public void Hide()
    {
    }

    public void Refresh()
    {
        int count = Mathf.Min(itemViews.Length, items.Length);

        for (int i = 0; i < itemViews.Length; i++)
        {
            bool active = i < count;
            itemViews[i].gameObject.SetActive(active);

            if (active)
                itemViews[i].Setup(items[i], shopService);
        }
    }
}