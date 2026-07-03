using System.Collections.Generic;
using UnityEngine;

public sealed class BunkerShopUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private BunkerShopService shopService;
    [SerializeField] private BunkerShopItemData[] items;

    [Header("View")]
    [SerializeField] private Transform itemsContainer;
    [SerializeField] private BunkerShopItemView itemViewPrefab;

    private readonly List<BunkerShopItemView> spawnedViews = new();

    public void Refresh()
    {
        Clear();

        foreach (var item in items)
        {
            if (item == null)
                continue;

            BunkerShopItemView view = Instantiate(itemViewPrefab, itemsContainer);
            view.Setup(item, shopService);
            spawnedViews.Add(view);
        }
    }

    private void Clear()
    {
        foreach (var view in spawnedViews)
        {
            if (view != null)
                Destroy(view.gameObject);
        }

        spawnedViews.Clear();
    }
}