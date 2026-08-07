using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerPanelManager : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private SelectionPanelController selectionPanelController;
    [SerializeField] private GameObject mapPanel;

    [Header("Panel UI")]
    [SerializeField] private BunkerShopUI shopUI;
    [SerializeField] private MetaUpgradeShopUI metaUpgradeShopUI;
    [SerializeField] private Button upgradeBackButton;

    private BunkerStationUpgradePanel stationUpgradePanel;

    [Header("Run")]
    [SerializeField] private BunkerRunStarter runStarter;

    public bool IsAnyPanelOpen =>
        (selectionPanelController != null && selectionPanelController.IsOpen) ||
        (mapPanel != null && mapPanel.activeInHierarchy) ||
        (stationUpgradePanel != null && stationUpgradePanel.IsVisible);

    private void Awake()
    {
        stationUpgradePanel = GetComponent<BunkerStationUpgradePanel>();
        if (stationUpgradePanel == null)
            stationUpgradePanel = gameObject.AddComponent<BunkerStationUpgradePanel>();

        if (upgradeBackButton != null)
            upgradeBackButton.onClick.AddListener(CloseAll);
    }

    private void OnDestroy()
    {
        if (upgradeBackButton != null)
            upgradeBackButton.onClick.RemoveListener(CloseAll);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && IsAnyPanelOpen)
            CloseAll();
    }

    public void OpenCharacterSelection()
    {
        if (!TryGetSelectionController(out SelectionPanelController controller))
            return;

        CloseAll(false);
        controller.ShowCharacters();
    }

    public void OpenWeaponSelection()
    {
        if (!TryGetSelectionController(out SelectionPanelController controller))
            return;

        CloseAll(false);
        controller.ShowWeapons();
    }

    public void OpenShop()
    {
        if (!TryGetSelectionController(out SelectionPanelController controller))
            return;

        CloseAll(false);
        controller.ShowShop();
        shopUI?.Refresh();
    }

    public void OpenMap()
    {
        CloseAll(false);

        if (mapPanel == null)
        {
            Debug.LogError("[BunkerPanelManager] MapPanel is not assigned.", this);
            return;
        }

        mapPanel.SetActive(true);
    }

    public void OpenUpgrade()
    {
        if (!TryGetSelectionController(out SelectionPanelController controller))
            return;

        CloseAll(false);
        controller.ShowUpgrade();
        metaUpgradeShopUI?.Refresh();
    }

    public void CloseAll()
    {
        CloseAll(true);
    }

    public void CloseAll(bool playSound)
    {
        selectionPanelController?.Hide();

        if (mapPanel != null)
            mapPanel.SetActive(false);

        stationUpgradePanel?.Hide();

    }

    public void ShowStationProgression(BunkerStationId stationId)
    {
        stationUpgradePanel?.Show(stationId);
    }

    public void StartRun()
    {
        if (runStarter == null)
        {
            Debug.LogError("[BunkerPanelManager] BunkerRunStarter is not assigned.", this);
            return;
        }

        runStarter.StartRun();
    }

    private bool TryGetSelectionController(out SelectionPanelController controller)
    {
        controller = selectionPanelController;

        if (controller != null)
            return true;

        Debug.LogError("[BunkerPanelManager] SelectionPanelController is not assigned.", this);
        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (selectionPanelController == null)
            Debug.LogWarning("[BunkerPanelManager] SelectionPanelController is not assigned.", this);

        if (upgradeBackButton == null)
            Debug.LogWarning("[BunkerPanelManager] UpgradeBackButton is not assigned.", this);
    }
#endif
}
