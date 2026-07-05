using UnityEngine;

public sealed class SelectionPanelController : MonoBehaviour
{
    [SerializeField] private GameObject root;

    [Header("Panels")]
    [SerializeField] private GameObject playerSelectPanel;
    [SerializeField] private GameObject weaponSelectPanel;
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private GameObject sceneSelectPanel;

    public void ShowCharacters() => ShowOnly(playerSelectPanel);
    public void ShowWeapons() => ShowOnly(weaponSelectPanel);
    public void ShowShop() => ShowOnly(shopPanel);
    public void ShowUpgrade() => ShowOnly(upgradePanel);
    public void ShowScenes() => ShowOnly(sceneSelectPanel);

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    private void ShowOnly(GameObject target)
    {
        if (root != null)
            root.SetActive(true);

        if (playerSelectPanel != null)
            playerSelectPanel.SetActive(playerSelectPanel == target);

        if (weaponSelectPanel != null)
            weaponSelectPanel.SetActive(weaponSelectPanel == target);

        if (shopPanel != null)
            shopPanel.SetActive(shopPanel == target);

        if (upgradePanel != null)
            upgradePanel.SetActive(upgradePanel == target);

        if (sceneSelectPanel != null)
            sceneSelectPanel.SetActive(sceneSelectPanel == target);
    }
}