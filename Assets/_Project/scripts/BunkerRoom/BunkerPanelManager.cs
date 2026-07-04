using UnityEngine;

public sealed class BunkerPanelManager : MonoBehaviour
{
    [Header("Selection")]
    [SerializeField] private SelectionPanelController selectionPanelController;

    [SerializeField] private BunkerShopUI shopUI;
    [SerializeField] private GameObject mapPanel;
    [SerializeField] private GameObject upgradePanel;


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            CloseAll();
    }
    public void OpenCharacterSelection()
    {
        CloseAll();
        selectionPanelController.ShowCharacters();
    }

    public void OpenWeaponSelection()
    {
        CloseAll();
        selectionPanelController.ShowWeapons();
    }

    public void OpenShop()
    {
        Debug.Log("[BunkerPanelManager] OpenShop");

        if (selectionPanelController == null)
        {
            Debug.LogError("[BunkerPanelManager] SelectionPanelController is null");
            return;
        }

        selectionPanelController.ShowShop();

        if (shopUI != null)
            shopUI.Refresh();
    }

    public void OpenMap()
    {
        CloseAll();

        if (mapPanel != null)
            mapPanel.SetActive(true);
    }

    public void OpenUpgrade()
    {
        CloseAll();

        if (upgradePanel != null)
            upgradePanel.SetActive(true);
    }

    public void CloseAll()
    {
        if (selectionPanelController != null)
            selectionPanelController.Hide();

        if (mapPanel != null)
            mapPanel.SetActive(false);

        if (upgradePanel != null)
            upgradePanel.SetActive(false);
    }
}