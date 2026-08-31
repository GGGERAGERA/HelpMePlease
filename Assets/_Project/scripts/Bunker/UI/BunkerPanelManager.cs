using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerPanelManager : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private SelectionPanelController selectionPanelController;
    [SerializeField] private BunkerSelectionSourceHub selectionSources;
    [SerializeField] private GameObject mapPanel;

    [Header("Panel UI")]
    [SerializeField] private BunkerShopUI shopUI;
    [SerializeField] private AudioSettingsPanel audioSettingsPanel;

    [Header("Prefab-Driven Station Panels")]
    [SerializeField] private GameObject
        stationUpgradePanelPrefab;
    private BunkerStationUpgradePanel stationUpgradePanel;

    [Header("Run")]
    [SerializeField] private BunkerRunStarter runStarter;

    public bool IsAnyPanelOpen =>
        (selectionPanelController != null && selectionPanelController.IsOpen) ||
        (mapPanel != null && mapPanel.activeInHierarchy) ||
        (stationUpgradePanel != null && stationUpgradePanel.IsVisible) ||
        (audioSettingsPanel != null && audioSettingsPanel.IsOpen);

    private void Awake()
    {
        // MainMenu is artist-authored with the shared selection root active.
        // Close it before the first cursor Update so IsAnyPanelOpen cannot
        // disable the entire bunker interaction pipeline on scene load.
        selectionPanelController?.Hide();

        if (stationUpgradePanelPrefab != null)
        {
            GameObject stationPanelObject = Instantiate(
                stationUpgradePanelPrefab,
                transform);
            stationPanelObject.name = stationUpgradePanelPrefab.name;
            stationUpgradePanel =
                stationPanelObject.GetComponent<BunkerStationUpgradePanel>();
            if (stationUpgradePanel == null)
            {
                Debug.LogError(
                    "[BunkerPanelManager] StationUpgradePanel prefab has " +
                    "no BunkerStationUpgradePanel component.",
                    stationPanelObject);
            }
        }
        else
        {
            Debug.LogError(
                "[BunkerPanelManager] StationUpgradePanel prefab is not assigned.",
                this);
        }

    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;
        if (audioSettingsPanel != null && audioSettingsPanel.IsOpen)
        {
            audioSettingsPanel.Close();
            return;
        }
        if (IsAnyPanelOpen)
            CloseAll();
    }

    public void OpenCharacterSelection()
    {
        if (!TryGetSelectionController(out SelectionPanelController controller))
            return;

        CloseAll(false);
        controller.ShowSelection(selectionSources?.Characters, this);
    }

    public void OpenWeaponSelection()
    {
        if (!TryGetSelectionController(out SelectionPanelController controller))
            return;

        CloseAll(false);
        controller.ShowSelection(selectionSources?.Weapons, this);
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
        controller.ShowSelection(selectionSources?.Upgrades, this);
    }

    public void OpenAnomalyStabilizers()
    {
        if (!TryGetSelectionController(out SelectionPanelController controller))
            return;

        CloseAll(false);
        controller.ShowSelection(selectionSources?.Anomalies, this);
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
        if (audioSettingsPanel != null && audioSettingsPanel.IsOpen)
            audioSettingsPanel.Close();

    }

    public void ShowStationProgression(BunkerStationId stationId)
    {
        if (selectionPanelController != null && selectionPanelController.IsOpen)
            return;

        stationUpgradePanel?.Show(stationId);
    }

    public void OpenSettings()
    {
        if (audioSettingsPanel == null)
        {
            Debug.LogWarning("[BunkerPanelManager] AudioSettingsPanel is not assigned.", this);
            return;
        }
        audioSettingsPanel.Open();
    }

    public void StartRun(Transform transitionTarget)
    {
        if (runStarter == null)
        {
            Debug.LogError("[BunkerPanelManager] BunkerRunStarter is not assigned.", this);
            return;
        }

        runStarter.StartRun(transitionTarget);
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

        if (selectionSources == null)
            Debug.LogWarning("[BunkerPanelManager] Selection sources are not assigned.", this);

    }
#endif
}
