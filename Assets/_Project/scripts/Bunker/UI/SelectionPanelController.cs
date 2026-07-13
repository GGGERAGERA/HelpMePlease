using UnityEngine;

public sealed class SelectionPanelController : MonoBehaviour
{
    private enum PanelType
    {
        None,
        Characters,
        Weapons,
        Shop,
        Upgrade,
        Scenes
    }

    [SerializeField] private GameObject root;

    [Header("Panels")]
    [SerializeField] private GameObject playerSelectPanel;
    [SerializeField] private GameObject weaponSelectPanel;
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private GameObject sceneSelectPanel;

    public bool IsOpen => root != null && root.activeInHierarchy;

    public void ShowCharacters() => Show(PanelType.Characters);
    public void ShowWeapons() => Show(PanelType.Weapons);
    public void ShowShop() => Show(PanelType.Shop);
    public void ShowUpgrade() => Show(PanelType.Upgrade);
    public void ShowScenes() => Show(PanelType.Scenes);

    public void Hide()
    {
        SetPanelStates(PanelType.None);

        if (root != null)
            root.SetActive(false);
    }

    private void Show(PanelType panelType)
    {
        GameObject target = GetPanel(panelType);

        if (root == null)
        {
            Debug.LogError("[SelectionPanelController] Root is not assigned.", this);
            return;
        }

        if (target == null)
        {
            Debug.LogError($"[SelectionPanelController] Panel '{panelType}' is not assigned.", this);
            return;
        }

        SetPanelStates(panelType);
        root.SetActive(true);
    }

    private void SetPanelStates(PanelType activePanel)
    {
        SetActive(playerSelectPanel, activePanel == PanelType.Characters);
        SetActive(weaponSelectPanel, activePanel == PanelType.Weapons);
        SetActive(shopPanel, activePanel == PanelType.Shop);
        SetActive(upgradePanel, activePanel == PanelType.Upgrade);
        SetActive(sceneSelectPanel, activePanel == PanelType.Scenes);
    }

    private GameObject GetPanel(PanelType panelType)
    {
        return panelType switch
        {
            PanelType.Characters => playerSelectPanel,
            PanelType.Weapons => weaponSelectPanel,
            PanelType.Shop => shopPanel,
            PanelType.Upgrade => upgradePanel,
            PanelType.Scenes => sceneSelectPanel,
            _ => null
        };
    }

    private static void SetActive(GameObject panel, bool isActive)
    {
        if (panel != null && panel.activeSelf != isActive)
            panel.SetActive(isActive);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (root == null)
            Debug.LogWarning("[SelectionPanelController] Root is not assigned.", this);
    }
#endif
}
